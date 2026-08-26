//go:build unit

package service

import (
	"context"
	"errors"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func TestCopilotHTTPResponsesPreserveStatusAndBodyWithoutFailover(t *testing.T) {
	tests := []struct {
		name             string
		statusCode       int
		body             string
		wantInvalidation int
	}{
		{
			name:             "final 401 invalidates token and returns upstream response",
			statusCode:       http.StatusUnauthorized,
			body:             `{"error":{"code":"invalid_token","message":"expired"}}`,
			wantInvalidation: 1,
		},
		{
			name:       "non-quota 402 is returned without switching account",
			statusCode: http.StatusPaymentRequired,
			body:       `{"error":{"code":"billing_issue","message":"payment required"}}`,
		},
		{
			name:       "429 is returned without switching account",
			statusCode: http.StatusTooManyRequests,
			body:       `{"error":{"code":"rate_limited","message":"slow down"}}`,
		},
		{
			name:       "5xx is returned without switching account",
			statusCode: http.StatusServiceUnavailable,
			body:       `{"error":{"code":"server_error","message":"temporarily unavailable"}}`,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			repo := &rateLimitAccountRepoStub{}
			invalidator := &tokenCacheInvalidatorRecorder{}
			rateLimits := NewRateLimitService(repo, nil, &config.Config{}, nil, nil)
			rateLimits.SetTokenCacheInvalidator(invalidator)
			gateway := &OpenAIGatewayService{rateLimitService: rateLimits}
			account := newCopilotGatewayTestAccount()
			account.Status = StatusActive

			gin.SetMode(gin.TestMode)
			recorder := httptest.NewRecorder()
			c, _ := gin.CreateTestContext(recorder)
			c.Request = httptest.NewRequest(http.MethodPost, "/copilot/v1/chat/completions", nil)
			resp := &http.Response{
				StatusCode: tt.statusCode,
				Header:     http.Header{"Content-Type": []string{"application/json"}},
				Body:       io.NopCloser(strings.NewReader(tt.body)),
			}

			responseBody, upstreamMsg := gateway.readOpenAIUpstreamError(resp)
			failoverErr := gateway.failoverOpenAIUpstreamHTTPError(
				context.Background(), c, account, resp, responseBody, upstreamMsg, "gpt-4.1",
			)
			require.Nil(t, failoverErr)

			result, err := gateway.handleChatCompletionsErrorResponse(resp, c, account, "gpt-4.1")
			require.Nil(t, result)
			require.Error(t, err)
			var gotFailover *UpstreamFailoverError
			require.False(t, errors.As(err, &gotFailover))
			var statusErr *UpstreamHTTPStatusError
			require.True(t, errors.As(err, &statusErr))
			require.Equal(t, GatewayFailureScopeRequest, statusErr.Scope)
			require.Equal(t, tt.statusCode, statusErr.StatusCode)

			require.Equal(t, tt.statusCode, recorder.Code)
			require.Equal(t, tt.body, recorder.Body.String())
			require.Contains(t, recorder.Header().Get("Content-Type"), "application/json")
			require.Equal(t, tt.wantInvalidation, len(invalidator.accounts))
			require.Zero(t, repo.setErrorCalls)
			require.Zero(t, repo.tempCalls)
			require.Equal(t, StatusActive, account.Status)
			require.False(t, gateway.isOpenAIAccountRuntimeBlocked(account))
		})
	}
}

func TestCopilotQuotaExceededIsTheOnlyHTTPFailover(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)

	repo := &rateLimitAccountRepoStub{}
	rateLimits := NewRateLimitService(repo, nil, &config.Config{}, nil, nil)
	gateway := &OpenAIGatewayService{rateLimitService: rateLimits}
	account := newCopilotGatewayTestAccount()
	body := `{"error":{"code":"quota_exceeded","message":"monthly quota exhausted"}}`

	gin.SetMode(gin.TestMode)
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	c.Request = httptest.NewRequest(http.MethodPost, "/copilot/v1/chat/completions", nil)
	resp := &http.Response{
		StatusCode: http.StatusPaymentRequired,
		Header:     http.Header{"Content-Type": []string{"application/json"}},
		Body:       io.NopCloser(strings.NewReader(body)),
	}
	responseBody, upstreamMsg := gateway.readOpenAIUpstreamError(resp)

	failoverErr := gateway.failoverOpenAIUpstreamHTTPError(
		context.Background(), c, account, resp, responseBody, upstreamMsg, "gpt-4.1",
	)

	require.NotNil(t, failoverErr)
	require.Equal(t, http.StatusPaymentRequired, failoverErr.StatusCode)
	require.Equal(t, body, string(failoverErr.ResponseBody))
	require.False(t, failoverErr.RetryableOnSameAccount)
	require.Zero(t, recorder.Body.Len(), "quota response must not be committed before account failover")
	require.Zero(t, repo.setErrorCalls)
	require.Equal(t, 1, repo.tempCalls)
	require.Equal(t, CopilotMonthlyQuotaExceededReason, repo.lastTempReason)
	require.NotNil(t, account.TempUnschedulableUntil)
	require.Equal(t, CopilotMonthlyQuotaExceededReason, account.TempUnschedulableReason)
}

func TestHandleOpenAIAccountUpstreamError_NilAccountDoesNotPanic(t *testing.T) {
	gateway := &OpenAIGatewayService{}

	require.False(t, gateway.handleOpenAIAccountUpstreamError(
		context.Background(),
		nil,
		http.StatusUnauthorized,
		http.Header{},
		[]byte(`{"error":{"code":"invalid_token"}}`),
	))
}
