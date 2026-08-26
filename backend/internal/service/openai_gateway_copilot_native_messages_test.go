//go:build unit

package service

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
	"github.com/tidwall/gjson"
)

func copilotNativeMessagesTestAccount() *Account {
	return &Account{
		ID:          7101,
		Name:        "copilot-native-messages",
		Platform:    PlatformOpenAI,
		Type:        AccountTypeOAuth,
		Concurrency: 1,
		Credentials: map[string]any{
			"oauth_profile": CopilotOAuthProfile,
			"access_token":  "copilot-test-token",
			"base_url":      "http://upstream.example",
		},
	}
}

func newCopilotMessagesContext(body []byte) (*gin.Context, *httptest.ResponseRecorder) {
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	c.Request = httptest.NewRequest(http.MethodPost, "/v1/messages", bytes.NewReader(body))
	c.Request.Header.Set("Content-Type", "application/json")
	return c, recorder
}

func copilotChatFallbackJSON() *http.Response {
	return &http.Response{
		StatusCode: http.StatusOK,
		Header: http.Header{
			"Content-Type": []string{"application/json"},
			"x-request-id": []string{"rid-chat-fallback"},
		},
		Body: io.NopCloser(strings.NewReader(
			`{"id":"chatcmpl-fallback","object":"chat.completion","model":"claude-sonnet-4.5","choices":[{"index":0,"message":{"role":"assistant","content":"fallback ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}`,
		)),
	}
}

func copilotChatFallbackStream() *http.Response {
	body := strings.Join([]string{
		`data: {"id":"chatcmpl-fallback","object":"chat.completion.chunk","model":"claude-sonnet-4.5","choices":[{"index":0,"delta":{"role":"assistant","content":"fallback ok"},"finish_reason":"stop"}]}`,
		"",
		`data: {"id":"chatcmpl-fallback","object":"chat.completion.chunk","model":"claude-sonnet-4.5","choices":[],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}`,
		"",
		"data: [DONE]",
		"",
	}, "\n")
	return &http.Response{
		StatusCode: http.StatusOK,
		Header: http.Header{
			"Content-Type": []string{"text/event-stream"},
			"x-request-id": []string{"rid-chat-fallback-stream"},
		},
		Body: io.NopCloser(strings.NewReader(body)),
	}
}

func TestPrepareCopilotNativeMessagesBodyPreservesCacheBreakpoints(t *testing.T) {
	body := []byte(`{
		"model":"claude-sonnet-4-5",
		"metadata":{"user_id":"session-test"},
		"system":[{"type":"text","text":"stable system","cache_control":{"type":"ephemeral","scope":"global"}}],
		"tools":[{"name":"Read","description":"read","input_schema":{"type":"object"}}],
		"messages":[
			{"role":"user","content":[{"type":"text","text":"first"}]},
			{"role":"assistant","content":[{"type":"text","text":"answer"}]},
			{"role":"user","content":[{"type":"text","text":"second"}]},
			{"role":"assistant","content":[{"type":"text","text":"latest"}]}
		],
		"max_tokens":1024,
		"stream":true
	}`)

	got, metadataUserID, err := prepareCopilotNativeMessagesBody(body, "claude-sonnet-4.5")
	require.NoError(t, err)
	require.Equal(t, "session-test", metadataUserID)
	require.Equal(t, "claude-sonnet-4.5", gjson.GetBytes(got, "model").String())
	require.False(t, gjson.GetBytes(got, "system.0.cache_control.scope").Exists())
	require.Equal(t, "ephemeral", gjson.GetBytes(got, "system.0.cache_control.type").String())
	require.Equal(t, "ephemeral", gjson.GetBytes(got, "tools.0.cache_control.type").String())
	require.Equal(t, "ephemeral", gjson.GetBytes(got, "messages.3.content.0.cache_control.type").String())
	require.Equal(t, maxCacheControlBlocks, strings.Count(string(got), `"cache_control"`))
}

func TestForwardAsAnthropic_CopilotUsesNativeMessagesAndCountsCacheOnce(t *testing.T) {
	gin.SetMode(gin.TestMode)
	body := []byte(`{
		"model":"claude-sonnet-4-5",
		"metadata":{"user_id":"{\"device_id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"account_uuid\":\"\",\"session_id\":\"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa\"}"},
		"messages":[{"role":"user","content":[{"type":"text","text":"hello"}]}],
		"max_tokens":64,
		"stream":false
	}`)
	c, recorder := newCopilotMessagesContext(body)
	c.Request.Header.Set("anthropic-version", "2023-06-01")
	c.Request.Header.Set("anthropic-beta", "interleaved-thinking-2025-05-14,unsupported-beta")
	upstream := &httpUpstreamRecorder{resp: &http.Response{
		StatusCode: http.StatusOK,
		Header: http.Header{
			"Content-Type": []string{"application/json"},
			"x-request-id": []string{"rid-native"},
		},
		Body: io.NopCloser(strings.NewReader(
			`{"id":"msg-native","type":"message","role":"assistant","model":"claude-sonnet-4.5","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","usage":{"input_tokens":50,"output_tokens":7,"cache_creation_input_tokens":20,"cache_read_input_tokens":30}}`,
		)),
	}}
	svc := &OpenAIGatewayService{cfg: rawChatCompletionsTestConfig(), httpUpstream: upstream}

	result, err := svc.ForwardAsAnthropic(context.Background(), c, copilotNativeMessagesTestAccount(), body, "", "")

	require.NoError(t, err)
	require.NotNil(t, result)
	require.Equal(t, "/v1/messages", upstream.lastReq.URL.Path)
	require.Equal(t, "Bearer copilot-test-token", upstream.lastReq.Header.Get("Authorization"))
	require.Equal(t, "messages-proxy", upstream.lastReq.Header.Get("x-interaction-type"))
	require.Equal(t, "messages-proxy", upstream.lastReq.Header.Get("openai-intent"))
	require.Empty(t, upstream.lastReq.Header.Get("copilot-integration-id"))
	require.Equal(t, "interleaved-thinking-2025-05-14", upstream.lastReq.Header.Get("anthropic-beta"))
	require.Equal(t, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", upstream.lastReq.Header.Get("editor-device-id"))
	require.Equal(t, "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", upstream.lastReq.Header.Get("x-interaction-id"))
	require.Equal(t, "claude-sonnet-4.5", gjson.GetBytes(upstream.lastBody, "model").String())
	require.Equal(t, 100, result.Usage.InputTokens)
	require.Equal(t, 20, result.Usage.CacheCreationInputTokens)
	require.Equal(t, 30, result.Usage.CacheReadInputTokens)
	require.Equal(t, 7, result.Usage.OutputTokens)
	require.Equal(t, "claude-sonnet-4-5", result.BillingModel)
	require.Equal(t, "claude-sonnet-4.5", result.UpstreamModel)
	require.Equal(t, copilotNativeMessagesEndpoint, result.UpstreamEndpoint)
	require.Equal(t, copilotNativeMessagesEndpoint, GetActualOpenAIUpstreamEndpoint(c))
	require.Contains(t, recorder.Body.String(), `"cache_creation_input_tokens":20`)
}

func TestForwardAsAnthropic_CopilotNativeStatusFallsBackToChatCompletions(t *testing.T) {
	gin.SetMode(gin.TestMode)
	for _, status := range []int{
		http.StatusBadRequest,
		http.StatusNotFound,
		http.StatusMethodNotAllowed,
		http.StatusUnsupportedMediaType,
		http.StatusUnprocessableEntity,
	} {
		t.Run(http.StatusText(status), func(t *testing.T) {
			body := []byte(`{"model":"claude-sonnet-4-5","messages":[{"role":"user","content":"hello"}],"max_tokens":64,"stream":false}`)
			c, recorder := newCopilotMessagesContext(body)
			upstream := &httpUpstreamRecorder{responses: []*http.Response{
				{
					StatusCode: status,
					Header:     http.Header{"Content-Type": []string{"application/json"}},
					Body:       io.NopCloser(strings.NewReader(`{"error":{"message":"native unsupported"}}`)),
				},
				copilotChatFallbackJSON(),
			}}
			svc := &OpenAIGatewayService{cfg: rawChatCompletionsTestConfig(), httpUpstream: upstream}

			result, err := svc.ForwardAsAnthropic(context.Background(), c, copilotNativeMessagesTestAccount(), body, "", "")

			require.NoError(t, err)
			require.NotNil(t, result)
			require.Len(t, upstream.requests, 2)
			require.Equal(t, "/v1/messages", upstream.requests[0].URL.Path)
			require.Equal(t, "/chat/completions", upstream.requests[1].URL.Path)
			require.Equal(t, messagesChatFallbackUpstreamEndpoint, result.UpstreamEndpoint)
			require.Equal(t, messagesChatFallbackUpstreamEndpoint, GetActualOpenAIUpstreamEndpoint(c))
			require.Contains(t, recorder.Body.String(), "fallback ok")
			require.NotContains(t, recorder.Body.String(), "native unsupported")
		})
	}
}

func TestForwardAsAnthropic_CopilotInvalidNative200FallsBack(t *testing.T) {
	gin.SetMode(gin.TestMode)
	body := []byte(`{"model":"claude-sonnet-4-5","messages":[{"role":"user","content":"hello"}],"max_tokens":64,"stream":false}`)
	c, recorder := newCopilotMessagesContext(body)
	upstream := &httpUpstreamRecorder{responses: []*http.Response{
		{
			StatusCode: http.StatusOK,
			Header:     http.Header{"Content-Type": []string{"application/json"}},
			Body:       io.NopCloser(strings.NewReader(`{"id":"msg-native","type":"message","role":"assistant","content":[]}`)),
		},
		copilotChatFallbackJSON(),
	}}
	svc := &OpenAIGatewayService{cfg: rawChatCompletionsTestConfig(), httpUpstream: upstream}

	result, err := svc.ForwardAsAnthropic(context.Background(), c, copilotNativeMessagesTestAccount(), body, "", "")

	require.NoError(t, err)
	require.Equal(t, messagesChatFallbackUpstreamEndpoint, result.UpstreamEndpoint)
	require.Equal(t, []string{"/v1/messages", "/chat/completions"}, []string{upstream.requests[0].URL.Path, upstream.requests[1].URL.Path})
	require.Contains(t, recorder.Body.String(), "fallback ok")
}

func TestForwardAsAnthropic_CopilotNativeStreamBeforeStartFallsBack(t *testing.T) {
	gin.SetMode(gin.TestMode)
	body := []byte(`{"model":"claude-sonnet-4-5","messages":[{"role":"user","content":"hello"}],"max_tokens":64,"stream":true}`)
	c, recorder := newCopilotMessagesContext(body)
	upstream := &httpUpstreamRecorder{responses: []*http.Response{
		{
			StatusCode: http.StatusOK,
			Header:     http.Header{"Content-Type": []string{"text/event-stream"}},
			Body:       io.NopCloser(strings.NewReader("event: ping\ndata: {\"type\":\"ping\"}\n\n")),
		},
		copilotChatFallbackStream(),
	}}
	svc := &OpenAIGatewayService{cfg: rawChatCompletionsTestConfig(), httpUpstream: upstream}

	result, err := svc.ForwardAsAnthropic(context.Background(), c, copilotNativeMessagesTestAccount(), body, "", "")

	require.NoError(t, err)
	require.Equal(t, messagesChatFallbackUpstreamEndpoint, result.UpstreamEndpoint)
	require.Equal(t, []string{"/v1/messages", "/chat/completions"}, []string{upstream.requests[0].URL.Path, upstream.requests[1].URL.Path})
	require.NotContains(t, recorder.Body.String(), `"type":"ping"`)
	require.Contains(t, recorder.Body.String(), "fallback ok")
}

func TestForwardAsAnthropic_CopilotNativeStreamDoesNotFallbackAfterMessageStart(t *testing.T) {
	gin.SetMode(gin.TestMode)
	body := []byte(`{"model":"claude-sonnet-4-5","messages":[{"role":"user","content":"hello"}],"max_tokens":64,"stream":true}`)
	c, recorder := newCopilotMessagesContext(body)
	nativeStream := strings.Join([]string{
		"event: message_start",
		`data: {"type":"message_start","message":{"id":"msg-native","type":"message","role":"assistant","model":"claude-sonnet-4.5","content":[],"usage":{"input_tokens":3,"output_tokens":0}}}`,
		"",
		"event: content_block_delta",
		`data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"partial"}}`,
		"",
	}, "\n")
	upstream := &httpUpstreamRecorder{resp: &http.Response{
		StatusCode: http.StatusOK,
		Header: http.Header{
			"Content-Type": []string{"text/event-stream"},
			"x-request-id": []string{"rid-native-partial"},
		},
		Body: io.NopCloser(strings.NewReader(nativeStream)),
	}}
	svc := &OpenAIGatewayService{cfg: rawChatCompletionsTestConfig(), httpUpstream: upstream}

	result, err := svc.ForwardAsAnthropic(context.Background(), c, copilotNativeMessagesTestAccount(), body, "", "")

	require.Error(t, err)
	require.Contains(t, err.Error(), "missing message_stop")
	require.NotNil(t, result)
	require.Equal(t, copilotNativeMessagesEndpoint, result.UpstreamEndpoint)
	require.Equal(t, 3, result.Usage.InputTokens)
	require.Len(t, upstream.requests, 1, "fallback must not run after message_start was written")
	require.Equal(t, "/v1/messages", upstream.requests[0].URL.Path)
	require.Contains(t, recorder.Body.String(), "partial")
	require.NotContains(t, recorder.Body.String(), "message_stop")
}

func TestShouldFallbackCopilotNativeMessagesStatus(t *testing.T) {
	for _, tt := range []struct {
		status int
		want   bool
	}{
		{http.StatusBadRequest, true},
		{http.StatusNotFound, true},
		{http.StatusMethodNotAllowed, true},
		{http.StatusUnsupportedMediaType, true},
		{http.StatusUnprocessableEntity, true},
		{http.StatusUnauthorized, false},
		{http.StatusForbidden, false},
		{http.StatusTooManyRequests, false},
		{http.StatusInternalServerError, false},
	} {
		t.Run(fmt.Sprintf("status_%d", tt.status), func(t *testing.T) {
			require.Equal(t, tt.want, shouldFallbackCopilotNativeMessagesStatus(tt.status))
		})
	}
}
