package service

import (
	"context"
	"crypto/tls"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/config"
	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"github.com/google/uuid"
)

const (
	UpstreamProviderOpenAI = "openai"
	UpstreamProviderClaude = "claude"
	UpstreamAuthAPIKey     = "api_key"
	UpstreamAuthWIF        = "wif"

	upstreamAccountTestTimeout     = 10 * time.Second
	upstreamAccountMaxResponseSize = 512 * 1024
)

var ErrUpstreamAccountNotFound = infraerrors.NotFound("UPSTREAM_ACCOUNT_NOT_FOUND", "上游账号不存在")

// UpstreamAccount is deliberately separate from Account. Account represents
// the official Go gateway account domain; UpstreamAccount mirrors the Worker
// provider_accounts domain and is persisted in upstream_accounts.
type UpstreamAccount struct {
	ID                   string
	Name                 string
	ProviderType         string
	BaseURL              string
	AuthType             string
	CredentialCiphertext string
	CredentialHint       string

	OAuthProfile   *string
	OAuthAccountID *string
	OAuthEmail     *string
	OAuthExpiresAt *time.Time

	WIFSubjectTokenURL    *string
	WIFClientID           *string
	WIFClientAuthMethod   *string
	WIFAudience           *string
	WIFScope              *string
	WIFIdentityProviderID *string
	WIFServiceAccountID   *string
	WIFFederationRuleID   *string
	WIFOrganizationID     *string
	WIFWorkspaceID        *string

	IsActive                      bool
	Priority                      int
	Weight                        int
	MaxConcurrency                int
	RPMLimit                      int
	CircuitBreakerThreshold       int
	CircuitBreakerCooldownSeconds int

	QuotaStatus                    string
	QuotaUtilization               *float64
	QuotaResetsAt                  *time.Time
	QuotaCheckedAt                 *time.Time
	QuotaFiveHourUtilization       *float64
	QuotaFiveHourResetsAt          *time.Time
	QuotaSevenDayUtilization       *float64
	QuotaSevenDayResetsAt          *time.Time
	QuotaSevenDaySonnetUtilization *float64
	QuotaSevenDaySonnetResetsAt    *time.Time
	CooldownUntil                  *time.Time
	CooldownReason                 *string
	LastUpstreamStatus             *int
	LastSuccessAt                  *time.Time
	LastFailureAt                  *time.Time
	CreatedAt                      time.Time
	UpdatedAt                      time.Time
	DeletedAt                      *time.Time
}

type UpstreamAccountRepository interface {
	Create(ctx context.Context, account *UpstreamAccount) error
	GetByID(ctx context.Context, id string) (*UpstreamAccount, error)
	List(ctx context.Context) ([]UpstreamAccount, error)
	Update(ctx context.Context, account *UpstreamAccount) error
	SoftDelete(ctx context.Context, id string, deletedAt time.Time) error
}

type UpstreamAccountInput struct {
	Name                          string
	ProviderType                  string
	BaseURL                       string
	AuthType                      string
	APIKey                        string
	WIFClientSecret               string
	WIFSubjectTokenURL            string
	WIFClientID                   string
	WIFClientAuthMethod           string
	WIFAudience                   string
	WIFScope                      string
	WIFIdentityProviderID         string
	WIFServiceAccountID           string
	WIFFederationRuleID           string
	WIFOrganizationID             string
	WIFWorkspaceID                string
	IsActive                      bool
	Priority                      int
	Weight                        int
	MaxConcurrency                int
	RPMLimit                      int
	CircuitBreakerThreshold       int
	CircuitBreakerCooldownSeconds int
}

type UpstreamConnectionTestResult struct {
	Success    bool   `json:"success"`
	Code       string `json:"code"`
	Message    string `json:"message"`
	LatencyMS  int64  `json:"latency_ms"`
	StatusCode *int   `json:"status_code,omitempty"`
	ModelCount *int   `json:"model_count,omitempty"`
}

type UpstreamAccountService struct {
	repo                    UpstreamAccountRepository
	encryptor               SecretEncryptor
	wif                     *WIFTokenProvider
	persistentEncryptionKey bool
	httpClient              *http.Client
}

func NewUpstreamAccountService(
	repo UpstreamAccountRepository,
	encryptor SecretEncryptor,
	wif *WIFTokenProvider,
	cfg *config.Config,
) *UpstreamAccountService {
	transport := http.DefaultTransport.(*http.Transport).Clone()
	transport.TLSClientConfig = &tls.Config{MinVersion: tls.VersionTLS12}
	return &UpstreamAccountService{
		repo:                    repo,
		encryptor:               encryptor,
		wif:                     wif,
		persistentEncryptionKey: cfg != nil && cfg.Totp.EncryptionKeyConfigured,
		httpClient: &http.Client{
			Transport: transport,
			Timeout:   upstreamAccountTestTimeout,
			CheckRedirect: func(_ *http.Request, _ []*http.Request) error {
				return http.ErrUseLastResponse
			},
		},
	}
}

func (s *UpstreamAccountService) List(ctx context.Context) ([]UpstreamAccount, error) {
	accounts, err := s.repo.List(ctx)
	if err != nil {
		return nil, fmt.Errorf("list upstream accounts: %w", err)
	}
	return accounts, nil
}

func (s *UpstreamAccountService) Get(ctx context.Context, id string) (*UpstreamAccount, error) {
	if strings.TrimSpace(id) == "" {
		return nil, ErrUpstreamAccountNotFound
	}
	return s.repo.GetByID(ctx, id)
}

func (s *UpstreamAccountService) Create(ctx context.Context, input UpstreamAccountInput) (*UpstreamAccount, error) {
	normalized, credential, err := normalizeUpstreamAccountInput(input, true)
	if err != nil {
		return nil, err
	}
	ciphertext, hint, err := s.encryptCredential(credential)
	if err != nil {
		return nil, err
	}
	now := time.Now().UTC()
	account := &UpstreamAccount{
		ID:                            uuid.NewString(),
		Name:                          normalized.Name,
		ProviderType:                  normalized.ProviderType,
		BaseURL:                       normalized.BaseURL,
		AuthType:                      normalized.AuthType,
		CredentialCiphertext:          ciphertext,
		CredentialHint:                hint,
		IsActive:                      normalized.IsActive,
		Priority:                      normalized.Priority,
		Weight:                        normalized.Weight,
		MaxConcurrency:                normalized.MaxConcurrency,
		RPMLimit:                      normalized.RPMLimit,
		CircuitBreakerThreshold:       normalized.CircuitBreakerThreshold,
		CircuitBreakerCooldownSeconds: normalized.CircuitBreakerCooldownSeconds,
		QuotaStatus:                   "unknown",
		CreatedAt:                     now,
		UpdatedAt:                     now,
	}
	applyUpstreamWIFInput(account, normalized)
	if err := s.repo.Create(ctx, account); err != nil {
		return nil, fmt.Errorf("create upstream account: %w", err)
	}
	return account, nil
}

func (s *UpstreamAccountService) Update(ctx context.Context, id string, input UpstreamAccountInput) (*UpstreamAccount, error) {
	current, err := s.repo.GetByID(ctx, id)
	if err != nil {
		return nil, err
	}
	normalized, submittedCredential, err := normalizeUpstreamAccountInput(input, false)
	if err != nil {
		return nil, err
	}
	boundaryChanged := upstreamCredentialBoundaryChanged(current, normalized)
	if boundaryChanged && submittedCredential == "" {
		return nil, upstreamCredentialRequired(normalized.AuthType)
	}

	current.Name = normalized.Name
	current.ProviderType = normalized.ProviderType
	current.BaseURL = normalized.BaseURL
	current.AuthType = normalized.AuthType
	current.IsActive = normalized.IsActive
	current.Priority = normalized.Priority
	current.Weight = normalized.Weight
	current.MaxConcurrency = normalized.MaxConcurrency
	current.RPMLimit = normalized.RPMLimit
	current.CircuitBreakerThreshold = normalized.CircuitBreakerThreshold
	current.CircuitBreakerCooldownSeconds = normalized.CircuitBreakerCooldownSeconds
	applyUpstreamWIFInput(current, normalized)
	if submittedCredential != "" {
		ciphertext, hint, encryptErr := s.encryptCredential(submittedCredential)
		if encryptErr != nil {
			return nil, encryptErr
		}
		current.CredentialCiphertext = ciphertext
		current.CredentialHint = hint
	}
	current.UpdatedAt = time.Now().UTC()
	if err := s.repo.Update(ctx, current); err != nil {
		return nil, fmt.Errorf("update upstream account: %w", err)
	}
	return current, nil
}

func (s *UpstreamAccountService) SetActive(ctx context.Context, id string, active bool) (*UpstreamAccount, error) {
	account, err := s.repo.GetByID(ctx, id)
	if err != nil {
		return nil, err
	}
	account.IsActive = active
	account.UpdatedAt = time.Now().UTC()
	if err := s.repo.Update(ctx, account); err != nil {
		return nil, fmt.Errorf("update upstream account scheduling: %w", err)
	}
	return account, nil
}

func (s *UpstreamAccountService) Delete(ctx context.Context, id string) error {
	if _, err := s.repo.GetByID(ctx, id); err != nil {
		return err
	}
	if err := s.repo.SoftDelete(ctx, id, time.Now().UTC()); err != nil {
		return fmt.Errorf("delete upstream account: %w", err)
	}
	return nil
}

func (s *UpstreamAccountService) TestDraft(ctx context.Context, input UpstreamAccountInput) (*UpstreamConnectionTestResult, error) {
	normalized, credential, err := normalizeUpstreamAccountInput(input, true)
	if err != nil {
		return nil, err
	}
	return s.testConnection(ctx, normalized, credential), nil
}

func (s *UpstreamAccountService) TestSaved(ctx context.Context, id string) (*UpstreamConnectionTestResult, error) {
	account, err := s.repo.GetByID(ctx, id)
	if err != nil {
		return nil, err
	}
	if s.encryptor == nil {
		return nil, infraerrors.New(http.StatusInternalServerError, "UPSTREAM_ACCOUNT_DECRYPTION_UNAVAILABLE", "上游账号凭据解密服务不可用")
	}
	credential, err := s.encryptor.Decrypt(account.CredentialCiphertext)
	if err != nil || strings.TrimSpace(credential) == "" {
		return &UpstreamConnectionTestResult{Success: false, Code: "credential_unavailable", Message: "已保存的认证凭据无法使用，请重新填写后再试。"}, nil
	}
	return s.testConnection(ctx, upstreamInputFromAccount(account), credential), nil
}

func (s *UpstreamAccountService) encryptCredential(credential string) (string, string, error) {
	credential = strings.TrimSpace(credential)
	if credential == "" {
		return "", "", infraerrors.BadRequest("UPSTREAM_ACCOUNT_CREDENTIAL_REQUIRED", "请输入上游认证凭据")
	}
	if !s.persistentEncryptionKey || s.encryptor == nil {
		return "", "", infraerrors.BadRequest("UPSTREAM_ACCOUNT_ENCRYPTION_KEY_REQUIRED", "保存上游账号前必须配置固定的 TOTP_ENCRYPTION_KEY")
	}
	ciphertext, err := s.encryptor.Encrypt(credential)
	if err != nil {
		return "", "", infraerrors.New(http.StatusInternalServerError, "UPSTREAM_ACCOUNT_ENCRYPTION_FAILED", "上游账号凭据加密失败")
	}
	hint := credential
	if len(hint) > 4 {
		hint = hint[len(hint)-4:]
	}
	return ciphertext, hint, nil
}

func (s *UpstreamAccountService) testConnection(ctx context.Context, input UpstreamAccountInput, credential string) *UpstreamConnectionTestResult {
	started := time.Now()
	upstreamCredential := credential
	if input.AuthType == UpstreamAuthWIF {
		if s.wif == nil {
			return upstreamConnectionFailure("wif_provider_unavailable", "WIF 令牌服务不可用。", started, nil)
		}
		result, err := s.wif.Resolve(ctx, WIFConfiguration{
			Platform:           upstreamWIFPlatform(input.ProviderType),
			SubjectTokenURL:    input.WIFSubjectTokenURL,
			ClientID:           input.WIFClientID,
			ClientSecret:       credential,
			ClientAuthMethod:   WIFClientAuthMethod(input.WIFClientAuthMethod),
			Audience:           input.WIFAudience,
			Scope:              input.WIFScope,
			IdentityProviderID: input.WIFIdentityProviderID,
			ServiceAccountID:   input.WIFServiceAccountID,
			FederationRuleID:   input.WIFFederationRuleID,
			OrganizationID:     input.WIFOrganizationID,
			WorkspaceID:        input.WIFWorkspaceID,
		})
		if err != nil {
			return upstreamConnectionFailure("wif_token_exchange_failed", "WIF 令牌交换失败，请检查身份联合配置后重试。", started, nil)
		}
		upstreamCredential = result.AccessToken
	}

	testCtx, cancel := context.WithTimeout(ctx, upstreamAccountTestTimeout)
	defer cancel()
	req, err := http.NewRequestWithContext(testCtx, http.MethodGet, upstreamModelsURL(input.BaseURL), nil)
	if err != nil {
		return upstreamConnectionFailure("invalid_upstream_url", "上游 API 地址无效。", started, nil)
	}
	req.Header.Set("Accept", "application/json")
	if input.ProviderType == UpstreamProviderOpenAI || input.AuthType == UpstreamAuthWIF {
		req.Header.Set("Authorization", "Bearer "+upstreamCredential)
	} else {
		req.Header.Set("X-Api-Key", upstreamCredential)
		req.Header.Set("Anthropic-Version", "2023-06-01")
	}
	resp, err := s.httpClient.Do(req)
	if err != nil {
		if testCtx.Err() != nil {
			return upstreamConnectionFailure("upstream_timeout", "连接上游超时，请稍后重试。", started, nil)
		}
		return upstreamConnectionFailure("upstream_unreachable", "无法连接上游服务，请检查地址和网络后重试。", started, nil)
	}
	defer func() { _ = resp.Body.Close() }()
	status := resp.StatusCode
	if status >= 300 && status < 400 {
		return upstreamConnectionFailure("upstream_redirect_rejected", "上游返回了重定向，已按安全策略拒绝。", started, &status)
	}
	if status < 200 || status >= 300 {
		return upstreamHTTPFailure(status, started)
	}
	payload, err := io.ReadAll(io.LimitReader(resp.Body, upstreamAccountMaxResponseSize+1))
	if err != nil || len(payload) > upstreamAccountMaxResponseSize {
		return upstreamConnectionFailure("upstream_response_too_large", "上游响应超过 512 KiB 限制。", started, &status)
	}
	var modelEnvelope struct {
		Data []json.RawMessage `json:"data"`
	}
	if err := json.Unmarshal(payload, &modelEnvelope); err != nil || modelEnvelope.Data == nil {
		return upstreamConnectionFailure("upstream_incompatible_response", "上游 /v1/models 响应格式不兼容。", started, &status)
	}
	count := len(modelEnvelope.Data)
	return &UpstreamConnectionTestResult{
		Success: true, Code: "connection_succeeded",
		Message:   fmt.Sprintf("连接成功，已读取 %d 个模型。", count),
		LatencyMS: time.Since(started).Milliseconds(), StatusCode: &status, ModelCount: &count,
	}
}

func normalizeUpstreamAccountInput(input UpstreamAccountInput, requireCredential bool) (UpstreamAccountInput, string, error) {
	input.Name = strings.TrimSpace(input.Name)
	if input.Name == "" || len([]rune(input.Name)) > 80 {
		return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_ACCOUNT_NAME", "账号名称不能为空且不能超过 80 个字符")
	}
	input.ProviderType = strings.ToLower(strings.TrimSpace(input.ProviderType))
	if input.ProviderType != UpstreamProviderOpenAI && input.ProviderType != UpstreamProviderClaude {
		return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_PROVIDER_TYPE", "上游类型必须是 openai 或 claude")
	}
	input.AuthType = strings.ToLower(strings.TrimSpace(input.AuthType))
	if input.AuthType != UpstreamAuthAPIKey && input.AuthType != UpstreamAuthWIF {
		return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_AUTH_TYPE", "认证方式必须是 api_key 或 wif")
	}
	baseURL, err := normalizeUpstreamBaseURL(input.BaseURL)
	if err != nil {
		return input, "", err
	}
	input.BaseURL = baseURL
	if input.Priority < 1 || input.Priority > 9999 || input.Weight < 1 || input.Weight > 10000 ||
		input.MaxConcurrency < 1 || input.MaxConcurrency > 10000 || input.RPMLimit < 1 || input.RPMLimit > 1000000 ||
		input.CircuitBreakerThreshold < 1 || input.CircuitBreakerThreshold > 1000 ||
		input.CircuitBreakerCooldownSeconds < 1 || input.CircuitBreakerCooldownSeconds > 86400 {
		return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_SCHEDULING_POLICY", "上游账号调度参数超出允许范围")
	}

	credential := strings.TrimSpace(input.APIKey)
	if input.AuthType == UpstreamAuthWIF {
		credential = strings.TrimSpace(input.WIFClientSecret)
		if input.ProviderType == UpstreamProviderOpenAI {
			input.BaseURL = "https://api.openai.com"
		} else {
			input.BaseURL = "https://api.anthropic.com"
		}
		input.WIFSubjectTokenURL = strings.TrimSpace(input.WIFSubjectTokenURL)
		input.WIFClientID = strings.TrimSpace(input.WIFClientID)
		input.WIFClientAuthMethod = strings.TrimSpace(input.WIFClientAuthMethod)
		input.WIFServiceAccountID = strings.TrimSpace(input.WIFServiceAccountID)
		if input.WIFClientAuthMethod != string(WIFClientSecretBasic) && input.WIFClientAuthMethod != string(WIFClientSecretPost) {
			return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_WIF_CONFIGURATION", "WIF 客户端认证方式无效")
		}
		normalizedTokenURL, tokenURLErr := validateAndNormalizeWIFSubjectTokenURL(input.WIFSubjectTokenURL)
		if tokenURLErr != nil {
			return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_WIF_CONFIGURATION", "外部 IdP Token URL 必须是安全的公网 HTTPS 地址")
		}
		input.WIFSubjectTokenURL = normalizedTokenURL
		if input.WIFClientID == "" || input.WIFServiceAccountID == "" {
			return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_WIF_CONFIGURATION", "WIF Client ID 和 Service Account ID 不能为空")
		}
		if input.ProviderType == UpstreamProviderOpenAI && strings.TrimSpace(input.WIFIdentityProviderID) == "" {
			return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_WIF_CONFIGURATION", "OpenAI WIF 必须填写 Identity Provider ID")
		}
		if input.ProviderType == UpstreamProviderClaude && (strings.TrimSpace(input.WIFFederationRuleID) == "" || strings.TrimSpace(input.WIFOrganizationID) == "") {
			return input, "", infraerrors.BadRequest("INVALID_UPSTREAM_WIF_CONFIGURATION", "Claude WIF 必须填写 Federation Rule ID 和 Organization ID")
		}
	} else {
		clearUpstreamWIFInput(&input)
	}
	if requireCredential && credential == "" {
		return input, "", upstreamCredentialRequired(input.AuthType)
	}
	return input, credential, nil
}

func normalizeUpstreamBaseURL(raw string) (string, error) {
	parsed, err := url.Parse(strings.TrimSpace(raw))
	if err != nil || parsed.Scheme != "https" || parsed.Host == "" || parsed.User != nil || parsed.RawQuery != "" || parsed.Fragment != "" {
		return "", infraerrors.BadRequest("INVALID_UPSTREAM_BASE_URL", "API 地址必须是无账号、查询参数和片段的 HTTPS 地址")
	}
	parsed.Path = strings.TrimRight(parsed.Path, "/")
	return parsed.String(), nil
}

func upstreamCredentialRequired(authType string) error {
	if authType == UpstreamAuthWIF {
		return infraerrors.BadRequest("UPSTREAM_WIF_CLIENT_SECRET_REQUIRED", "新建账号或变更身份联合边界时必须填写 WIF Client Secret")
	}
	return infraerrors.BadRequest("UPSTREAM_API_KEY_REQUIRED", "新建账号或变更认证边界时必须填写 API Key")
}

func upstreamCredentialBoundaryChanged(current *UpstreamAccount, next UpstreamAccountInput) bool {
	if current.ProviderType != next.ProviderType || current.AuthType != next.AuthType || upstreamOrigin(current.BaseURL) != upstreamOrigin(next.BaseURL) {
		return true
	}
	if next.AuthType != UpstreamAuthWIF {
		return false
	}
	return upstreamStringValue(current.WIFSubjectTokenURL) != next.WIFSubjectTokenURL ||
		upstreamStringValue(current.WIFClientID) != next.WIFClientID ||
		upstreamStringValue(current.WIFClientAuthMethod) != next.WIFClientAuthMethod
}

func upstreamOrigin(value string) string {
	parsed, err := url.Parse(value)
	if err != nil {
		return strings.ToLower(value)
	}
	return strings.ToLower(parsed.Scheme + "://" + parsed.Host)
}

func applyUpstreamWIFInput(account *UpstreamAccount, input UpstreamAccountInput) {
	if input.AuthType != UpstreamAuthWIF {
		account.WIFSubjectTokenURL, account.WIFClientID, account.WIFClientAuthMethod = nil, nil, nil
		account.WIFAudience, account.WIFScope, account.WIFIdentityProviderID = nil, nil, nil
		account.WIFServiceAccountID, account.WIFFederationRuleID = nil, nil
		account.WIFOrganizationID, account.WIFWorkspaceID = nil, nil
		return
	}
	account.WIFSubjectTokenURL = optionalString(input.WIFSubjectTokenURL)
	account.WIFClientID = optionalString(input.WIFClientID)
	account.WIFClientAuthMethod = optionalString(input.WIFClientAuthMethod)
	account.WIFAudience = optionalString(input.WIFAudience)
	account.WIFScope = optionalString(input.WIFScope)
	account.WIFIdentityProviderID = optionalString(input.WIFIdentityProviderID)
	account.WIFServiceAccountID = optionalString(input.WIFServiceAccountID)
	account.WIFFederationRuleID = optionalString(input.WIFFederationRuleID)
	account.WIFOrganizationID = optionalString(input.WIFOrganizationID)
	account.WIFWorkspaceID = optionalString(input.WIFWorkspaceID)
}

func upstreamInputFromAccount(account *UpstreamAccount) UpstreamAccountInput {
	return UpstreamAccountInput{
		Name: account.Name, ProviderType: account.ProviderType, BaseURL: account.BaseURL, AuthType: account.AuthType,
		WIFSubjectTokenURL: upstreamStringValue(account.WIFSubjectTokenURL), WIFClientID: upstreamStringValue(account.WIFClientID),
		WIFClientAuthMethod: upstreamStringValue(account.WIFClientAuthMethod), WIFAudience: upstreamStringValue(account.WIFAudience),
		WIFScope: upstreamStringValue(account.WIFScope), WIFIdentityProviderID: upstreamStringValue(account.WIFIdentityProviderID),
		WIFServiceAccountID: upstreamStringValue(account.WIFServiceAccountID), WIFFederationRuleID: upstreamStringValue(account.WIFFederationRuleID),
		WIFOrganizationID: upstreamStringValue(account.WIFOrganizationID), WIFWorkspaceID: upstreamStringValue(account.WIFWorkspaceID),
		IsActive: account.IsActive, Priority: account.Priority, Weight: account.Weight, MaxConcurrency: account.MaxConcurrency,
		RPMLimit: account.RPMLimit, CircuitBreakerThreshold: account.CircuitBreakerThreshold,
		CircuitBreakerCooldownSeconds: account.CircuitBreakerCooldownSeconds,
	}
}

func clearUpstreamWIFInput(input *UpstreamAccountInput) {
	input.WIFClientSecret, input.WIFSubjectTokenURL, input.WIFClientID, input.WIFClientAuthMethod = "", "", "", ""
	input.WIFAudience, input.WIFScope, input.WIFIdentityProviderID, input.WIFServiceAccountID = "", "", "", ""
	input.WIFFederationRuleID, input.WIFOrganizationID, input.WIFWorkspaceID = "", "", ""
}

func upstreamWIFPlatform(providerType string) string {
	if providerType == UpstreamProviderClaude {
		return PlatformAnthropic
	}
	return PlatformOpenAI
}

func upstreamModelsURL(baseURL string) string {
	baseURL = strings.TrimRight(baseURL, "/")
	if strings.HasSuffix(strings.ToLower(baseURL), "/v1") {
		return baseURL + "/models"
	}
	return baseURL + "/v1/models"
}

func upstreamConnectionFailure(code, message string, started time.Time, status *int) *UpstreamConnectionTestResult {
	return &UpstreamConnectionTestResult{Success: false, Code: code, Message: message, LatencyMS: time.Since(started).Milliseconds(), StatusCode: status}
}

func upstreamHTTPFailure(status int, started time.Time) *UpstreamConnectionTestResult {
	message := "上游连接测试失败。"
	code := "upstream_http_error"
	switch status {
	case http.StatusUnauthorized, http.StatusForbidden:
		code, message = "upstream_authentication_failed", "上游认证失败，请检查凭据。"
	case http.StatusTooManyRequests:
		code, message = "upstream_rate_limited", "上游触发限流，请稍后重试。"
	default:
		if status >= 500 {
			code, message = "upstream_unavailable", "上游服务暂时不可用。"
		}
	}
	return upstreamConnectionFailure(code, message, started, &status)
}

func optionalString(value string) *string {
	value = strings.TrimSpace(value)
	if value == "" {
		return nil
	}
	return &value
}

func upstreamStringValue(value *string) string {
	if value == nil {
		return ""
	}
	return *value
}
