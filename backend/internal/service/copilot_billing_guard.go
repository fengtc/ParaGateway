package service

import (
	"context"
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"net/url"
	"strings"
	"sync"
	"time"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"golang.org/x/sync/singleflight"
)

const (
	DefaultCopilotBillingCreditLimit  = 20000.0
	DefaultCopilotBillingSafetyMargin = 200.0

	copilotBillingNormalCacheTTL       = 10 * time.Minute
	copilotBillingNearLimitCacheTTL    = time.Minute
	copilotBillingExhaustedCacheTTL    = 30 * time.Minute
	copilotBillingFetchFailureRetryTTL = time.Minute
	copilotBillingPrefetchConcurrency  = 8

	CopilotMonthlyQuotaExceededReason = "copilot_monthly_quota_exceeded"
)

type githubBillingUsageResponse struct {
	TimePeriod map[string]any           `json:"timePeriod,omitempty"`
	User       string                   `json:"user,omitempty"`
	UsageItems []githubBillingUsageItem `json:"usageItems"`
}

type githubBillingUsageItem struct {
	GrossQuantity float64 `json:"grossQuantity"`
	GrossAmount   float64 `json:"grossAmount"`
	NetQuantity   float64 `json:"netQuantity"`
	NetAmount     float64 `json:"netAmount"`
}

// CopilotBillingValidationResult contains only aggregate usage metadata. The
// PAT is intentionally absent from this response type.
type CopilotBillingValidationResult struct {
	Valid         bool           `json:"valid"`
	Username      string         `json:"username"`
	Period        map[string]any `json:"period,omitempty"`
	ItemsCount    int            `json:"items_count"`
	GrossQuantity float64        `json:"gross_quantity"`
	GrossAmount   float64        `json:"gross_amount"`
	NetQuantity   float64        `json:"net_quantity"`
	NetAmount     float64        `json:"net_amount"`
}

// CopilotBillingUsageSnapshot is the safe, aggregate monthly Billing payload
// exposed to the admin account list. It intentionally contains no PAT or
// other credential material.
type CopilotBillingUsageSnapshot struct {
	Username      string  `json:"username"`
	Period        string  `json:"period"`
	ItemsCount    int     `json:"items_count"`
	GrossQuantity float64 `json:"gross_quantity"`
	GrossAmount   float64 `json:"gross_amount"`
	NetQuantity   float64 `json:"net_quantity"`
	NetAmount     float64 `json:"net_amount"`
	FetchedAt     string  `json:"fetched_at"`
}

type copilotBillingUsageCacheEntry struct {
	snapshot  *CopilotBillingUsageSnapshot
	expiresAt time.Time
}

var copilotBillingUsageCache sync.Map
var copilotBillingUsageFetchGroup singleflight.Group

// GetCopilotBillingUsageSnapshot returns the current month's aggregate GitHub
// Billing usage for a Copilot account. A short-lived, credential-scoped cache
// prevents every account-list refresh from issuing another upstream request.
func (s *OpenAIOAuthService) GetCopilotBillingUsageSnapshot(ctx context.Context, account *Account) *CopilotBillingUsageSnapshot {
	if s == nil || account == nil || !account.IsGitHubCopilot() {
		return nil
	}
	username, token := copilotBillingGuardCredentials(account)
	if username == "" || token == "" {
		return nil
	}
	s.ensureCopilotRuntime()
	now := time.Now().UTC()
	period := fmt.Sprintf("%04d-%02d", now.Year(), int(now.Month()))
	tokenHash := sha256.Sum256([]byte(token))
	proxyURL := copilotBillingProxyURL(account)
	baseURL := strings.TrimRight(strings.TrimSpace(s.copilotEndpoints.billingAPIBaseURL), "/")
	cacheKey := fmt.Sprintf("%d:%s:%s:%s:%s:%x", account.ID, period, username, baseURL, proxyURL, tokenHash[:8])
	if cached, ok := copilotBillingUsageCache.Load(cacheKey); ok {
		if entry, valid := cached.(copilotBillingUsageCacheEntry); valid && entry.snapshot != nil && now.Before(entry.expiresAt) {
			return entry.snapshot
		}
		copilotBillingUsageCache.Delete(cacheKey)
	}

	result, err, _ := copilotBillingUsageFetchGroup.Do(cacheKey, func() (any, error) {
		fetchCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
		defer cancel()
		usage, _, _, fetchErr := fetchGitHubBillingAIUsage(
			fetchCtx,
			s.copilotEndpoints.billingAPIBaseURL,
			username,
			token,
			proxyURL,
			now.Year(),
			int(now.Month()),
		)
		if fetchErr != nil {
			return nil, fetchErr
		}
		return summarizeCopilotBillingUsage(usage, username, period, now), nil
	})
	if err != nil {
		slog.Debug("copilot_billing_usage_fetch_failed", "account_id", account.ID, "error", err)
		return nil
	}
	snapshot, ok := result.(*CopilotBillingUsageSnapshot)
	if !ok || snapshot == nil {
		return nil
	}
	copilotBillingUsageCache.Store(cacheKey, copilotBillingUsageCacheEntry{
		snapshot:  snapshot,
		expiresAt: now.Add(copilotBillingNormalCacheTTL),
	})
	return snapshot
}

func summarizeCopilotBillingUsage(
	usage *githubBillingUsageResponse,
	fallbackUsername string,
	period string,
	fetchedAt time.Time,
) *CopilotBillingUsageSnapshot {
	if usage == nil {
		return nil
	}
	username := strings.TrimSpace(usage.User)
	if username == "" {
		username = strings.TrimSpace(fallbackUsername)
	}
	snapshot := &CopilotBillingUsageSnapshot{
		Username:   username,
		Period:     period,
		ItemsCount: len(usage.UsageItems),
		FetchedAt:  fetchedAt.UTC().Format(time.RFC3339),
	}
	for _, item := range usage.UsageItems {
		snapshot.GrossQuantity += item.GrossQuantity
		snapshot.GrossAmount += item.GrossAmount
		snapshot.NetQuantity += item.NetQuantity
		snapshot.NetAmount += item.NetAmount
	}
	return snapshot
}

// ValidateCopilotBillingPAT verifies a fine-grained PAT against GitHub's AI
// Credits endpoint through the selected proxy.
func (s *OpenAIOAuthService) ValidateCopilotBillingPAT(
	ctx context.Context,
	username string,
	billingPAT string,
	proxyID *int64,
) (*CopilotBillingValidationResult, error) {
	if s == nil {
		return nil, infraerrors.New(http.StatusInternalServerError, "COPILOT_RUNTIME_UNAVAILABLE", "GitHub Copilot OAuth 服务不可用")
	}
	s.ensureCopilotRuntime()
	username = strings.TrimSpace(username)
	billingPAT = strings.TrimSpace(billingPAT)
	if username == "" || billingPAT == "" {
		return nil, infraerrors.New(http.StatusBadRequest, "COPILOT_BILLING_CREDENTIALS_REQUIRED", "GitHub 用户名和 Billing PAT 不能为空")
	}
	if len(username) > 100 || strings.ContainsAny(username, "/?#") {
		return nil, infraerrors.New(http.StatusBadRequest, "COPILOT_BILLING_USERNAME_INVALID", "GitHub Billing 用户名无效")
	}
	proxyURL, err := s.resolveCopilotProxyURL(ctx, proxyID)
	if err != nil {
		return nil, err
	}
	now := time.Now().UTC()
	usage, statusCode, message, err := fetchGitHubBillingAIUsage(
		ctx,
		s.copilotEndpoints.billingAPIBaseURL,
		username,
		billingPAT,
		proxyURL,
		now.Year(),
		int(now.Month()),
	)
	if err != nil {
		switch statusCode {
		case http.StatusUnauthorized:
			return nil, infraerrors.New(http.StatusUnauthorized, "COPILOT_BILLING_PAT_INVALID", "GitHub Billing PAT 无效或已过期").WithCause(err)
		case http.StatusForbidden:
			return nil, infraerrors.New(http.StatusForbidden, "COPILOT_BILLING_PAT_FORBIDDEN", "GitHub Billing PAT 无权读取 AI Credits，请启用 Plan 只读权限").WithCause(err)
		case http.StatusNotFound:
			return nil, infraerrors.New(http.StatusNotFound, "COPILOT_BILLING_USER_NOT_FOUND", "未找到该 GitHub 用户的 AI Credits 用量").WithCause(err)
		default:
			if strings.TrimSpace(message) == "" {
				message = "GitHub Billing API 请求失败"
			}
			return nil, infraerrors.New(http.StatusBadGateway, "COPILOT_BILLING_UPSTREAM_FAILED", message).WithCause(err)
		}
	}
	result := &CopilotBillingValidationResult{
		Valid:      true,
		Username:   strings.TrimSpace(usage.User),
		Period:     usage.TimePeriod,
		ItemsCount: len(usage.UsageItems),
	}
	if result.Username == "" {
		result.Username = username
	}
	for _, item := range usage.UsageItems {
		result.GrossQuantity += item.GrossQuantity
		result.GrossAmount += item.GrossAmount
		result.NetQuantity += item.NetQuantity
		result.NetAmount += item.NetAmount
	}
	return result, nil
}

type copilotBillingGuardCacheEntry struct {
	usedCredits   float64
	expiresAt     time.Time
	forceSkip     bool
	authoritative bool
}

var copilotBillingGuardCache sync.Map
var copilotBillingGuardFetchGroup singleflight.Group

type copilotBillingGuardFetcher func(context.Context, string, string, string, int, int) (float64, error)

func shouldSkipCopilotAccountForBilling(ctx context.Context, account *Account) (bool, float64, float64) {
	return shouldSkipCopilotAccountForBillingWithFetcher(ctx, account, fetchCopilotBillingGuardUsedCredits)
}

func prefetchCopilotBillingGuards(ctx context.Context, accounts []Account) {
	prefetchCopilotBillingGuardsWithFetcher(
		ctx,
		accounts,
		copilotBillingPrefetchConcurrency,
		fetchCopilotBillingGuardUsedCredits,
	)
}

func prefetchCopilotBillingGuardsWithFetcher(
	ctx context.Context,
	accounts []Account,
	maxConcurrency int,
	fetch copilotBillingGuardFetcher,
) {
	if len(accounts) == 0 || maxConcurrency <= 0 || fetch == nil {
		return
	}

	candidates := make([]*Account, 0, len(accounts))
	for i := range accounts {
		account := &accounts[i]
		if !account.IsGitHubCopilot() || copilotBillingAutoPauseDisabled(account) || copilotBillingCreditLimit(account) <= 0 {
			continue
		}
		username, token := copilotBillingGuardCredentials(account)
		if username == "" || token == "" {
			continue
		}
		candidates = append(candidates, account)
	}
	if len(candidates) == 0 {
		return
	}

	workerCount := min(maxConcurrency, len(candidates))
	jobs := make(chan *Account, len(candidates))
	for _, account := range candidates {
		jobs <- account
	}
	close(jobs)

	var workers sync.WaitGroup
	workers.Add(workerCount)
	for range workerCount {
		go func() {
			defer workers.Done()
			for account := range jobs {
				if ctx.Err() != nil {
					return
				}
				shouldSkipCopilotAccountForBillingWithFetcher(ctx, account, fetch)
			}
		}()
	}
	workers.Wait()
}

func shouldSkipCopilotAccountForBillingWithFetcher(
	ctx context.Context,
	account *Account,
	fetch copilotBillingGuardFetcher,
) (bool, float64, float64) {
	if account == nil || !account.IsGitHubCopilot() {
		return false, 0, 0
	}
	configuredLimit := copilotBillingCreditLimit(account)
	if configuredLimit <= 0 {
		return false, 0, 0
	}
	safetyMargin := copilotBillingSafetyMargin(account, configuredLimit)
	stopLimit := copilotBillingGuardStopLimitWithMargin(configuredLimit, safetyMargin)
	now := time.Now().UTC()

	authoritativeKey := copilotBillingGuardAuthoritativeKey(account.ID, now)
	if cached, ok := copilotBillingGuardCache.Load(authoritativeKey); ok {
		entry, valid := cached.(copilotBillingGuardCacheEntry)
		if valid && now.Before(entry.expiresAt) {
			return true, entry.usedCredits, stopLimit
		}
		copilotBillingGuardCache.Delete(authoritativeKey)
	}

	username, token := copilotBillingGuardCredentials(account)
	if token == "" || copilotBillingAutoPauseDisabled(account) {
		return false, 0, stopLimit
	}
	cacheKey := copilotBillingGuardCacheKey(account.ID, token, now)
	var lastGood *copilotBillingGuardCacheEntry
	if cached, ok := copilotBillingGuardCache.Load(cacheKey); ok {
		entry, valid := cached.(copilotBillingGuardCacheEntry)
		if valid {
			lastGood = &entry
			if now.Before(entry.expiresAt) {
				return entry.forceSkip || entry.usedCredits >= stopLimit, entry.usedCredits, stopLimit
			}
		} else {
			copilotBillingGuardCache.Delete(cacheKey)
		}
	}
	if username == "" || fetch == nil {
		return false, 0, stopLimit
	}

	fetchResult, err, _ := copilotBillingGuardFetchGroup.Do(cacheKey, func() (any, error) {
		fetchCtx, cancel := context.WithTimeout(ctx, 3*time.Second)
		defer cancel()
		return fetch(fetchCtx, username, token, copilotBillingProxyURL(account), now.Year(), int(now.Month()))
	})
	if err != nil {
		slog.Warn("copilot_billing_guard_fetch_failed", "account_id", account.ID, "error", err)
		if lastGood != nil {
			lastGood.expiresAt = now.Add(copilotBillingFetchFailureRetryTTL)
			if copilotBillingGuardNearLimit(lastGood.usedCredits, stopLimit, safetyMargin) {
				lastGood.forceSkip = true
			}
			copilotBillingGuardCache.Store(cacheKey, *lastGood)
			return lastGood.forceSkip || lastGood.usedCredits >= stopLimit, lastGood.usedCredits, stopLimit
		}
		// Fail open for availability, but remember the cold-fetch failure briefly.
		// Without this negative cache every request retries GitHub immediately and
		// a Billing API outage adds up to three seconds per Copilot candidate.
		copilotBillingGuardCache.Store(cacheKey, copilotBillingGuardCacheEntry{
			expiresAt: now.Add(copilotBillingFetchFailureRetryTTL),
		})
		return false, 0, stopLimit
	}
	used, ok := fetchResult.(float64)
	if !ok {
		slog.Warn("copilot_billing_guard_fetch_invalid_result", "account_id", account.ID)
		return false, 0, stopLimit
	}

	copilotBillingGuardCache.Store(cacheKey, copilotBillingGuardCacheEntry{
		usedCredits: used,
		expiresAt:   now.Add(copilotBillingGuardCacheTTL(used, stopLimit, safetyMargin)),
	})
	return used >= stopLimit, used, stopLimit
}

// markCopilotBillingGuardExhausted immediately mirrors an authoritative
// upstream 402 into the process-local guard, including accounts without a PAT.
func markCopilotBillingGuardExhausted(account *Account) bool {
	if account == nil || !account.IsGitHubCopilot() || account.ID <= 0 {
		return false
	}
	limit := copilotBillingCreditLimit(account)
	stopLimit := copilotBillingGuardStopLimitWithMargin(limit, copilotBillingSafetyMargin(account, limit))
	now := time.Now().UTC()
	copilotBillingGuardCache.Store(copilotBillingGuardAuthoritativeKey(account.ID, now), copilotBillingGuardCacheEntry{
		usedCredits:   stopLimit,
		expiresAt:     nextCopilotMonthlyQuotaReset(now),
		forceSkip:     true,
		authoritative: true,
	})
	return true
}

func copilotBillingGuardCacheKey(accountID int64, token string, now time.Time) string {
	tokenHash := sha256.Sum256([]byte(token))
	return fmt.Sprintf("%d:%04d-%02d:%x", accountID, now.UTC().Year(), int(now.UTC().Month()), tokenHash[:8])
}

func copilotBillingGuardAuthoritativeKey(accountID int64, now time.Time) string {
	return fmt.Sprintf("%d:%04d-%02d:authoritative", accountID, now.UTC().Year(), int(now.UTC().Month()))
}

func copilotBillingGuardStopLimitWithMargin(configuredLimit, safetyMargin float64) float64 {
	if configuredLimit <= 0 {
		return 0
	}
	if safetyMargin < 0 {
		safetyMargin = 0
	}
	if safetyMargin > configuredLimit {
		safetyMargin = configuredLimit
	}
	return configuredLimit - safetyMargin
}

func copilotBillingGuardCacheTTL(used, stopLimit, safetyMargin float64) time.Duration {
	if used >= stopLimit {
		return copilotBillingExhaustedCacheTTL
	}
	if copilotBillingGuardNearLimit(used, stopLimit, safetyMargin) {
		return copilotBillingNearLimitCacheTTL
	}
	return copilotBillingNormalCacheTTL
}

func copilotBillingGuardNearLimit(used, stopLimit, safetyMargin float64) bool {
	return safetyMargin > 0 && used >= stopLimit-safetyMargin
}

func copilotBillingGuardCredentials(account *Account) (string, string) {
	if account == nil || account.Credentials == nil {
		return "", ""
	}
	username, _ := account.Credentials["billing_username"].(string)
	token, _ := account.Credentials["billing_pat"].(string)
	return strings.TrimSpace(username), strings.TrimSpace(token)
}

func copilotBillingCreditLimit(account *Account) float64 {
	if account != nil && account.Extra != nil {
		if parsed, ok := parseCopilotBillingFloat(account.Extra["billing_credit_limit"]); ok && parsed > 0 {
			return parsed
		}
	}
	return DefaultCopilotBillingCreditLimit
}

func copilotBillingSafetyMargin(account *Account, configuredLimit float64) float64 {
	margin := DefaultCopilotBillingSafetyMargin
	if account != nil && account.Extra != nil {
		if parsed, ok := parseCopilotBillingFloat(account.Extra["billing_safety_margin"]); ok && parsed >= 0 {
			margin = parsed
		}
	}
	if margin > configuredLimit {
		return configuredLimit
	}
	return margin
}

func copilotBillingAutoPauseDisabled(account *Account) bool {
	if account == nil || account.Extra == nil {
		return false
	}
	disabled, _ := account.Extra["billing_auto_pause_disabled"].(bool)
	return disabled
}

func parseCopilotBillingFloat(value any) (float64, bool) {
	switch typed := value.(type) {
	case float64:
		return typed, true
	case float32:
		return float64(typed), true
	case int:
		return float64(typed), true
	case int64:
		return float64(typed), true
	case json.Number:
		parsed, err := typed.Float64()
		return parsed, err == nil
	default:
		return 0, false
	}
}

func copilotBillingProxyURL(account *Account) string {
	if account == nil || account.ProxyID == nil || account.Proxy == nil {
		return ""
	}
	return account.Proxy.URL()
}

func fetchCopilotBillingGuardUsedCredits(
	ctx context.Context,
	username string,
	token string,
	proxyURL string,
	year int,
	month int,
) (float64, error) {
	usage, _, _, err := fetchGitHubBillingAIUsage(ctx, copilotGitHubAPIBaseURL, username, token, proxyURL, year, month)
	if err != nil {
		return 0, err
	}
	var used float64
	for _, item := range usage.UsageItems {
		used += item.GrossQuantity
	}
	return used, nil
}

func fetchGitHubBillingAIUsage(
	ctx context.Context,
	baseURL string,
	username string,
	token string,
	proxyURL string,
	year int,
	month int,
) (*githubBillingUsageResponse, int, string, error) {
	endpoint := strings.TrimRight(strings.TrimSpace(baseURL), "/") +
		fmt.Sprintf("/users/%s/settings/billing/ai_credit/usage?year=%d&month=%d", url.PathEscape(strings.TrimSpace(username)), year, month)
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, endpoint, nil)
	if err != nil {
		return nil, 0, "", err
	}
	request.Header.Set("Accept", "application/vnd.github+json")
	request.Header.Set("Authorization", "Bearer "+strings.TrimSpace(token))
	request.Header.Set("X-GitHub-Api-Version", "2026-03-10")
	request.Header.Set("User-Agent", "ParaGateway-Copilot-Billing")

	client, err := copilotHTTPClientForProxy(nil, proxyURL, 15*time.Second)
	if err != nil {
		return nil, 0, "", err
	}
	response, err := client.Do(request)
	if err != nil {
		return nil, 0, "", err
	}
	defer response.Body.Close()
	payload, err := io.ReadAll(io.LimitReader(response.Body, copilotMaxResponseBytes+1))
	if err != nil {
		return nil, response.StatusCode, "", err
	}
	if len(payload) > copilotMaxResponseBytes {
		return nil, response.StatusCode, "", fmt.Errorf("GitHub Billing response too large")
	}
	if response.StatusCode < 200 || response.StatusCode >= 300 {
		var errorPayload struct {
			Message string
		}
		_ = json.Unmarshal(payload, &errorPayload)
		if strings.TrimSpace(errorPayload.Message) == "" {
			errorPayload.Message = response.Status
		}
		return nil, response.StatusCode, errorPayload.Message, fmt.Errorf("GitHub Billing returned status %d", response.StatusCode)
	}
	var usage githubBillingUsageResponse
	if err := json.Unmarshal(payload, &usage); err != nil {
		return nil, response.StatusCode, "", err
	}
	return &usage, response.StatusCode, "", nil
}

func nextCopilotMonthlyQuotaReset(now time.Time) time.Time {
	now = now.UTC()
	return time.Date(now.Year(), now.Month()+1, 1, 0, 0, 0, 0, time.UTC)
}
