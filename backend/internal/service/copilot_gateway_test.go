package service

import (
	"bytes"
	"context"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/config"
	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
	"github.com/tidwall/gjson"
)

func newCopilotGatewayTestAccount() *Account {
	return &Account{
		ID:          901,
		Name:        "github-copilot",
		Platform:    PlatformOpenAI,
		Type:        AccountTypeOAuth,
		Status:      StatusActive,
		Schedulable: true,
		Concurrency: 1,
		Credentials: map[string]any{
			"oauth_profile":       CopilotOAuthProfile,
			"access_token":        "copilot-token",
			"github_access_token": "github-token",
			"base_url":            CopilotAPIBaseURL,
		},
	}
}

func newCopilotGatewayTestService(upstream HTTPUpstream) *OpenAIGatewayService {
	return &OpenAIGatewayService{
		cfg: &config.Config{Security: config.SecurityConfig{URLAllowlist: config.URLAllowlistConfig{
			Enabled: false,
		}}},
		httpUpstream: upstream,
	}
}

func TestCopilotEmptyModelMappingAllowsMultiVendorCatalog(t *testing.T) {
	account := newCopilotGatewayTestAccount()
	require.True(t, account.IsModelSupported("gpt-4.1"))
	require.True(t, account.IsModelSupported("claude-sonnet-4-6"))
	require.True(t, account.IsModelSupported("gemini-2.0-flash-001"))

	account.Credentials["model_mapping"] = map[string]any{"gpt-4.1": "gpt-4.1"}
	require.True(t, account.IsModelSupported("gpt-4.1"))
	require.False(t, account.IsModelSupported("gemini-2.0-flash-001"))
}

func TestBuildCopilotAPIURLUsesRootEndpoints(t *testing.T) {
	require.Equal(t, "https://api.githubcopilot.com/chat/completions", buildCopilotAPIURL(CopilotAPIBaseURL, "/chat/completions"))
	require.Equal(t, "https://api.githubcopilot.com/responses", buildCopilotAPIURL(CopilotAPIBaseURL, "/responses"))
	require.Equal(t, "https://api.githubcopilot.com/models", buildCopilotAPIURL(CopilotAPIBaseURL, "/models"))
	require.Equal(t, "https://proxy.example/copilot/responses?tenant=1", buildCopilotAPIURL("https://proxy.example/copilot?tenant=1", "/responses"))
}

func TestFetchCodexModelsManifestForCopilotIsLocal(t *testing.T) {
	manifest, err := (&OpenAIGatewayService{}).FetchCodexModelsManifest(
		context.Background(),
		newCopilotGatewayTestAccount(),
		"0.150.0",
		"",
	)
	require.NoError(t, err)
	require.NotNil(t, manifest)
	require.NotEmpty(t, manifest.ETag)
	require.Equal(t, "gemini-2.0-flash-001", gjson.GetBytes(manifest.Body, "models.#(slug==\"gemini-2.0-flash-001\").slug").String())

	notModified, err := (&OpenAIGatewayService{}).FetchCodexModelsManifest(
		context.Background(),
		newCopilotGatewayTestAccount(),
		"0.150.0",
		manifest.ETag,
	)
	require.NoError(t, err)
	require.True(t, notModified.NotModified)
	require.Empty(t, notModified.Body)
}

func TestCopilotCountTokensReturnsNotImplementedWithoutUpstream(t *testing.T) {
	gin.SetMode(gin.TestMode)
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	body := []byte(`{"model":"claude-sonnet-4-6","messages":[{"role":"user","content":"hello"}]}`)
	c.Request = httptest.NewRequest(http.MethodPost, "/v1/messages/count_tokens", bytes.NewReader(body))

	err := (&OpenAIGatewayService{}).ForwardCountTokensAsAnthropic(context.Background(), c, newCopilotGatewayTestAccount(), body, "")
	require.Error(t, err)
	require.Equal(t, http.StatusNotImplemented, infraerrors.Code(err))
	require.Equal(t, http.StatusNotImplemented, recorder.Code)
	require.Equal(t, "not_supported_error", gjson.Get(recorder.Body.String(), "error.type").String())
	require.Contains(t, recorder.Body.String(), "GitHub Copilot")
}

func TestCopilotLiveAndCompactAreRejectedBeforeNetwork(t *testing.T) {
	account := newCopilotGatewayTestAccount()
	service := &OpenAIGatewayService{}

	_, err := service.createUpstreamLiveCall(context.Background(), account, &LiveCallRequest{}, "attestation")
	require.Error(t, err)
	require.Equal(t, http.StatusNotImplemented, infraerrors.Code(err))

	gin.SetMode(gin.TestMode)
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	c.Request = httptest.NewRequest(http.MethodPost, "/v1/responses/compact", strings.NewReader(`{"model":"gpt-4.1","input":"hello"}`))
	_, err = service.Forward(context.Background(), c, account, []byte(`{"model":"gpt-4.1","input":"hello"}`))
	require.Error(t, err)
	require.Equal(t, http.StatusNotImplemented, infraerrors.Code(err))
}

func TestCopilotModelSyncRequestUsesRootModelsEndpoint(t *testing.T) {
	request, err := (&AccountTestService{
		cfg: newCopilotGatewayTestService(nil).cfg,
	}).buildOpenAIUpstreamModelsRequest(context.Background(), newCopilotGatewayTestAccount())
	require.NoError(t, err)
	require.Equal(t, "https://api.githubcopilot.com/models", request.URL.String())
	require.Equal(t, "Bearer copilot-token", request.Header.Get("Authorization"))
	require.Equal(t, "vscode-chat", request.Header.Get("Copilot-Integration-Id"))
}

func TestCopilotChatUnsupportedAPIRetriesResponsesOnce(t *testing.T) {
	gin.SetMode(gin.TestMode)
	responsesSSE := strings.Join([]string{
		`data: {"type":"response.output_text.delta","delta":"ok"}`,
		``,
		`data: {"type":"response.completed","response":{"id":"resp_copilot","object":"response","model":"claude-sonnet-4.6","status":"completed","output":[{"type":"message","id":"msg_1","role":"assistant","status":"completed","content":[{"type":"output_text","text":"ok"}]}],"usage":{"input_tokens":5,"output_tokens":2,"total_tokens":7}}}`,
		``,
		`data: [DONE]`,
		``,
	}, "\n")
	upstream := &httpUpstreamRecorder{responses: []*http.Response{
		{
			StatusCode: http.StatusBadRequest,
			Header:     http.Header{"Content-Type": []string{"application/json"}},
			Body:       io.NopCloser(strings.NewReader(`{"error":{"code":"unsupported_api_for_model","message":"model does not support chat completions"}}`)),
		},
		{
			StatusCode: http.StatusOK,
			Header:     http.Header{"Content-Type": []string{"text/event-stream"}},
			Body:       io.NopCloser(strings.NewReader(responsesSSE)),
		},
	}}
	service := newCopilotGatewayTestService(upstream)
	account := newCopilotGatewayTestAccount()
	body := []byte(`{"model":"claude-sonnet-4-6","messages":[{"role":"system","content":"be concise"},{"role":"user","content":"hello"}],"stream":false}`)
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	c.Request = httptest.NewRequest(http.MethodPost, "/v1/chat/completions", bytes.NewReader(body))

	result, err := service.ForwardAsChatCompletions(context.Background(), c, account, body, "", "")
	require.NoError(t, err)
	require.NotNil(t, result)
	require.Len(t, upstream.requests, 2)
	require.Equal(t, "https://api.githubcopilot.com/chat/completions", upstream.requests[0].URL.String())
	require.Equal(t, "https://api.githubcopilot.com/responses", upstream.requests[1].URL.String())
	require.Equal(t, "claude-sonnet-4.6", gjson.GetBytes(upstream.bodies[1], "model").String())
	require.Equal(t, "hello", gjson.GetBytes(upstream.bodies[1], "input").String())
	require.Equal(t, "be concise", gjson.GetBytes(upstream.bodies[1], "instructions").String())
	require.Equal(t, "ok", gjson.Get(recorder.Body.String(), "choices.0.message.content").String())
	require.Equal(t, "claude-sonnet-4-6", gjson.Get(recorder.Body.String(), "model").String())
}

func TestCopilotChatOtherBadRequestDoesNotRetryResponses(t *testing.T) {
	require.False(t, isCopilotUnsupportedAPIForModel(http.StatusBadRequest, []byte(`{"error":{"code":"invalid_request"}}`)))
	require.False(t, isCopilotUnsupportedAPIForModel(http.StatusUnauthorized, []byte(`{"error":{"code":"unsupported_api_for_model"}}`)))
	require.True(t, isCopilotUnsupportedAPIForModel(http.StatusBadRequest, []byte(`{"error":{"code":"unsupported_api_for_model"}}`)))
}
