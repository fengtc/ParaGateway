package service

import (
	"bytes"
	"context"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"sync/atomic"
	"testing"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func TestCopilotDeviceOAuthPendingThenCreatesAccountOnce(t *testing.T) {
	var tokenPolls atomic.Int32
	var createCalls atomic.Int32
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		switch r.URL.Path {
		case "/device":
			require.Equal(t, http.MethodPost, r.Method)
			require.NoError(t, r.ParseForm())
			require.Equal(t, copilotDeviceClientID, r.Form.Get("client_id"))
			require.Equal(t, "read:user", r.Form.Get("scope"))
			_, _ = io.WriteString(w, `{"device_code":"device-123","user_code":"ABCD-EFGH","verification_uri":"https://github.com/login/device","expires_in":900,"interval":1}`)
		case "/token":
			require.NoError(t, r.ParseForm())
			require.Equal(t, "device-123", r.Form.Get("device_code"))
			if tokenPolls.Add(1) == 1 {
				_, _ = io.WriteString(w, `{"error":"authorization_pending"}`)
				return
			}
			_, _ = io.WriteString(w, `{"access_token":"github-token-123"}`)
		case "/user":
			require.Equal(t, "token github-token-123", r.Header.Get("Authorization"))
			_, _ = io.WriteString(w, `{"login":"octocat","id":12345}`)
		case "/exchange":
			require.Equal(t, "token github-token-123", r.Header.Get("Authorization"))
			require.Equal(t, "vscode/1.98.1", r.Header.Get("Editor-Version"))
			require.Equal(t, "copilot-chat/0.26.7", r.Header.Get("Editor-Plugin-Version"))
			require.Empty(t, r.Header.Get("Copilot-Integration-Id"), "GitHub token exchange must not use Copilot API headers")
			payload := map[string]any{
				"token":      "copilot-access-token-123",
				"expires_at": time.Now().Add(time.Hour).Unix(),
				"refresh_in": 1800,
			}
			require.NoError(t, json.NewEncoder(w).Encode(payload))
		default:
			http.NotFound(w, r)
		}
	}))
	defer server.Close()

	service := &OpenAIOAuthService{
		copilotHTTPClient: server.Client(),
		copilotEndpoints: copilotOAuthEndpoints{
			deviceCodeURL:    server.URL + "/device",
			accessTokenURL:   server.URL + "/token",
			githubUserURL:    server.URL + "/user",
			tokenExchangeURL: server.URL + "/exchange",
		},
	}
	started, err := service.StartCopilotOAuthFlow(context.Background(), 7, "  primary copilot  ")
	require.NoError(t, err)
	require.Equal(t, CopilotOAuthStatusPending, started.Status)
	require.Equal(t, "ABCD-EFGH", started.UserCode)
	require.Equal(t, 5, started.IntervalSeconds, "GitHub device polling interval is clamped to five seconds")
	require.NotEmpty(t, started.FlowID)

	create := func(_ context.Context, name string, credentials map[string]any) (*Account, error) {
		createCalls.Add(1)
		require.Equal(t, "primary copilot", name)
		require.Equal(t, CopilotOAuthProfile, credentials["oauth_profile"])
		require.Equal(t, "github-token-123", credentials["github_access_token"])
		require.Equal(t, "copilot-access-token-123", credentials["access_token"])
		require.Equal(t, "octocat", credentials["github_login"])
		require.Equal(t, "12345", credentials["github_user_id"])
		return &Account{ID: 88, Name: name, Credentials: credentials}, nil
	}

	pending, err := service.PollCopilotOAuthFlow(context.Background(), 7, started.FlowID, create)
	require.NoError(t, err)
	require.Equal(t, CopilotOAuthStatusPending, pending.Status)
	require.Zero(t, createCalls.Load())

	flow := service.getCopilotFlow(started.FlowID, 7)
	require.NotNil(t, flow)
	flow.NextPollAt = time.Now().Add(-time.Second)
	completed, err := service.PollCopilotOAuthFlow(context.Background(), 7, started.FlowID, create)
	require.NoError(t, err)
	require.Equal(t, CopilotOAuthStatusCompleted, completed.Status)
	require.Equal(t, int64(88), *completed.ProviderAccountID)
	require.NotNil(t, completed.ProviderAccount)
	require.EqualValues(t, 1, createCalls.Load())

	again, err := service.PollCopilotOAuthFlow(context.Background(), 7, started.FlowID, create)
	require.NoError(t, err)
	require.Equal(t, CopilotOAuthStatusCompleted, again.Status)
	require.EqualValues(t, 1, createCalls.Load(), "completed flow must not create a duplicate account")
}

func TestCopilotExchangeHeadersAndZeroValueRuntime(t *testing.T) {
	var requestCount atomic.Int32
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		requestCount.Add(1)
		require.Equal(t, http.MethodGet, r.Method)
		require.Equal(t, "token github-token-xyz", r.Header.Get("Authorization"))
		require.Equal(t, "GitHubCopilotChat/0.26.7", r.Header.Get("User-Agent"))
		_, _ = io.WriteString(w, `{"token":"copilot-token-xyz","expires_at":4102444800,"refresh_in":1200}`)
	}))
	defer server.Close()

	service := &OpenAIOAuthService{
		copilotHTTPClient: server.Client(),
		copilotEndpoints:  copilotOAuthEndpoints{tokenExchangeURL: server.URL},
	}
	info, err := service.ExchangeCopilotToken(context.Background(), " github-token-xyz ")
	require.NoError(t, err)
	require.Equal(t, "copilot-token-xyz", info.AccessToken)
	require.EqualValues(t, 1, requestCount.Load())
	require.NotNil(t, service.copilotFlows, "zero-value service lazily initializes the flow store")
	require.NotEmpty(t, service.copilotEndpoints.deviceCodeURL)
	require.NotEmpty(t, service.copilotEndpoints.accessTokenURL)
	require.NotEmpty(t, service.copilotEndpoints.githubUserURL)
}

func TestCopilotUpstream401ForcesTokenRefreshAndRetriesOnce(t *testing.T) {
	var exchangeCalls atomic.Int32
	exchange := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		exchangeCalls.Add(1)
		require.Equal(t, "token github-refresh-token", r.Header.Get("Authorization"))
		_, _ = io.WriteString(w, `{"token":"fresh-copilot-token","expires_at":4102444800,"refresh_in":1200}`)
	}))
	defer exchange.Close()

	oauthService := &OpenAIOAuthService{
		copilotHTTPClient: exchange.Client(),
		copilotEndpoints:  copilotOAuthEndpoints{tokenExchangeURL: exchange.URL},
	}
	upstream := &httpUpstreamRecorder{responses: []*http.Response{
		{StatusCode: http.StatusUnauthorized, Header: http.Header{}, Body: io.NopCloser(strings.NewReader(`{"error":{"message":"expired"}}`))},
		{StatusCode: http.StatusOK, Header: http.Header{}, Body: io.NopCloser(strings.NewReader(`{"ok":true}`))},
	}}
	service := newCopilotGatewayTestService(upstream)
	service.openAITokenProvider = NewOpenAITokenProvider(nil, nil, oauthService)
	account := newCopilotGatewayTestAccount()
	account.Credentials["access_token"] = "stale-copilot-token"
	account.Credentials["github_access_token"] = "github-refresh-token"

	gin.SetMode(gin.TestMode)
	c, _ := gin.CreateTestContext(httptest.NewRecorder())
	c.Request = httptest.NewRequest(http.MethodPost, "/v1/chat/completions", bytes.NewReader(nil))
	response, err := service.sendCCUpstreamRequest(
		context.Background(),
		c,
		account,
		buildCopilotAPIURL(CopilotAPIBaseURL, "/chat/completions"),
		[]byte(`{"model":"gpt-4.1","messages":[]}`),
		false,
		"stale-copilot-token",
		"",
		"",
	)
	require.NoError(t, err)
	require.Equal(t, http.StatusOK, response.StatusCode)
	require.Len(t, upstream.requests, 2)
	require.Equal(t, "Bearer stale-copilot-token", upstream.requests[0].Header.Get("Authorization"))
	require.Equal(t, "Bearer fresh-copilot-token", upstream.requests[1].Header.Get("Authorization"))
	require.Equal(t, "fresh-copilot-token", account.GetOpenAIAccessToken())
	require.EqualValues(t, 1, exchangeCalls.Load())
}

func TestCopilotFormHeadersAreEncoded(t *testing.T) {
	headers := copilotFormHeaders()
	require.Equal(t, "application/x-www-form-urlencoded", headers["Content-Type"])
	values := url.Values{"client_id": {copilotDeviceClientID}, "scope": {"read:user"}}
	require.Contains(t, values.Encode(), "scope=read%3Auser")
}
