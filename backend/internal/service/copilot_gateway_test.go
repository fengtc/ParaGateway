package service

import (
	"bytes"
	"context"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

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

func TestCopilotResponsesInputTokensReturnsNotImplementedWithoutUpstream(t *testing.T) {
	gin.SetMode(gin.TestMode)
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	body := []byte(`{"model":"gpt-4.1","input":"hello"}`)
	c.Request = httptest.NewRequest(http.MethodPost, "/v1/responses/input_tokens", bytes.NewReader(body))
	upstream := &httpUpstreamRecorder{resp: &http.Response{
		StatusCode: http.StatusOK,
		Body:       io.NopCloser(strings.NewReader(`{"input_tokens":1}`)),
	}}

	err := newCopilotGatewayTestService(upstream).ForwardResponsesInputTokens(
		context.Background(), c, newCopilotGatewayTestAccount(), body,
	)

	require.Error(t, err)
	require.Equal(t, http.StatusNotImplemented, infraerrors.Code(err))
	require.Equal(t, http.StatusNotImplemented, recorder.Code)
	require.Equal(t, "not_supported_error", gjson.Get(recorder.Body.String(), "error.type").String())
	require.Contains(t, recorder.Body.String(), "GitHub Copilot")
	require.Empty(t, upstream.requests, "Copilot credentials must never be sent to the OpenAI input_tokens host")
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

func TestCopilotBillingGuardPrefetchBoundsConcurrencyAndWarmsAllCandidates(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)

	const accountCount = 20
	accounts := make([]Account, 0, accountCount)
	for i := range accountCount {
		account := newCopilotGatewayTestAccount()
		account.ID = int64(1300 + i)
		account.Credentials["billing_username"] = "octocat"
		account.Credentials["billing_pat"] = fmt.Sprintf("billing-token-%d", i)
		accounts = append(accounts, *account)
	}

	var calls atomic.Int32
	var active atomic.Int32
	var maxActive atomic.Int32
	fetch := func(context.Context, string, string, string, int, int) (float64, error) {
		calls.Add(1)
		current := active.Add(1)
		for {
			observed := maxActive.Load()
			if current <= observed || maxActive.CompareAndSwap(observed, current) {
				break
			}
		}
		time.Sleep(25 * time.Millisecond)
		active.Add(-1)
		return 100, nil
	}

	startedAt := time.Now()
	prefetchCopilotBillingGuardsWithFetcher(context.Background(), accounts, copilotBillingPrefetchConcurrency, fetch)
	elapsed := time.Since(startedAt)

	require.EqualValues(t, accountCount, calls.Load())
	require.Greater(t, maxActive.Load(), int32(1))
	require.LessOrEqual(t, maxActive.Load(), int32(copilotBillingPrefetchConcurrency))
	require.Less(t, elapsed, 300*time.Millisecond)
}

func TestCopilotBillingGuardPrefetchFiltersCandidatesAndCachesFailures(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)

	valid := newCopilotGatewayTestAccount()
	valid.ID = 1401
	valid.Credentials["billing_username"] = "valid"
	valid.Credentials["billing_pat"] = "billing-token-valid"

	failing := newCopilotGatewayTestAccount()
	failing.ID = 1402
	failing.Credentials["billing_username"] = "failing"
	failing.Credentials["billing_pat"] = "billing-token-failing"

	nonCopilot := newCopilotGatewayTestAccount()
	nonCopilot.ID = 1403
	nonCopilot.Credentials["oauth_profile"] = "chatgpt"
	nonCopilot.Credentials["billing_username"] = "non-copilot"
	nonCopilot.Credentials["billing_pat"] = "billing-token-non-copilot"

	disabled := newCopilotGatewayTestAccount()
	disabled.ID = 1404
	disabled.Credentials["billing_username"] = "disabled"
	disabled.Credentials["billing_pat"] = "billing-token-disabled"
	disabled.Extra = map[string]any{"billing_auto_pause_disabled": true}

	missingCredentials := newCopilotGatewayTestAccount()
	missingCredentials.ID = 1405
	missingCredentials.Credentials["billing_username"] = ""
	missingCredentials.Credentials["billing_pat"] = ""

	accounts := []Account{*valid, *failing, *nonCopilot, *disabled, *missingCredentials}
	var calls atomic.Int32
	var calledUsers sync.Map
	fetch := func(_ context.Context, username, _ string, _ string, _ int, _ int) (float64, error) {
		calls.Add(1)
		calledUsers.Store(username, true)
		if username == "failing" {
			return 0, errors.New("billing unavailable")
		}
		return 100, nil
	}

	prefetchCopilotBillingGuardsWithFetcher(context.Background(), accounts, copilotBillingPrefetchConcurrency, fetch)
	prefetchCopilotBillingGuardsWithFetcher(context.Background(), accounts, copilotBillingPrefetchConcurrency, fetch)

	require.EqualValues(t, 2, calls.Load())
	_, validCalled := calledUsers.Load("valid")
	require.True(t, validCalled)
	_, failingCalled := calledUsers.Load("failing")
	require.True(t, failingCalled)
	for _, username := range []string{"non-copilot", "disabled", ""} {
		_, called := calledUsers.Load(username)
		require.False(t, called)
	}
}
func TestCopilotChatOtherBadRequestDoesNotRetryResponses(t *testing.T) {
	require.False(t, isCopilotUnsupportedAPIForModel(http.StatusBadRequest, []byte(`{"error":{"code":"invalid_request"}}`)))
	require.False(t, isCopilotUnsupportedAPIForModel(http.StatusUnauthorized, []byte(`{"error":{"code":"unsupported_api_for_model"}}`)))
	require.True(t, isCopilotUnsupportedAPIForModel(http.StatusBadRequest, []byte(`{"error":{"code":"unsupported_api_for_model"}}`)))
}

func TestCopilotBillingGuardExcludesAuthoritativeExhaustionBeforeTopK(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)

	account := newCopilotGatewayTestAccount()
	account.ID = 1501
	require.True(t, markCopilotBillingGuardExhausted(account))

	scheduler := &defaultOpenAIAccountScheduler{service: &OpenAIGatewayService{}}
	compatible, reason := scheduler.isAccountRequestCompatibleReason(
		context.Background(),
		account,
		OpenAIAccountScheduleRequest{RequestedModel: "gpt-5.6-sol"},
	)

	require.False(t, compatible)
	require.Equal(t, "copilot_billing_credit_limited", reason)
}
