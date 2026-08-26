//go:build unit

package handler

import (
	"bytes"
	"context"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/Wei-Shaw/sub2api/internal/pkg/ctxkey"
	middleware "github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
	"github.com/tidwall/gjson"
)

type gatewayCopilotCompatUpstream struct {
	service.HTTPUpstream

	mu      sync.Mutex
	paths   []string
	headers []http.Header
}

func (u *gatewayCopilotCompatUpstream) Do(req *http.Request, _ string, _ int64, _ int) (*http.Response, error) {
	body, err := io.ReadAll(req.Body)
	if err != nil {
		return nil, err
	}
	u.mu.Lock()
	u.paths = append(u.paths, req.URL.Path)
	u.headers = append(u.headers, req.Header.Clone())
	u.mu.Unlock()

	header := http.Header{"x-request-id": []string{"copilot-handler-test"}}
	if req.URL.Path == "/v1/messages" {
		if gjson.GetBytes(body, "stream").Bool() {
			header.Set("Content-Type", "text/event-stream")
			streamBody := strings.Join([]string{
				"event: message_start",
				`data: {"type":"message_start","message":{"id":"msg_copilot","type":"message","role":"assistant","model":"claude-sonnet-4.5","content":[],"usage":{"input_tokens":100,"output_tokens":0,"cache_read_input_tokens":30}}}`,
				"",
				"event: content_block_start",
				`data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}`,
				"",
				"event: content_block_delta",
				`data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"ok"}}`,
				"",
				"event: content_block_stop",
				`data: {"type":"content_block_stop","index":0}`,
				"",
				"event: message_delta",
				`data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":20}}`,
				"",
				"event: message_stop",
				`data: {"type":"message_stop"}`,
				"",
			}, "\n")
			return &http.Response{StatusCode: http.StatusOK, Header: header, Body: io.NopCloser(strings.NewReader(streamBody))}, nil
		}

		header.Set("Content-Type", "application/json")
		responseBody := `{"id":"msg_copilot","type":"message","role":"assistant","model":"claude-sonnet-4.5","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":100,"output_tokens":20,"cache_read_input_tokens":30}}`
		return &http.Response{StatusCode: http.StatusOK, Header: header, Body: io.NopCloser(strings.NewReader(responseBody))}, nil
	}

	if gjson.GetBytes(body, "stream").Bool() {
		header.Set("Content-Type", "text/event-stream")
		streamBody := strings.Join([]string{
			`data: {"id":"chatcmpl_copilot","object":"chat.completion.chunk","created":1,"model":"claude-sonnet-4.5","choices":[{"index":0,"delta":{"role":"assistant","content":"ok"},"finish_reason":null}]}`,
			`data: {"id":"chatcmpl_copilot","object":"chat.completion.chunk","created":1,"model":"claude-sonnet-4.5","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}`,
			`data: {"id":"chatcmpl_copilot","object":"chat.completion.chunk","created":1,"model":"claude-sonnet-4.5","choices":[],"usage":{"prompt_tokens":100,"completion_tokens":20,"total_tokens":120,"prompt_tokens_details":{"cached_tokens":30}}}`,
			`data: [DONE]`,
			"",
		}, "\n\n")
		return &http.Response{StatusCode: http.StatusOK, Header: header, Body: io.NopCloser(strings.NewReader(streamBody))}, nil
	}

	header.Set("Content-Type", "application/json")
	responseBody := `{"id":"chatcmpl_copilot","object":"chat.completion","created":1,"model":"claude-sonnet-4.5","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],"usage":{"prompt_tokens":100,"completion_tokens":20,"total_tokens":120,"prompt_tokens_details":{"cached_tokens":30}}}`
	return &http.Response{StatusCode: http.StatusOK, Header: header, Body: io.NopCloser(strings.NewReader(responseBody))}, nil
}

func (u *gatewayCopilotCompatUpstream) snapshot() ([]string, []http.Header) {
	u.mu.Lock()
	defer u.mu.Unlock()
	paths := append([]string(nil), u.paths...)
	headers := make([]http.Header, len(u.headers))
	for i := range u.headers {
		headers[i] = u.headers[i].Clone()
	}
	return paths, headers
}

func newGatewayCopilotCompatHandler(t *testing.T) (*GatewayHandler, *service.Group, *service.APIKey, *gatewayCopilotCompatUpstream) {
	t.Helper()
	groupID := int64(9801)
	group := &service.Group{
		ID:       groupID,
		Hydrated: true,
		Platform: service.PlatformAnthropic,
		Status:   service.StatusActive,
	}
	account := &service.Account{
		ID:          9802,
		Name:        "copilot-handler-test",
		Platform:    service.PlatformOpenAI,
		Type:        service.AccountTypeOAuth,
		Status:      service.StatusActive,
		Schedulable: true,
		Concurrency: 1,
		Credentials: map[string]any{
			"oauth_profile": service.CopilotOAuthProfile,
			"access_token":  "copilot-test-token",
			"base_url":      "https://api.githubcopilot.com",
		},
		Extra: map[string]any{"mixed_scheduling": true},
		AccountGroups: []service.AccountGroup{
			{AccountID: 9802, GroupID: groupID},
		},
	}

	h, cleanup := newTestGatewayHandler(t, group, []*service.Account{account})
	t.Cleanup(cleanup)
	cfg := &config.Config{RunMode: config.RunModeSimple}
	cfg.Default.RateMultiplier = 1
	cfg.Security.URLAllowlist.Enabled = false
	h.cfg = cfg

	upstream := &gatewayCopilotCompatUpstream{}
	h.openAIGatewayService = service.NewOpenAIGatewayService(
		nil, nil, nil, nil, nil, nil, nil, cfg, nil, nil,
		service.NewBillingService(cfg, nil), nil, h.billingCacheService, upstream,
		&service.DeferredService{}, nil, nil, nil, nil, nil, nil, nil,
	)
	apiKey := &service.APIKey{
		ID:      9803,
		UserID:  9804,
		GroupID: &groupID,
		Group:   group,
		Status:  service.StatusActive,
		User:    &service.User{ID: 9804, Concurrency: 10, Balance: 100},
	}
	return h, group, apiKey, upstream
}

func gatewayCopilotCompatContext(t *testing.T, group *service.Group, apiKey *service.APIKey, path, body string) (*gin.Context, *httptest.ResponseRecorder) {
	t.Helper()
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	requestContext := context.WithValue(context.Background(), ctxkey.Group, group)
	c.Request = httptest.NewRequest(http.MethodPost, path, bytes.NewBufferString(body)).WithContext(requestContext)
	c.Request.Header.Set("Content-Type", "application/json")
	c.Set(string(middleware.ContextKeyAPIKey), apiKey)
	c.Set(string(middleware.ContextKeyUser), middleware.AuthSubject{UserID: apiKey.UserID, Concurrency: 10})
	return c, recorder
}

func TestGatewayHandlerCopilotCompat_MessagesAndChatCompletions(t *testing.T) {
	gin.SetMode(gin.TestMode)
	tests := []struct {
		name         string
		path         string
		body         string
		call         func(*GatewayHandler, *gin.Context)
		responseMark string
	}{
		{
			name: "messages non-stream", path: "/v1/messages",
			body: `{"model":"claude-sonnet-4.5","max_tokens":64,"messages":[{"role":"user","content":"hello"}],"stream":false}`,
			call: func(h *GatewayHandler, c *gin.Context) { h.Messages(c) }, responseMark: `"type":"message"`,
		},
		{
			name: "messages stream", path: "/v1/messages",
			body: `{"model":"claude-sonnet-4.5","max_tokens":64,"messages":[{"role":"user","content":"hello"}],"stream":true}`,
			call: func(h *GatewayHandler, c *gin.Context) { h.Messages(c) }, responseMark: "message_start",
		},
		{
			name: "chat non-stream", path: "/v1/chat/completions",
			body: `{"model":"claude-sonnet-4.5","messages":[{"role":"user","content":"hello"}],"stream":false}`,
			call: func(h *GatewayHandler, c *gin.Context) { h.ChatCompletions(c) }, responseMark: `"object":"chat.completion"`,
		},
		{
			name: "chat stream", path: "/v1/chat/completions",
			body: `{"model":"claude-sonnet-4.5","messages":[{"role":"user","content":"hello"}],"stream":true}`,
			call: func(h *GatewayHandler, c *gin.Context) { h.ChatCompletions(c) }, responseMark: `"object":"chat.completion.chunk"`,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			h, group, apiKey, upstream := newGatewayCopilotCompatHandler(t)
			c, recorder := gatewayCopilotCompatContext(t, group, apiKey, tt.path, tt.body)

			tt.call(h, c)

			require.Equal(t, http.StatusOK, recorder.Code)
			require.Contains(t, recorder.Body.String(), tt.responseMark)
			require.Contains(t, recorder.Body.String(), "ok")
			paths, headers := upstream.snapshot()
			expectedPath := "/chat/completions"
			if tt.path == "/v1/messages" {
				expectedPath = "/v1/messages"
			}
			require.Equal(t, []string{expectedPath}, paths)
			require.Len(t, headers, 1)
			require.Equal(t, "Bearer copilot-test-token", headers[0].Get("Authorization"))
		})
	}
}

func TestGatewayHandlerCopilotCompat_ResponsesAndCountTokensSkipCopilot(t *testing.T) {
	gin.SetMode(gin.TestMode)
	tests := []struct {
		name string
		path string
		body string
		call func(*GatewayHandler, *gin.Context)
	}{
		{
			name: "responses", path: "/v1/responses",
			body: `{"model":"claude-sonnet-4.5","input":"hello","stream":false}`,
			call: func(h *GatewayHandler, c *gin.Context) { h.Responses(c) },
		},
		{
			name: "count tokens", path: "/v1/messages/count_tokens",
			body: `{"model":"claude-sonnet-4.5","messages":[{"role":"user","content":"hello"}]}`,
			call: func(h *GatewayHandler, c *gin.Context) { h.CountTokens(c) },
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			h, group, apiKey, upstream := newGatewayCopilotCompatHandler(t)
			c, recorder := gatewayCopilotCompatContext(t, group, apiKey, tt.path, tt.body)

			tt.call(h, c)

			require.NotEqual(t, http.StatusOK, recorder.Code)
			paths, _ := upstream.snapshot()
			require.Empty(t, paths, "unsupported endpoint must not be forwarded to Copilot")
			_, selected := c.Get(opsAccountIDKey)
			require.False(t, selected, "a skipped Copilot account must not be reported as the selected upstream")
		})
	}
}
