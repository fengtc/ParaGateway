package service

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"net/http/httptest"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/stretchr/testify/require"
)

func TestValidateCopilotBillingPATParsesCamelCaseUsageAndRedactsPAT(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		require.Equal(t, "/users/octocat/settings/billing/ai_credit/usage", r.URL.Path)
		require.NotEmpty(t, r.URL.Query().Get("year"))
		require.NotEmpty(t, r.URL.Query().Get("month"))
		require.Equal(t, "Bearer billing-secret", r.Header.Get("Authorization"))
		w.Header().Set("Content-Type", "application/json")
		_, _ = io.WriteString(w, `{
			"timePeriod":{"year":2026,"month":8},
			"user":"octocat",
			"usageItems":[
				{"grossQuantity":12.5,"grossAmount":3.25,"netQuantity":10.0,"netAmount":2.75},
				{"grossQuantity":7.5,"grossAmount":1.75,"netQuantity":6.0,"netAmount":1.25}
			]
		}`)
	}))
	defer server.Close()

	service := &OpenAIOAuthService{
		copilotEndpoints: copilotOAuthEndpoints{billingAPIBaseURL: server.URL},
	}
	result, err := service.ValidateCopilotBillingPAT(context.Background(), " octocat ", " billing-secret ", nil)
	require.NoError(t, err)
	require.True(t, result.Valid)
	require.Equal(t, "octocat", result.Username)
	require.EqualValues(t, 2, result.ItemsCount)
	require.Equal(t, 20.0, result.GrossQuantity)
	require.Equal(t, 5.0, result.GrossAmount)
	require.Equal(t, 16.0, result.NetQuantity)
	require.Equal(t, 4.0, result.NetAmount)

	payload, err := json.Marshal(result)
	require.NoError(t, err)
	require.NotContains(t, string(payload), "billing-secret")
}

func TestGetCopilotBillingUsageSnapshotAggregatesAndCaches(t *testing.T) {
	copilotBillingUsageCache.Clear()
	t.Cleanup(copilotBillingUsageCache.Clear)

	var calls atomic.Int32
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		require.Equal(t, "/users/octocat/settings/billing/ai_credit/usage", r.URL.Path)
		require.NotEmpty(t, r.URL.Query().Get("year"))
		require.NotEmpty(t, r.URL.Query().Get("month"))
		require.Equal(t, "Bearer billing-snapshot-token", r.Header.Get("Authorization"))
		w.Header().Set("Content-Type", "application/json")
		_, _ = io.WriteString(w, "{\"user\":\"octocat\",\"usageItems\":[{\"grossQuantity\":120.5,\"grossAmount\":1.25,\"netQuantity\":118,\"netAmount\":1.1},{\"grossQuantity\":79.5,\"grossAmount\":0.75,\"netQuantity\":78,\"netAmount\":0.7}]}")
	}))
	defer server.Close()

	account := newCopilotGatewayTestAccount()
	account.ID = 9201
	account.Credentials["billing_username"] = "octocat"
	account.Credentials["billing_pat"] = "billing-snapshot-token"
	oauthService := &OpenAIOAuthService{
		copilotEndpoints: copilotOAuthEndpoints{billingAPIBaseURL: server.URL},
	}

	first := oauthService.GetCopilotBillingUsageSnapshot(context.Background(), account)
	require.NotNil(t, first)
	require.Equal(t, "octocat", first.Username)
	require.Equal(t, 200.0, first.GrossQuantity)
	require.Equal(t, 196.0, first.NetQuantity)
	require.EqualValues(t, 2, first.ItemsCount)

	second := oauthService.GetCopilotBillingUsageSnapshot(context.Background(), account)
	require.Same(t, first, second)
	require.EqualValues(t, 1, calls.Load())

	payload, err := json.Marshal(first)
	require.NoError(t, err)
	require.NotContains(t, string(payload), "billing-snapshot-token")
}
func TestCopilotBillingGuardDisabledStillHonorsAuthoritativeQuota(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)
	account := newCopilotGatewayTestAccount()
	account.ID = 1201
	account.Credentials["billing_username"] = "octocat"
	account.Credentials["billing_pat"] = "billing-token"
	account.Extra = map[string]any{
		"billing_credit_limit":        1000.0,
		"billing_safety_margin":       100.0,
		"billing_auto_pause_disabled": true,
	}
	fetchCalls := 0
	fetch := func(context.Context, string, string, string, int, int) (float64, error) {
		fetchCalls++
		return 950, nil
	}

	skip, _, stopLimit := shouldSkipCopilotAccountForBillingWithFetcher(context.Background(), account, fetch)
	require.False(t, skip)
	require.Equal(t, 900.0, stopLimit)
	require.Zero(t, fetchCalls)

	require.True(t, markCopilotBillingGuardExhausted(account))
	skip, used, stopLimit := shouldSkipCopilotAccountForBillingWithFetcher(context.Background(), account, fetch)
	require.True(t, skip)
	require.Equal(t, stopLimit, used)
	require.Zero(t, fetchCalls)
}

func TestCopilotBillingGuardUsesNearLimitTTL(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)
	account := newCopilotGatewayTestAccount()
	account.ID = 1202
	account.Credentials["billing_username"] = "octocat"
	account.Credentials["billing_pat"] = "billing-token"
	account.Extra = map[string]any{
		"billing_credit_limit":  1000.0,
		"billing_safety_margin": 100.0,
	}
	now := time.Now().UTC()
	fetchCalls := 0
	fetch := func(context.Context, string, string, string, int, int) (float64, error) {
		fetchCalls++
		return 850, nil
	}

	skip, used, stopLimit := shouldSkipCopilotAccountForBillingWithFetcher(context.Background(), account, fetch)
	require.False(t, skip)
	require.Equal(t, 850.0, used)
	require.Equal(t, 900.0, stopLimit)
	require.EqualValues(t, 1, fetchCalls)

	cached, ok := copilotBillingGuardCache.Load(copilotBillingGuardCacheKey(account.ID, "billing-token", now))
	require.True(t, ok)
	entry, ok := cached.(copilotBillingGuardCacheEntry)
	require.True(t, ok)
	remaining := time.Until(entry.expiresAt)
	require.Greater(t, remaining, 0*time.Second)
	require.LessOrEqual(t, remaining, copilotBillingNearLimitCacheTTL)
}

func TestCopilotBillingGuardCoalescesConcurrentColdFetches(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)
	account := newCopilotGatewayTestAccount()
	account.ID = 1203
	account.Credentials["billing_username"] = "octocat"
	account.Credentials["billing_pat"] = "billing-token-concurrent"

	var fetchCalls atomic.Int32
	fetch := func(context.Context, string, string, string, int, int) (float64, error) {
		fetchCalls.Add(1)
		time.Sleep(50 * time.Millisecond)
		return 100, nil
	}

	const callers = 12
	start := make(chan struct{})
	var wg sync.WaitGroup
	wg.Add(callers)
	for range callers {
		go func() {
			defer wg.Done()
			<-start
			skip, _, _ := shouldSkipCopilotAccountForBillingWithFetcher(context.Background(), account, fetch)
			require.False(t, skip)
		}()
	}
	close(start)
	wg.Wait()

	require.EqualValues(t, 1, fetchCalls.Load())
}

func TestCopilotBillingGuardCachesColdFetchFailure(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)
	account := newCopilotGatewayTestAccount()
	account.ID = 1204
	account.Credentials["billing_username"] = "octocat"
	account.Credentials["billing_pat"] = "billing-token-failure"

	fetchCalls := 0
	fetch := func(context.Context, string, string, string, int, int) (float64, error) {
		fetchCalls++
		return 0, errors.New("billing unavailable")
	}

	for range 2 {
		skip, _, _ := shouldSkipCopilotAccountForBillingWithFetcher(context.Background(), account, fetch)
		require.False(t, skip)
	}
	require.Equal(t, 1, fetchCalls)
}
