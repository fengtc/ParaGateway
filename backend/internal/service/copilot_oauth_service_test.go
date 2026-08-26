package service

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
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

func TestNormalizeCopilotModelDropsClaudeDateSuffix(t *testing.T) {
	require.Equal(t, "claude-sonnet-4.5", normalizeCopilotModel("claude-sonnet-4-5-20250929"))
	require.Equal(t, "claude-opus-4.6", normalizeCopilotModel(" claude-opus-4-6 "))
	require.Equal(t, "gpt-4.1", normalizeCopilotModel("gpt-4.1"))
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
	account.Credentials["user_agent"] = "configured-openai-agent/1.0"
	account.Credentials["access_token"] = "stale-copilot-token"
	account.Credentials["github_access_token"] = "github-refresh-token"

	gin.SetMode(gin.TestMode)
	c, _ := gin.CreateTestContext(httptest.NewRecorder())
	c.Request = httptest.NewRequest(http.MethodPost, "/v1/chat/completions", bytes.NewReader(nil))
	c.Request.Header.Set("User-Agent", "PostmanRuntime/7.46.0")
	response, err := service.sendCCUpstreamRequest(
		context.Background(),
		c,
		account,
		buildCopilotAPIURL(CopilotAPIBaseURL, "/chat/completions"),
		[]byte(`{"model":"gpt-4.1","messages":[]}`),
		false,
		"stale-copilot-token",
		account.GetOpenAIUserAgent(),
		"",
	)
	require.NoError(t, err)
	require.Equal(t, http.StatusOK, response.StatusCode)
	require.Len(t, upstream.requests, 2)
	require.Equal(t, "Bearer stale-copilot-token", upstream.requests[0].Header.Get("Authorization"))
	require.Equal(t, "Bearer fresh-copilot-token", upstream.requests[1].Header.Get("Authorization"))
	require.Equal(t, "GitHubCopilotChat/0.26.7", upstream.requests[0].Header.Get("User-Agent"))
	require.Equal(t, "GitHubCopilotChat/0.26.7", upstream.requests[1].Header.Get("User-Agent"))
	require.Equal(t, "fresh-copilot-token", account.GetOpenAIAccessToken())
	require.EqualValues(t, 1, exchangeCalls.Load())
}

func TestCopilotFormHeadersAreEncoded(t *testing.T) {
	headers := copilotFormHeaders()
	require.Equal(t, "application/x-www-form-urlencoded", headers["Content-Type"])
	values := url.Values{"client_id": {copilotDeviceClientID}, "scope": {"read:user"}}
	require.Contains(t, values.Encode(), "scope=read%3Auser")
}

func TestCopilotDeviceOAuthCreationFailureCanRetryWithSettings(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		switch r.URL.Path {
		case "/device":
			_, _ = io.WriteString(w, `{"device_code":"device-retry","user_code":"RETRY-CODE","verification_uri":"https://github.com/login/device","expires_in":900,"interval":5}`)
		case "/token":
			_, _ = io.WriteString(w, `{"access_token":"github-token-retry"}`)
		case "/user":
			_, _ = io.WriteString(w, `{"login":"retry-user","id":987}`)
		case "/exchange":
			_, _ = io.WriteString(w, `{"token":"copilot-token-retry","expires_at":4102444800,"refresh_in":1200}`)
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
	notes := "team-owned account"
	loadFactor := 37
	rateMultiplier := 1.25
	expiresAt := time.Now().UTC().AddDate(1, 0, 0).Unix()
	autoPause := true
	schedulable := false
	creditLimit := 12345.0
	safetyMargin := 345.0
	autoPauseDisabled := false
	settings := CopilotAccountSettings{
		Name:                     "retry copilot",
		Notes:                    &notes,
		Concurrency:              6,
		LoadFactor:               &loadFactor,
		Priority:                 23,
		RateMultiplier:           &rateMultiplier,
		GroupIDs:                 []int64{4, 8},
		ExpiresAt:                &expiresAt,
		AutoPauseOnExpired:       &autoPause,
		Schedulable:              &schedulable,
		ModelMapping:             map[string]string{"claude-sonnet-4-6": "claude-sonnet-4.6"},
		BillingPAT:               "billing-pat-retry",
		BillingCreditLimit:       &creditLimit,
		BillingSafetyMargin:      &safetyMargin,
		BillingAutoPauseDisabled: &autoPauseDisabled,
	}
	started, err := service.StartCopilotOAuthFlowWithSettings(context.Background(), 17, settings)
	require.NoError(t, err)

	flow := service.getCopilotFlow(started.FlowID, 17)
	require.NotNil(t, flow)
	flow.NextPollAt = time.Now().Add(-time.Second)
	createErr := errors.New("database temporarily unavailable")
	createCalls := 0
	creator := func(_ context.Context, got CopilotAccountSettings, credentials, extra map[string]any) (*Account, error) {
		createCalls++
		require.Equal(t, "retry copilot", got.Name)
		require.Equal(t, &notes, got.Notes)
		require.Equal(t, 6, got.Concurrency)
		require.Equal(t, &loadFactor, got.LoadFactor)
		require.Equal(t, 23, got.Priority)
		require.Equal(t, &rateMultiplier, got.RateMultiplier)
		require.Equal(t, []int64{4, 8}, got.GroupIDs)
		require.Equal(t, &expiresAt, got.ExpiresAt)
		require.Equal(t, &autoPause, got.AutoPauseOnExpired)
		require.Equal(t, &schedulable, got.Schedulable)
		require.Equal(t, "retry-user", got.BillingUsername)
		require.Equal(t, "github-token-retry", credentials["github_access_token"])
		require.Equal(t, "billing-pat-retry", credentials["billing_pat"])
		require.Equal(t, "retry-user", credentials["billing_username"])
		require.Equal(t, map[string]any{"claude-sonnet-4-6": "claude-sonnet-4.6"}, credentials["model_mapping"])
		require.Equal(t, creditLimit, extra["billing_credit_limit"])
		require.Equal(t, safetyMargin, extra["billing_safety_margin"])
		require.Equal(t, autoPauseDisabled, extra["billing_auto_pause_disabled"])
		if createCalls == 1 {
			return nil, createErr
		}
		return &Account{ID: 73, Name: got.Name, Notes: got.Notes, Credentials: credentials, Extra: extra}, nil
	}

	first, err := service.PollCopilotOAuthFlowWithSettings(context.Background(), 17, started.FlowID, creator)
	require.ErrorIs(t, err, createErr)
	require.Nil(t, first)
	require.Equal(t, "github-token-retry", flow.GitHubToken, "authorized token must remain server-side for retry")
	require.Empty(t, flow.DeviceCode)
	require.Equal(t, "billing-pat-retry", flow.Settings.BillingPAT)
	require.WithinDuration(t, time.Now().UTC().Add(copilotAccountCreationRetryWindow), flow.AccountCreateExpiresAt, 2*time.Second)

	completed, err := service.PollCopilotOAuthFlowWithSettings(context.Background(), 17, started.FlowID, creator)
	require.NoError(t, err)
	require.Equal(t, CopilotOAuthStatusCompleted, completed.Status)
	require.EqualValues(t, 2, createCalls)
	require.EqualValues(t, 73, *completed.ProviderAccountID)
	require.Empty(t, flow.GitHubToken)
	require.Empty(t, flow.DeviceCode)
	require.Empty(t, flow.Settings.BillingPAT)
	require.Nil(t, flow.Settings.Notes)
}

func TestCleanupCopilotFlowsExpiresAuthorizationAndAccountCreationWindows(t *testing.T) {
	now := time.Now().UTC()
	deviceFlow := &copilotOAuthFlow{
		ID:          "11111111-1111-4111-8111-111111111111",
		AdminUserID: 7,
		Settings: copilotAccountFlowSettings{CopilotAccountSettings: CopilotAccountSettings{
			Name: "expired device", BillingPAT: "device-billing-secret",
		}},
		DeviceCode: "device-secret",
		Status:     CopilotOAuthStatusPending,
		ExpiresAt:  now.Add(-time.Second),
		CreatedAt:  now.Add(-time.Minute),
	}
	creationFlow := &copilotOAuthFlow{
		ID:          "22222222-2222-4222-8222-222222222222",
		AdminUserID: 7,
		Settings: copilotAccountFlowSettings{CopilotAccountSettings: CopilotAccountSettings{
			Name: "expired creation", BillingPAT: "creation-billing-secret",
		}},
		GitHubToken:            "github-secret",
		Status:                 CopilotOAuthStatusPending,
		ExpiresAt:              now.Add(time.Hour),
		AccountCreateExpiresAt: now.Add(-time.Second),
		CreatedAt:              now.Add(-time.Minute),
	}
	activeFlow := &copilotOAuthFlow{
		ID:          "33333333-3333-4333-8333-333333333333",
		AdminUserID: 7,
		Settings: copilotAccountFlowSettings{CopilotAccountSettings: CopilotAccountSettings{
			Name: "active creation", BillingPAT: "active-billing-secret",
		}},
		GitHubToken:            "active-github-secret",
		Status:                 CopilotOAuthStatusPending,
		ExpiresAt:              now.Add(time.Hour),
		AccountCreateExpiresAt: now.Add(time.Minute),
		CreatedAt:              now.Add(-time.Minute),
	}
	service := &OpenAIOAuthService{copilotFlows: map[string]*copilotOAuthFlow{
		deviceFlow.ID:   deviceFlow,
		creationFlow.ID: creationFlow,
		activeFlow.ID:   activeFlow,
	}}

	service.copilotFlowsMu.Lock()
	service.cleanupCopilotFlowsLocked(now)
	pending := service.countPendingCopilotFlowsLocked(7)
	service.copilotFlowsMu.Unlock()

	for _, expired := range []*copilotOAuthFlow{deviceFlow, creationFlow} {
		require.Equal(t, CopilotOAuthStatusExpired, expired.Status)
		require.Empty(t, expired.DeviceCode)
		require.Empty(t, expired.GitHubToken)
		require.Empty(t, expired.Settings.BillingPAT)
		require.Empty(t, expired.Settings.Name)
		require.Contains(t, service.copilotFlows, expired.ID, "expired results remain queryable during retention")
	}
	require.Equal(t, CopilotOAuthStatusPending, activeFlow.Status)
	require.Equal(t, "active-github-secret", activeFlow.GitHubToken)
	require.Equal(t, "active-billing-secret", activeFlow.Settings.BillingPAT)
	require.Equal(t, 1, pending)
}

func TestStartCopilotOAuthFlowConcurrentCapacityCheckAllowsOnlyThreePendingPerAdmin(t *testing.T) {
	requestsStarted := make(chan struct{}, 2)
	releaseRequests := make(chan struct{})
	var deviceRequests atomic.Int32
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		deviceRequests.Add(1)
		requestsStarted <- struct{}{}
		<-releaseRequests
		w.Header().Set("Content-Type", "application/json")
		_, _ = io.WriteString(w, `{"device_code":"concurrent-device","user_code":"CONCURRENT","verification_uri":"https://github.com/login/device","expires_in":900,"interval":5}`)
	}))
	defer server.Close()

	now := time.Now().UTC()
	service := &OpenAIOAuthService{
		copilotHTTPClient: server.Client(),
		copilotEndpoints:  copilotOAuthEndpoints{deviceCodeURL: server.URL},
		copilotFlows: map[string]*copilotOAuthFlow{
			"11111111-1111-4111-8111-111111111111": {ID: "11111111-1111-4111-8111-111111111111", AdminUserID: 9, Status: CopilotOAuthStatusPending, CreatedAt: now, ExpiresAt: now.Add(time.Hour)},
			"22222222-2222-4222-8222-222222222222": {ID: "22222222-2222-4222-8222-222222222222", AdminUserID: 9, Status: CopilotOAuthStatusPending, CreatedAt: now, ExpiresAt: now.Add(time.Hour)},
		},
	}

	results := make(chan error, 2)
	var starts sync.WaitGroup
	starts.Add(2)
	for i := 0; i < 2; i++ {
		go func() {
			defer starts.Done()
			_, err := service.StartCopilotOAuthFlow(context.Background(), 9, "concurrent")
			results <- err
		}()
	}
	<-requestsStarted
	<-requestsStarted
	close(releaseRequests)
	starts.Wait()

	successes := 0
	limited := 0
	for i := 0; i < 2; i++ {
		err := <-results
		if err == nil {
			successes++
			continue
		}
		require.Equal(t, http.StatusTooManyRequests, infraerrors.Code(err))
		require.Equal(t, "COPILOT_OAUTH_FLOW_LIMIT", infraerrors.Reason(err))
		limited++
	}
	service.copilotFlowsMu.Lock()
	pending := service.countPendingCopilotFlowsLocked(9)
	service.copilotFlowsMu.Unlock()
	require.Equal(t, 1, successes)
	require.Equal(t, 1, limited)
	require.EqualValues(t, 2, deviceRequests.Load(), "both calls must pass the preflight check before the insertion check")
	require.Equal(t, copilotMaxPendingFlowsPerAdmin, pending)
}

func TestCancelCopilotOAuthFlowEnforcesOwnerClearsSecretsAndReleasesCapacity(t *testing.T) {
	now := time.Now().UTC()
	ownedID := "11111111-1111-4111-8111-111111111111"
	owned := &copilotOAuthFlow{
		ID:          ownedID,
		AdminUserID: 7,
		Settings: copilotAccountFlowSettings{CopilotAccountSettings: CopilotAccountSettings{
			Name: "owned", BillingPAT: "billing-secret",
		}},
		DeviceCode:  "device-secret",
		GitHubToken: "github-secret",
		Status:      CopilotOAuthStatusPending,
		CreatedAt:   now,
		ExpiresAt:   now.Add(time.Hour),
	}
	service := &OpenAIOAuthService{copilotFlows: map[string]*copilotOAuthFlow{
		ownedID:                                owned,
		"22222222-2222-4222-8222-222222222222": {ID: "22222222-2222-4222-8222-222222222222", AdminUserID: 7, Status: CopilotOAuthStatusPending, CreatedAt: now, ExpiresAt: now.Add(time.Hour)},
		"33333333-3333-4333-8333-333333333333": {ID: "33333333-3333-4333-8333-333333333333", AdminUserID: 7, Status: CopilotOAuthStatusPending, CreatedAt: now, ExpiresAt: now.Add(time.Hour)},
	}}

	err := service.ensureCopilotFlowCapacity(7, now)
	require.Equal(t, http.StatusTooManyRequests, infraerrors.Code(err))
	err = service.CancelCopilotOAuthFlow(8, ownedID)
	require.Equal(t, http.StatusNotFound, infraerrors.Code(err))
	require.Equal(t, "github-secret", owned.GitHubToken, "another administrator must not mutate the flow")
	require.Contains(t, service.copilotFlows, ownedID)

	require.NoError(t, service.CancelCopilotOAuthFlow(7, ownedID))
	require.NotContains(t, service.copilotFlows, ownedID)
	require.Empty(t, owned.DeviceCode)
	require.Empty(t, owned.GitHubToken)
	require.Empty(t, owned.Settings.BillingPAT)
	require.Empty(t, owned.Settings.Name)
	require.NoError(t, service.ensureCopilotFlowCapacity(7, now), "cancelling a flow must release one pending slot")
}

func TestPollCapturedBeforeCancelObservesTerminalStateWithoutUpstreamCall(t *testing.T) {
	var upstreamCalls atomic.Int32
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		upstreamCalls.Add(1)
		http.Error(w, "must not be called", http.StatusInternalServerError)
	}))
	defer server.Close()

	now := time.Now().UTC()
	flowID := "11111111-1111-4111-8111-111111111111"
	flow := &copilotOAuthFlow{
		ID:              flowID,
		AdminUserID:     7,
		Settings:        copilotAccountFlowSettings{CopilotAccountSettings: CopilotAccountSettings{Name: "captured", BillingPAT: "billing-secret"}},
		DeviceCode:      "device-secret",
		Status:          CopilotOAuthStatusPending,
		IntervalSeconds: 5,
		NextPollAt:      now.Add(-time.Second),
		ExpiresAt:       now.Add(time.Hour),
		CreatedAt:       now,
	}
	service := &OpenAIOAuthService{
		copilotHTTPClient: server.Client(),
		copilotEndpoints:  copilotOAuthEndpoints{accessTokenURL: server.URL},
		copilotFlows:      map[string]*copilotOAuthFlow{flowID: flow},
	}

	// Simulate Poll resolving the pointer before Cancel removes it from the map,
	// but acquiring flow.mu only after Cancel has completed.
	captured := service.getCopilotFlow(flowID, 7)
	require.Same(t, flow, captured)
	require.NoError(t, service.CancelCopilotOAuthFlow(7, flowID))
	createCalls := 0
	result, err := service.pollCapturedCopilotOAuthFlowWithSettings(
		context.Background(),
		7,
		captured,
		func(context.Context, CopilotAccountSettings, map[string]any, map[string]any) (*Account, error) {
			createCalls++
			return &Account{ID: 99}, nil
		},
	)

	require.NoError(t, err)
	require.Equal(t, CopilotOAuthStatusExpired, result.Status)
	require.Zero(t, upstreamCalls.Load())
	require.Zero(t, createCalls)
	require.Empty(t, captured.DeviceCode)
	require.Empty(t, captured.GitHubToken)
	require.Empty(t, captured.Settings.BillingPAT)
}

func TestCreateCopilotAccountFromGitHubTokenFillsBillingUsername(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		switch r.URL.Path {
		case "/user":
			require.Equal(t, "token manual-github-token", r.Header.Get("Authorization"))
			_, _ = io.WriteString(w, `{"login":"manual-user","id":456}`)
		case "/exchange":
			require.Equal(t, "token manual-github-token", r.Header.Get("Authorization"))
			_, _ = io.WriteString(w, `{"token":"manual-copilot-token","expires_at":4102444800,"refresh_in":1200}`)
		default:
			http.NotFound(w, r)
		}
	}))
	defer server.Close()

	service := &OpenAIOAuthService{
		copilotHTTPClient: server.Client(),
		copilotEndpoints: copilotOAuthEndpoints{
			githubUserURL:    server.URL + "/user",
			tokenExchangeURL: server.URL + "/exchange",
		},
	}
	account, err := service.CreateCopilotAccountFromGitHubToken(
		context.Background(),
		CopilotAccountSettings{
			Name:        "manual copilot",
			Concurrency: 8,
			Priority:    100,
			BillingPAT:  "manual-billing-pat",
		},
		" manual-github-token ",
		func(_ context.Context, settings CopilotAccountSettings, credentials, _ map[string]any) (*Account, error) {
			require.Equal(t, "manual-user", settings.BillingUsername)
			require.Equal(t, "manual-user", credentials["billing_username"])
			require.Equal(t, "manual-billing-pat", credentials["billing_pat"])
			require.Equal(t, CopilotOAuthProfile, credentials["oauth_profile"])
			return &Account{ID: 91, Name: settings.Name, Credentials: credentials}, nil
		},
	)
	require.NoError(t, err)
	require.EqualValues(t, 91, account.ID)
}

type copilotProxyRepositoryStub struct {
	ProxyRepository
	proxy *Proxy
	calls atomic.Int32
}

func (s *copilotProxyRepositoryStub) GetByID(_ context.Context, id int64) (*Proxy, error) {
	s.calls.Add(1)
	if s.proxy == nil || s.proxy.ID != id {
		return nil, ErrProxyNotFound
	}
	return s.proxy, nil
}

func TestRefreshCopilotAccountTokenUsesSelectedProxy(t *testing.T) {
	proxyServer := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		require.Equal(t, "copilot.test", r.URL.Hostname())
		require.Equal(t, "/exchange", r.URL.Path)
		require.Equal(t, "token refresh-github-token", r.Header.Get("Authorization"))
		w.Header().Set("Content-Type", "application/json")
		_, _ = io.WriteString(w, `{"token":"proxy-refreshed-copilot-token","expires_at":4102444800,"refresh_in":1200}`)
	}))
	defer proxyServer.Close()

	parsedProxyURL, err := url.Parse(proxyServer.URL)
	require.NoError(t, err)
	proxyPort, err := strconv.Atoi(parsedProxyURL.Port())
	require.NoError(t, err)
	proxyID := int64(12)
	proxyRepo := &copilotProxyRepositoryStub{proxy: &Proxy{
		ID:       proxyID,
		Protocol: parsedProxyURL.Scheme,
		Host:     parsedProxyURL.Hostname(),
		Port:     proxyPort,
		Status:   StatusActive,
	}}
	service := &OpenAIOAuthService{
		proxyRepo: proxyRepo,
		copilotEndpoints: copilotOAuthEndpoints{
			tokenExchangeURL: "http://copilot.test/exchange",
		},
	}
	account := newCopilotGatewayTestAccount()
	account.ProxyID = &proxyID
	account.Credentials["github_access_token"] = "refresh-github-token"

	info, err := service.RefreshAccountToken(context.Background(), account)
	require.NoError(t, err)
	require.Equal(t, "proxy-refreshed-copilot-token", info.AccessToken)
	require.EqualValues(t, 1, proxyRepo.calls.Load())
}
