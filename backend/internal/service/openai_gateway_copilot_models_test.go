package service

import (
	"context"
	"errors"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
	"github.com/tidwall/gjson"
)

func TestFetchGitHubCopilotModelsRequestsLiveRootModelsAndRewritesClaudeIDs(t *testing.T) {
	upstream := &httpUpstreamRecorder{resp: &http.Response{
		StatusCode: http.StatusOK,
		Header:     http.Header{"Content-Type": []string{"application/json"}},
		Body: io.NopCloser(strings.NewReader(`{"object":"list","data":[` +
			`{"id":"claude-sonnet-4.5","object":"model"},` +
			`{"id":"claude-opus-4.6","object":"model"},` +
			`{"id":"gpt-4.1","object":"model"}]}`)),
	}}
	svc := newCopilotGatewayTestService(upstream)
	account := newCopilotGatewayTestAccount()

	body, err := svc.FetchGitHubCopilotModels(context.Background(), account)

	require.NoError(t, err)
	require.NotNil(t, upstream.lastReq)
	require.Equal(t, http.MethodGet, upstream.lastReq.Method)
	require.Equal(t, "/models", upstream.lastReq.URL.Path)
	require.NotEqual(t, "/v1/models", upstream.lastReq.URL.Path)
	require.Equal(t, "Bearer copilot-token", upstream.lastReq.Header.Get("Authorization"))
	require.Equal(t, "claude-sonnet-4-5", gjson.GetBytes(body, "data.0.id").String())
	require.Equal(t, "claude-opus-4-6", gjson.GetBytes(body, "data.1.id").String())
	require.Equal(t, "gpt-4.1", gjson.GetBytes(body, "data.2.id").String())
}

func TestFetchGitHubCopilotModelsReturnsUpstreamErrorWithoutStaticFallback(t *testing.T) {
	upstream := &httpUpstreamRecorder{resp: &http.Response{
		StatusCode: http.StatusServiceUnavailable,
		Header:     http.Header{"Content-Type": []string{"application/json"}},
		Body:       io.NopCloser(strings.NewReader(`{"error":{"message":"temporarily unavailable"}}`)),
	}}
	svc := newCopilotGatewayTestService(upstream)

	body, err := svc.FetchGitHubCopilotModels(context.Background(), newCopilotGatewayTestAccount())

	require.Error(t, err)
	require.Nil(t, body)
	require.Contains(t, err.Error(), "503")
}

func TestFetchGitHubCopilotModelsReturnsTransportError(t *testing.T) {
	upstream := &httpUpstreamRecorder{err: errors.New("models transport failed")}
	svc := newCopilotGatewayTestService(upstream)

	body, err := svc.FetchGitHubCopilotModels(context.Background(), newCopilotGatewayTestAccount())

	require.Error(t, err)
	require.Nil(t, body)
	require.Contains(t, err.Error(), "models transport failed")
}

func TestOpenAIGatewayGenerateSessionHashIsEmptyForCopilotOnlyContext(t *testing.T) {
	gin.SetMode(gin.TestMode)
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	request := httptest.NewRequest(http.MethodPost, "/copilot/v1/messages", nil)
	request.Header.Set("session_id", "must-not-create-sticky-binding")
	c.Request = request.WithContext(WithGitHubCopilotOnly(request.Context()))
	body := []byte(`{"model":"claude-sonnet-4-5","messages":[{"role":"user","content":"hello"}]}`)

	hash := (&OpenAIGatewayService{}).GenerateSessionHash(c, body)

	require.Empty(t, hash)
	require.Empty(t, openAILegacySessionHashFromContext(c.Request.Context()))
}
