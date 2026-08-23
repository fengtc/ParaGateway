package requestaudit

import (
	"context"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/Wei-Shaw/sub2api/internal/pkg/ctxkey"
	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"github.com/Wei-Shaw/sub2api/internal/repository"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func validPolicyRequest() UpdatePolicyRequest {
	return UpdatePolicyRequest{
		Enabled: true, CaptureMode: CaptureModeAll, SampleRate: 100,
		RetentionDays: 30, CaptureRequestBody: true, CaptureResponseBody: true,
		RedactionLevel: RedactionStandard, MaxBodyBytes: minBodyBytes, ExpectedVersion: 1,
	}
}

func TestValidatePolicyBoundariesAndPersistentEncryptionGate(t *testing.T) {
	svc := &Service{}
	require.NoError(t, svc.validatePolicy(validPolicyRequest()))

	encrypted := validPolicyRequest()
	encrypted.Enabled = false
	encrypted.StoreEncryptedContent = true
	err := svc.validatePolicy(encrypted)
	require.Equal(t, "request_audit_encryption_key_required", infraerrors.Reason(err))

	tests := []struct {
		name string
		edit func(*UpdatePolicyRequest)
		code string
	}{
		{"version", func(v *UpdatePolicyRequest) { v.ExpectedVersion = 0 }, "request_audit_version_required"},
		{"mode", func(v *UpdatePolicyRequest) { v.CaptureMode = "random" }, "request_audit_invalid_capture_mode"},
		{"sample", func(v *UpdatePolicyRequest) { v.SampleRate = 100.1 }, "request_audit_invalid_sample_rate"},
		{"retention", func(v *UpdatePolicyRequest) { v.RetentionDays = 3651 }, "request_audit_invalid_retention"},
		{"redaction", func(v *UpdatePolicyRequest) { v.RedactionLevel = "none" }, "request_audit_invalid_redaction"},
		{"body limit", func(v *UpdatePolicyRequest) { v.MaxBodyBytes = minBodyBytes - 1 }, "request_audit_invalid_body_limit"},
	}
	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			request := validPolicyRequest()
			tc.edit(&request)
			require.Equal(t, tc.code, infraerrors.Reason(svc.validatePolicy(request)))
		})
	}
}

func TestBuildPreviewStandardStrictBinaryAndBounded(t *testing.T) {
	body := []byte(`{"model":"gpt-5.4","messages":[{"role":"user","content":"quarterly plan"}],"api_key":"sk-private-canary"}`)
	standard := buildPreview(body, "application/json", RedactionStandard)
	require.Contains(t, standard, "quarterly plan")
	require.NotContains(t, standard, "sk-private-canary")
	require.LessOrEqual(t, len(standard), maxPreviewBytes)

	require.Equal(t, "<content hidden by strict redaction>", buildPreview(body, "application/json", RedactionStrict))
	require.Equal(t, "<binary or multipart content omitted>", buildPreview([]byte{0, 1, 2}, "image/png", RedactionStandard))
	require.True(t, textualContent("text/event-stream; charset=utf-8", []byte("data: ok")))
	require.False(t, textualContent("multipart/form-data", []byte("file")))

	model, stream := extractModelAndStream([]byte(`{"model":" gpt-5.4 ","stream":true}`))
	require.Equal(t, "gpt-5.4", model)
	require.True(t, stream)
}

func captureTestService(policy Policy) *Service {
	svc := &Service{queue: make(chan *captureEnvelope, 8)}
	svc.policy.Store(&policy)
	return svc
}

func serveCapturedRequest(t *testing.T, svc *Service, status int, requestBody, responseBody string) *captureEnvelope {
	t.Helper()
	gin.SetMode(gin.TestMode)
	router := gin.New()
	router.Use(svc.Middleware(func(*gin.Context) Identity {
		groupID := int64(7)
		return Identity{
			UserID: 11, Username: "alice", UserEmail: "alice@example.com",
			APIKeyID: 13, APIKeyName: "desktop", GroupID: &groupID, GroupName: "engineering",
			ClientIP: "192.0.2.10",
		}
	}))
	router.POST("/v1/messages", func(c *gin.Context) {
		ctx := context.WithValue(c.Request.Context(), ctxkey.RequestID, "request-final")
		c.Request = c.Request.WithContext(ctx)
		c.Data(status, "application/json", []byte(responseBody))
	})

	request := httptest.NewRequest(http.MethodPost, "/v1/messages", strings.NewReader(requestBody))
	request.Header.Set("Content-Type", "application/json")
	request = request.WithContext(context.WithValue(request.Context(), ctxkey.RequestID, "request-initial"))
	recorder := httptest.NewRecorder()
	router.ServeHTTP(recorder, request)

	select {
	case envelope := <-svc.queue:
		return envelope
	default:
		return nil
	}
}

func TestMiddlewareCapturesAuthenticatedGatewayTrafficAndFinalRequestID(t *testing.T) {
	policy := Policy{
		Enabled: true, CaptureMode: CaptureModeAll, RetentionDays: 30,
		CaptureRequestBody: true, CaptureResponseBody: true,
		RedactionLevel: RedactionStandard, MaxBodyBytes: 4096, Version: 4,
	}
	svc := captureTestService(policy)
	envelope := serveCapturedRequest(t, svc, http.StatusCreated,
		`{"model":"gpt-5.4","stream":true,"messages":[]}`,
		`{"id":"response-1"}`)
	require.NotNil(t, envelope)
	require.Equal(t, "request-final", envelope.record.RequestID)
	require.Equal(t, int64(11), *envelope.record.UserID)
	require.Equal(t, int64(13), *envelope.record.APIKeyID)
	require.Equal(t, "engineering", envelope.record.GroupNameSnapshot)
	require.Equal(t, "gpt-5.4", envelope.record.Model)
	require.True(t, envelope.record.IsStream)
	require.Equal(t, http.StatusCreated, envelope.record.StatusCode)
	require.Equal(t, int64(4), envelope.record.PolicyVersion)
}

func TestMiddlewareHonorsErrorAndSampleModes(t *testing.T) {
	base := Policy{
		Enabled: true, CaptureMode: CaptureModeErrors, RetentionDays: 30,
		CaptureRequestBody: true, CaptureResponseBody: true,
		RedactionLevel: RedactionStandard, MaxBodyBytes: 4096, Version: 1,
	}
	require.Nil(t, serveCapturedRequest(t, captureTestService(base), http.StatusOK, `{}`, `{}`))

	errorEnvelope := serveCapturedRequest(t, captureTestService(base), http.StatusBadGateway, `{}`, `{"error":"upstream"}`)
	require.NotNil(t, errorEnvelope)
	require.Equal(t, "error", errorEnvelope.record.CaptureReason)

	base.CaptureMode = CaptureModeSample
	base.SampleRate = 0
	require.Nil(t, serveCapturedRequest(t, captureTestService(base), http.StatusOK, `{}`, `{}`))
	base.SampleRate = 100
	require.NotNil(t, serveCapturedRequest(t, captureTestService(base), http.StatusOK, `{}`, `{}`))
}

func TestMiddlewareBoundsBodiesWithoutChangingForwardedPayload(t *testing.T) {
	policy := Policy{
		Enabled: true, CaptureMode: CaptureModeAll, RetentionDays: 30,
		CaptureRequestBody: true, CaptureResponseBody: true,
		RedactionLevel: RedactionStandard, MaxBodyBytes: 8, Version: 1,
	}
	requestBody := `{"model":"gpt-5.4","messages":["long request"]}`
	responseBody := `{"result":"long response"}`
	envelope := serveCapturedRequest(t, captureTestService(policy), http.StatusOK, requestBody, responseBody)
	require.NotNil(t, envelope)
	require.Len(t, envelope.requestBody, 8)
	require.Len(t, envelope.responseBody, 8)
	require.True(t, envelope.record.RequestTruncated)
	require.True(t, envelope.record.ResponseTruncated)
	require.Equal(t, int64(len(requestBody)), envelope.record.RequestBytes)
	require.Equal(t, int64(len(responseBody)), envelope.record.ResponseBytes)
}

func TestPrepareBodyOmitsBinaryAndEncryptsTextWithAES256GCM(t *testing.T) {
	cfg := &config.Config{Totp: config.TotpConfig{
		EncryptionKey: strings.Repeat("01", 32), EncryptionKeyConfigured: true,
	}}
	encryptor, err := repository.NewAESEncryptor(cfg)
	require.NoError(t, err)
	svc := &Service{encryptor: encryptor, encryptionConfigured: true}
	policy := Policy{StoreEncryptedContent: true, RedactionLevel: RedactionStandard}

	binary := &Record{}
	svc.prepareBody(binary, true, []byte{0, 1, 2, 3}, "image/png", policy)
	require.True(t, binary.RequestBodyOmitted)
	require.Empty(t, binary.RequestBodyCiphertext)

	plaintext := `{"model":"gpt-5.4","messages":[{"content":"confidential"}]}`
	item := &Record{}
	svc.prepareBody(item, true, []byte(plaintext), "application/json", policy)
	require.Equal(t, encryptionVersionAES256GCM, item.EncryptionVersion)
	require.NotEmpty(t, item.RequestBodyCiphertext)
	require.NotContains(t, item.RequestBodyCiphertext, "confidential")
	decrypted, err := encryptor.Decrypt(item.RequestBodyCiphertext)
	require.NoError(t, err)
	require.Equal(t, plaintext, decrypted)
}

func TestGetContentDecryptsBothBodies(t *testing.T) {
	cfg := &config.Config{Totp: config.TotpConfig{
		EncryptionKey: strings.Repeat("02", 32), EncryptionKeyConfigured: true,
	}}
	encryptor, err := repository.NewAESEncryptor(cfg)
	require.NoError(t, err)
	requestCipher, err := encryptor.Encrypt(`{"request":"secret"}`)
	require.NoError(t, err)
	responseCipher, err := encryptor.Encrypt(`{"response":"secret"}`)
	require.NoError(t, err)

	db, mock, err := sqlmock.New()
	require.NoError(t, err)
	defer db.Close()
	now := time.Now().UTC()
	columns := []string{
		"id", "request_id", "user_id", "username_snapshot", "user_email_snapshot",
		"api_key_id", "api_key_name_snapshot", "group_id", "group_name_snapshot",
		"method", "endpoint", "model", "client_ip", "status_code", "latency_ms",
		"is_stream", "capture_reason", "policy_version", "request_content_type", "response_content_type",
		"request_preview", "response_preview", "encryption_version", "request_bytes", "response_bytes",
		"request_truncated", "response_truncated", "request_body_omitted", "response_body_omitted",
		"content_error", "expires_at", "created_at", "raw_content_available",
		"request_body_ciphertext", "response_body_ciphertext",
	}
	mock.ExpectQuery(`(?s)SELECT .*FROM request_audit_records r WHERE r.id=\$1`).
		WithArgs(int64(42)).
		WillReturnRows(sqlmock.NewRows(columns).AddRow(
			int64(42), "req-42", nil, "", "", nil, "", nil, "",
			"POST", "/v1/messages", "gpt-5.4", "", 200, int64(10),
			false, "all", int64(1), "application/json", "application/json",
			"", "", encryptionVersionAES256GCM, int64(20), int64(21),
			false, false, false, false, "", now.Add(24*time.Hour), now, true,
			requestCipher, responseCipher,
		))

	svc := &Service{repo: NewRepository(db), encryptor: encryptor, encryptionConfigured: true}
	content, err := svc.GetContent(context.Background(), 42)
	require.NoError(t, err)
	require.True(t, content.RequestAvailable)
	require.True(t, content.ResponseAvailable)
	require.JSONEq(t, `{"request":"secret"}`, content.RequestBody)
	require.JSONEq(t, `{"response":"secret"}`, content.ResponseBody)
	require.NoError(t, mock.ExpectationsWereMet())
}
