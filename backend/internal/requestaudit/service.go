package requestaudit

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"log"
	"strings"
	"sync"
	"sync/atomic"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/config"
	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"github.com/Wei-Shaw/sub2api/internal/service"
)

const encryptionVersionAES256GCM = "aes-256-gcm-v1"

type captureEnvelope struct {
	record       *Record
	requestBody  []byte
	responseBody []byte
	policy       Policy
}

type Service struct {
	repo                 *Repository
	encryptor            service.SecretEncryptor
	encryptionConfigured bool
	policy               atomic.Pointer[Policy]
	queue                chan *captureEnvelope
	started              atomic.Bool
	stopped              atomic.Bool
	cancel               context.CancelFunc
	wg                   sync.WaitGroup
	enqueued             atomic.Int64
	persisted            atomic.Int64
	dropped              atomic.Int64
	failed               atomic.Int64
	stateMu              sync.RWMutex
	lastPersistedAt      time.Time
	lastCleanupAt        time.Time
	lastCleanupCount     int64
}

func NewService(repo *Repository, encryptor service.SecretEncryptor, cfg *config.Config) (*Service, error) {
	if repo == nil || repo.db == nil {
		return nil, errors.New("request audit repository unavailable")
	}
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	policy, err := repo.LoadPolicy(ctx)
	if err != nil {
		return nil, fmt.Errorf("load request audit policy: %w", err)
	}
	svc := &Service{
		repo:                 repo,
		encryptor:            encryptor,
		encryptionConfigured: cfg != nil && cfg.Totp.EncryptionKeyConfigured,
		queue:                make(chan *captureEnvelope, defaultQueueSize),
	}
	policy.EncryptionConfigured = svc.encryptionConfigured
	svc.policy.Store(policy)
	return svc, nil
}

func (s *Service) Start() {
	if s == nil || !s.started.CompareAndSwap(false, true) {
		return
	}
	ctx, cancel := context.WithCancel(context.Background())
	s.cancel = cancel
	for i := 0; i < 2; i++ {
		s.wg.Add(1)
		go s.worker(ctx)
	}
	s.wg.Add(1)
	go s.cleanupLoop(ctx)
}

func (s *Service) Shutdown(ctx context.Context) error {
	if s == nil || !s.stopped.CompareAndSwap(false, true) {
		return nil
	}
	if s.cancel != nil {
		s.cancel()
	}
	done := make(chan struct{})
	go func() {
		s.wg.Wait()
		close(done)
	}()
	select {
	case <-done:
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}

func (s *Service) GetPolicy() Policy {
	if s == nil {
		return Policy{}
	}
	if current := s.policy.Load(); current != nil {
		return *current
	}
	return Policy{}
}

func (s *Service) UpdatePolicy(ctx context.Context, req UpdatePolicyRequest, adminID int64) (*Policy, error) {
	if err := s.validatePolicy(req); err != nil {
		return nil, err
	}
	updated, err := s.repo.UpdatePolicy(ctx, req, adminID)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, infraerrors.Conflict("request_audit_config_conflict", "请求审计策略已被其他管理员更新，请刷新后重试")
	}
	if err != nil {
		return nil, err
	}
	updated.EncryptionConfigured = s.encryptionConfigured
	s.policy.Store(updated)
	return updated, nil
}

func (s *Service) validatePolicy(req UpdatePolicyRequest) error {
	if req.ExpectedVersion < 1 {
		return infraerrors.BadRequest("request_audit_version_required", "必须提供有效的策略版本")
	}
	switch req.CaptureMode {
	case CaptureModeAll, CaptureModeErrors, CaptureModeSample:
	default:
		return infraerrors.BadRequest("request_audit_invalid_capture_mode", "记录模式无效")
	}
	if req.SampleRate < 0 || req.SampleRate > 100 {
		return infraerrors.BadRequest("request_audit_invalid_sample_rate", "抽样比例必须在 0 到 100 之间")
	}
	if req.RetentionDays < 1 || req.RetentionDays > 3650 {
		return infraerrors.BadRequest("request_audit_invalid_retention", "保留周期必须在 1 到 3650 天之间")
	}
	if req.RedactionLevel != RedactionStandard && req.RedactionLevel != RedactionStrict {
		return infraerrors.BadRequest("request_audit_invalid_redaction", "脱敏级别无效")
	}
	if req.MaxBodyBytes < minBodyBytes || req.MaxBodyBytes > maxBodyBytes {
		return infraerrors.BadRequest("request_audit_invalid_body_limit", "单个正文上限必须在 4 KB 到 4 MB 之间")
	}
	if req.StoreEncryptedContent && (!s.encryptionConfigured || s.encryptor == nil) {
		return infraerrors.BadRequest("request_audit_encryption_key_required", "启用加密原文前必须配置持久化 TOTP 加密密钥")
	}
	return nil
}

func (s *Service) Enqueue(record *Record, requestBody, responseBody []byte, policy Policy) {
	if s == nil || record == nil || s.stopped.Load() {
		return
	}
	envelope := &captureEnvelope{
		record:       record,
		requestBody:  append([]byte(nil), requestBody...),
		responseBody: append([]byte(nil), responseBody...),
		policy:       policy,
	}
	select {
	case s.queue <- envelope:
		s.enqueued.Add(1)
	default:
		s.dropped.Add(1)
	}
}

func (s *Service) worker(ctx context.Context) {
	defer s.wg.Done()
	for {
		select {
		case envelope := <-s.queue:
			s.persist(envelope)
		case <-ctx.Done():
			for {
				select {
				case envelope := <-s.queue:
					s.persist(envelope)
				default:
					return
				}
			}
		}
	}
}

func (s *Service) persist(envelope *captureEnvelope) {
	if envelope == nil || envelope.record == nil {
		return
	}
	item := envelope.record
	policy := envelope.policy
	if policy.CaptureRequestBody {
		s.prepareBody(item, true, envelope.requestBody, item.RequestContentType, policy)
	} else {
		item.RequestBodyOmitted = true
	}
	if policy.CaptureResponseBody {
		s.prepareBody(item, false, envelope.responseBody, item.ResponseContentType, policy)
	} else {
		item.ResponseBodyOmitted = true
	}
	item.ExpiresAt = item.CreatedAt.AddDate(0, 0, policy.RetentionDays)
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	err := s.repo.Insert(ctx, item)
	cancel()
	if err != nil {
		s.failed.Add(1)
		log.Printf("request audit persist failed: %v", err)
		return
	}
	s.persisted.Add(1)
	s.stateMu.Lock()
	s.lastPersistedAt = time.Now().UTC()
	s.stateMu.Unlock()
}

func (s *Service) prepareBody(item *Record, request bool, body []byte, contentType string, policy Policy) {
	if len(body) == 0 {
		return
	}
	textual := textualContent(contentType, body)
	preview := buildPreview(body, contentType, policy.RedactionLevel)
	if request {
		item.RequestPreview = preview
		item.RequestBodyOmitted = !textual
	} else {
		item.ResponsePreview = preview
		item.ResponseBodyOmitted = !textual
	}
	if !policy.StoreEncryptedContent || !textual {
		return
	}
	if !s.encryptionConfigured || s.encryptor == nil {
		item.ContentError = "persistent encryption key unavailable"
		return
	}
	ciphertext, err := s.encryptor.Encrypt(string(body))
	if err != nil {
		item.ContentError = "content encryption failed"
		return
	}
	item.EncryptionVersion = encryptionVersionAES256GCM
	if request {
		item.RequestBodyCiphertext = ciphertext
	} else {
		item.ResponseBodyCiphertext = ciphertext
	}
}

func (s *Service) List(ctx context.Context, filter Filter, page, pageSize int) (*Page, error) {
	return s.repo.List(ctx, filter, page, pageSize)
}

func (s *Service) Get(ctx context.Context, id int64) (*Record, error) {
	item, err := s.repo.Get(ctx, id)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, infraerrors.NotFound("request_audit_record_not_found", "请求审计记录不存在")
	}
	return item, err
}

func (s *Service) GetContent(ctx context.Context, id int64) (*Content, error) {
	item, err := s.Get(ctx, id)
	if err != nil {
		return nil, err
	}
	result := &Content{RecordID: id}
	if item.RequestBodyCiphertext == "" && item.ResponseBodyCiphertext == "" {
		return result, nil
	}
	if !s.encryptionConfigured || s.encryptor == nil {
		return nil, infraerrors.ServiceUnavailable("request_audit_decryption_unavailable", "请求审计解密密钥不可用")
	}
	if item.RequestBodyCiphertext != "" {
		result.RequestBody, err = s.encryptor.Decrypt(item.RequestBodyCiphertext)
		if err != nil {
			return nil, infraerrors.ServiceUnavailable("request_audit_decryption_failed", "请求正文解密失败")
		}
		result.RequestAvailable = true
	}
	if item.ResponseBodyCiphertext != "" {
		result.ResponseBody, err = s.encryptor.Decrypt(item.ResponseBodyCiphertext)
		if err != nil {
			return nil, infraerrors.ServiceUnavailable("request_audit_decryption_failed", "响应正文解密失败")
		}
		result.ResponseAvailable = true
	}
	return result, nil
}

func (s *Service) Runtime() Runtime {
	if s == nil {
		return Runtime{}
	}
	s.stateMu.RLock()
	defer s.stateMu.RUnlock()
	policy := s.GetPolicy()
	return Runtime{
		Enabled: policy.Enabled, QueueDepth: len(s.queue), QueueCapacity: cap(s.queue),
		EnqueuedTotal: s.enqueued.Load(), PersistedTotal: s.persisted.Load(),
		DroppedTotal: s.dropped.Load(), FailedTotal: s.failed.Load(),
		LastPersistedAt: s.lastPersistedAt, LastCleanupAt: s.lastCleanupAt,
		LastCleanupCount: s.lastCleanupCount,
	}
}

func (s *Service) cleanupLoop(ctx context.Context) {
	defer s.wg.Done()
	s.cleanupExpired()
	ticker := time.NewTicker(time.Hour)
	defer ticker.Stop()
	for {
		select {
		case <-ticker.C:
			s.cleanupExpired()
		case <-ctx.Done():
			return
		}
	}
}

func (s *Service) cleanupExpired() {
	var deleted int64
	for {
		ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
		count, err := s.repo.DeleteExpired(ctx, time.Now().UTC(), 1000)
		cancel()
		if err != nil {
			log.Printf("request audit cleanup failed: %v", err)
			break
		}
		deleted += count
		if count < 1000 {
			break
		}
	}
	s.stateMu.Lock()
	s.lastCleanupAt = time.Now().UTC()
	s.lastCleanupCount = deleted
	s.stateMu.Unlock()
}

func normalizeError(value string) string {
	value = strings.TrimSpace(value)
	if len(value) > 255 {
		value = value[:255]
	}
	return value
}
