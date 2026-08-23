package requestaudit

import "time"

const (
	CaptureModeAll    = "all"
	CaptureModeErrors = "errors"
	CaptureModeSample = "sample"

	RedactionStandard = "standard"
	RedactionStrict   = "strict"

	minBodyBytes     = 4 * 1024
	maxBodyBytes     = 4 * 1024 * 1024
	defaultQueueSize = 2048
)

// Policy is the singleton deployment-level request audit policy. ParaGateway
// deployments currently represent one enterprise boundary, so no tenant ID is
// attached until the product introduces first-class multi-tenancy.
type Policy struct {
	ID                    int16     `json:"id"`
	Enabled               bool      `json:"enabled"`
	CaptureMode           string    `json:"capture_mode"`
	SampleRate            float64   `json:"sample_rate"`
	RetentionDays         int       `json:"retention_days"`
	CaptureRequestBody    bool      `json:"capture_request_body"`
	CaptureResponseBody   bool      `json:"capture_response_body"`
	StoreEncryptedContent bool      `json:"store_encrypted_content"`
	RedactionLevel        string    `json:"redaction_level"`
	MaxBodyBytes          int       `json:"max_body_bytes"`
	Version               int64     `json:"version"`
	UpdatedBy             *int64    `json:"updated_by,omitempty"`
	UpdatedAt             time.Time `json:"updated_at"`
	EncryptionConfigured  bool      `json:"encryption_configured"`
}

type UpdatePolicyRequest struct {
	Enabled               bool    `json:"enabled"`
	CaptureMode           string  `json:"capture_mode"`
	SampleRate            float64 `json:"sample_rate"`
	RetentionDays         int     `json:"retention_days"`
	CaptureRequestBody    bool    `json:"capture_request_body"`
	CaptureResponseBody   bool    `json:"capture_response_body"`
	StoreEncryptedContent bool    `json:"store_encrypted_content"`
	RedactionLevel        string  `json:"redaction_level"`
	MaxBodyBytes          int     `json:"max_body_bytes"`
	ExpectedVersion       int64   `json:"expected_version"`
}

type Identity struct {
	UserID     int64
	Username   string
	UserEmail  string
	APIKeyID   int64
	APIKeyName string
	GroupID    *int64
	GroupName  string
	ClientIP   string
}

type Record struct {
	ID                     int64     `json:"id"`
	RequestID              string    `json:"request_id"`
	UserID                 *int64    `json:"user_id,omitempty"`
	UsernameSnapshot       string    `json:"username"`
	UserEmailSnapshot      string    `json:"user_email"`
	APIKeyID               *int64    `json:"api_key_id,omitempty"`
	APIKeyNameSnapshot     string    `json:"api_key_name"`
	GroupID                *int64    `json:"group_id,omitempty"`
	GroupNameSnapshot      string    `json:"group_name"`
	Method                 string    `json:"method"`
	Endpoint               string    `json:"endpoint"`
	Model                  string    `json:"model"`
	ClientIP               string    `json:"client_ip"`
	StatusCode             int       `json:"status_code"`
	LatencyMS              int64     `json:"latency_ms"`
	IsStream               bool      `json:"is_stream"`
	CaptureReason          string    `json:"capture_reason"`
	PolicyVersion          int64     `json:"policy_version"`
	RequestContentType     string    `json:"request_content_type"`
	ResponseContentType    string    `json:"response_content_type"`
	RequestPreview         string    `json:"request_preview"`
	ResponsePreview        string    `json:"response_preview"`
	RequestBodyCiphertext  string    `json:"-"`
	ResponseBodyCiphertext string    `json:"-"`
	EncryptionVersion      string    `json:"encryption_version"`
	RequestBytes           int64     `json:"request_bytes"`
	ResponseBytes          int64     `json:"response_bytes"`
	RequestTruncated       bool      `json:"request_truncated"`
	ResponseTruncated      bool      `json:"response_truncated"`
	RequestBodyOmitted     bool      `json:"request_body_omitted"`
	ResponseBodyOmitted    bool      `json:"response_body_omitted"`
	ContentError           string    `json:"content_error,omitempty"`
	ExpiresAt              time.Time `json:"expires_at"`
	CreatedAt              time.Time `json:"created_at"`
	RawContentAvailable    bool      `json:"raw_content_available"`
}

type Content struct {
	RecordID          int64  `json:"record_id"`
	RequestBody       string `json:"request_body"`
	ResponseBody      string `json:"response_body"`
	RequestAvailable  bool   `json:"request_available"`
	ResponseAvailable bool   `json:"response_available"`
}

type Filter struct {
	UserID     *int64
	APIKeyID   *int64
	GroupID    *int64
	StatusCode *int
	RequestID  string
	Model      string
	Query      string
	StartAt    *time.Time
	EndAt      *time.Time
}

type Page struct {
	Items    []*Record `json:"items"`
	Total    int64     `json:"total"`
	Page     int       `json:"page"`
	PageSize int       `json:"page_size"`
	Pages    int       `json:"pages"`
}

type Runtime struct {
	Enabled          bool      `json:"enabled"`
	QueueDepth       int       `json:"queue_depth"`
	QueueCapacity    int       `json:"queue_capacity"`
	EnqueuedTotal    int64     `json:"enqueued_total"`
	PersistedTotal   int64     `json:"persisted_total"`
	DroppedTotal     int64     `json:"dropped_total"`
	FailedTotal      int64     `json:"failed_total"`
	LastPersistedAt  time.Time `json:"last_persisted_at,omitempty"`
	LastCleanupAt    time.Time `json:"last_cleanup_at,omitempty"`
	LastCleanupCount int64     `json:"last_cleanup_count"`
}
