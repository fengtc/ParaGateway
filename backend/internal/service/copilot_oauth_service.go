package service

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"regexp"
	"strconv"
	"strings"
	"sync"
	"time"
	"unicode/utf8"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"github.com/Wei-Shaw/sub2api/internal/pkg/proxyutil"
	"github.com/google/uuid"
)

var (
	copilotHyphenatedClaudeModel = regexp.MustCompile(`^(claude-(?:sonnet|opus|haiku)-[0-9]+)-([0-9]+)(-?[0-9]{8})?$`)
	copilotDottedClaudeModel     = regexp.MustCompile(`^(claude-(?:sonnet|opus|haiku)-[0-9]+)\.([0-9]+)(.*)$`)
)

const (
	CopilotOAuthProfile               = "github_copilot"
	CopilotAPIBaseURL                 = "https://api.githubcopilot.com"
	copilotGitHubAPIBaseURL           = "https://api.github.com"
	copilotDeviceClientID             = "Iv1.b507a08c87ecfe98"
	copilotRequestTimeout             = 30 * time.Second
	copilotMaxResponseBytes           = 128 * 1024
	copilotFlowRetention              = 24 * time.Hour
	copilotAccountCreationRetryWindow = 30 * time.Minute
	copilotMaxPendingFlowsPerAdmin    = 3

	CopilotOAuthStatusPending   = "pending"
	CopilotOAuthStatusCompleted = "completed"
	CopilotOAuthStatusFailed    = "failed"
	CopilotOAuthStatusExpired   = "expired"
)

// copilotDefaultModels mirrors the fallback catalog used by the standalone
// Worker implementation. GitHub may return a smaller account-specific list;
// these IDs are still useful when /v1/models is unavailable during admin setup.
var copilotDefaultModels = []string{
	"gpt-4o",
	"gpt-4o-mini",
	"gpt-4.1",
	"gpt-4.1-mini",
	"gpt-4.1-nano",
	"o4-mini",
	"o3-mini",
	"claude-sonnet-4",
	"claude-sonnet-4-5",
	"claude-sonnet-4-6",
	"claude-sonnet-5",
	"claude-opus-4-5",
	"claude-opus-4-6",
	"claude-haiku-4-5",
	"claude-3.5-sonnet",
	"gemini-2.0-flash-001",
}

// CopilotDefaultModels returns a defensive copy so callers cannot mutate the
// process-wide fallback catalog.
func CopilotDefaultModels() []string {
	return append([]string(nil), copilotDefaultModels...)
}

// buildCopilotAPIURL appends one of Copilot's root-level API paths. Unlike a
// standard OpenAI-compatible provider, api.githubcopilot.com exposes
// /chat/completions, /responses and /models without a /v1 prefix.
func buildCopilotAPIURL(baseURL, endpoint string) string {
	baseURL = strings.TrimSpace(baseURL)
	endpoint = "/" + strings.TrimLeft(strings.TrimSpace(endpoint), "/")
	parsed, err := url.Parse(baseURL)
	if err != nil || parsed.Scheme == "" || parsed.Host == "" {
		return strings.TrimRight(baseURL, "/") + endpoint
	}
	path := strings.TrimRight(parsed.Path, "/")
	if !strings.HasSuffix(path, endpoint) {
		path += endpoint
	}
	parsed.Path = path
	parsed.RawPath = ""
	parsed.Fragment = ""
	return parsed.String()
}

func copilotUnsupportedEndpointError(endpoint string) error {
	return infraerrors.Newf(
		http.StatusNotImplemented,
		"COPILOT_ENDPOINT_UNSUPPORTED",
		"GitHub Copilot 账号不支持 %s；请使用 Chat Completions、Responses 文本或 Messages 文本接口",
		strings.TrimSpace(endpoint),
	)
}

type copilotOAuthEndpoints struct {
	deviceCodeURL     string
	accessTokenURL    string
	githubUserURL     string
	tokenExchangeURL  string
	billingAPIBaseURL string
}

func defaultCopilotOAuthEndpoints() copilotOAuthEndpoints {
	return copilotOAuthEndpoints{
		deviceCodeURL:     "https://github.com/login/device/code",
		accessTokenURL:    "https://github.com/login/oauth/access_token",
		githubUserURL:     "https://api.github.com/user",
		tokenExchangeURL:  "https://api.github.com/copilot_internal/v2/token",
		billingAPIBaseURL: copilotGitHubAPIBaseURL,
	}
}

// ensureCopilotRuntime lazily initializes the in-process device-flow runtime.
// A few tests and embedded users instantiate OpenAIOAuthService directly, so
// relying solely on NewOpenAIOAuthService would make an otherwise valid flow
// panic on a nil map or silently target an empty URL. Only missing values are
// filled; injected HTTP clients/endpoints remain untouched.
func (s *OpenAIOAuthService) ensureCopilotRuntime() {
	if s == nil {
		return
	}
	s.copilotRuntimeMu.Lock()
	defer s.copilotRuntimeMu.Unlock()
	if s.copilotFlows == nil {
		s.copilotFlows = make(map[string]*copilotOAuthFlow)
	}
	if s.copilotHTTPClient == nil {
		s.copilotHTTPClient = &http.Client{Timeout: copilotRequestTimeout}
	}
	defaults := defaultCopilotOAuthEndpoints()
	if strings.TrimSpace(s.copilotEndpoints.deviceCodeURL) == "" {
		s.copilotEndpoints.deviceCodeURL = defaults.deviceCodeURL
	}
	if strings.TrimSpace(s.copilotEndpoints.accessTokenURL) == "" {
		s.copilotEndpoints.accessTokenURL = defaults.accessTokenURL
	}
	if strings.TrimSpace(s.copilotEndpoints.githubUserURL) == "" {
		s.copilotEndpoints.githubUserURL = defaults.githubUserURL
	}
	if strings.TrimSpace(s.copilotEndpoints.tokenExchangeURL) == "" {
		s.copilotEndpoints.tokenExchangeURL = defaults.tokenExchangeURL
	}
	if strings.TrimSpace(s.copilotEndpoints.billingAPIBaseURL) == "" {
		s.copilotEndpoints.billingAPIBaseURL = defaults.billingAPIBaseURL
	}
}

// CopilotAccountSettings contains account configuration retained server-side
// while a device flow is pending. BillingPAT is never exposed by flow results.
type CopilotAccountSettings struct {
	Name                     string
	Notes                    *string
	ProxyID                  *int64
	Concurrency              int
	LoadFactor               *int
	Priority                 int
	RateMultiplier           *float64
	GroupIDs                 []int64
	ExpiresAt                *int64
	AutoPauseOnExpired       *bool
	Schedulable              *bool
	ModelMapping             map[string]string
	BillingUsername          string
	BillingPAT               string
	BillingCreditLimit       *float64
	BillingSafetyMargin      *float64
	BillingAutoPauseDisabled *bool
	IdempotencyKey           string
}

type copilotAccountFlowSettings struct {
	CopilotAccountSettings
	proxyURL string
}

// CopilotAccountCreator persists an already validated Copilot account. Both
// credential maps are assembled on the server so tokens never reach a browser.
type CopilotAccountCreator func(context.Context, CopilotAccountSettings, map[string]any, map[string]any) (*Account, error)

type copilotOAuthFlow struct {
	mu                     sync.Mutex
	ID                     string
	AdminUserID            int64
	Settings               copilotAccountFlowSettings
	DeviceCode             string
	GitHubToken            string
	UserCode               string
	VerificationURI        string
	Status                 string
	IntervalSeconds        int
	NextPollAt             time.Time
	ExpiresAt              time.Time
	CreatedAt              time.Time
	AccountCreateExpiresAt time.Time
	CompletedAt            *time.Time
	ProviderAccountID      *int64
}

func (f *copilotOAuthFlow) clearSensitiveState() {
	if f == nil {
		return
	}
	f.DeviceCode = ""
	f.GitHubToken = ""
	f.Settings = copilotAccountFlowSettings{}
}

// CopilotOAuthFlowResult is safe to serialize after the handler converts Account
// through the normal redacting DTO mapper. It never exposes either OAuth token.
type CopilotOAuthFlowResult struct {
	FlowID            string     `json:"flow_id"`
	Profile           string     `json:"profile"`
	Status            string     `json:"status"`
	UserCode          string     `json:"user_code,omitempty"`
	VerificationURI   string     `json:"verification_uri,omitempty"`
	ExpiresAt         time.Time  `json:"expires_at"`
	IntervalSeconds   int        `json:"interval_seconds"`
	NextPollAt        time.Time  `json:"next_poll_at"`
	ProviderAccountID *int64     `json:"provider_account_id,omitempty"`
	CompletedAt       *time.Time `json:"completed_at,omitempty"`
	ProviderAccount   *Account   `json:"-"`
}

type copilotDeviceCodeResponse struct {
	DeviceCode      string `json:"device_code"`
	UserCode        string `json:"user_code"`
	VerificationURI string `json:"verification_uri"`
	ExpiresIn       int    `json:"expires_in"`
	Interval        int    `json:"interval"`
}

type copilotAccessTokenResponse struct {
	AccessToken string `json:"access_token"`
	Error       string `json:"error"`
	Interval    int    `json:"interval"`
}

type copilotGitHubUser struct {
	Login string `json:"login"`
	ID    int64  `json:"id"`
}

type copilotTokenExchangeResponse struct {
	Token     string `json:"token"`
	ExpiresAt int64  `json:"expires_at"`
	RefreshIn int64  `json:"refresh_in"`
}

type copilotTokenInfo struct {
	AccessToken string
	ExpiresAt   time.Time
	RefreshAt   time.Time
}

// StartCopilotOAuthFlow preserves the original service contract for embedded
// callers. New management APIs use StartCopilotOAuthFlowWithSettings.
func (s *OpenAIOAuthService) StartCopilotOAuthFlow(ctx context.Context, adminUserID int64, accountName string) (*CopilotOAuthFlowResult, error) {
	return s.StartCopilotOAuthFlowWithSettings(ctx, adminUserID, CopilotAccountSettings{
		Name:        accountName,
		Concurrency: 8,
		Priority:    100,
	})
}

// StartCopilotOAuthFlowWithSettings starts GitHub's device authorization flow.
// Device codes and all account settings stay server-side and are scoped to the
// authenticated administrator.
func (s *OpenAIOAuthService) StartCopilotOAuthFlowWithSettings(
	ctx context.Context,
	adminUserID int64,
	settings CopilotAccountSettings,
) (*CopilotOAuthFlowResult, error) {
	if s == nil || adminUserID <= 0 {
		return nil, infraerrors.New(http.StatusUnauthorized, "COPILOT_OAUTH_UNAUTHORIZED", "管理员登录状态无效")
	}
	s.ensureCopilotRuntime()
	var err error
	settings, err = normalizeCopilotAccountSettings(settings)
	if err != nil {
		return nil, err
	}
	if err := s.ensureCopilotFlowCapacity(adminUserID, time.Now().UTC()); err != nil {
		return nil, err
	}
	proxyURL, err := s.resolveCopilotProxyURL(ctx, settings.ProxyID)
	if err != nil {
		return nil, err
	}

	values := url.Values{
		"client_id": {copilotDeviceClientID},
		"scope":     {"read:user"},
	}
	var deviceResponse copilotDeviceCodeResponse
	if err := s.copilotRequestJSONWithProxy(ctx, http.MethodPost, s.copilotEndpoints.deviceCodeURL, strings.NewReader(values.Encode()), copilotFormHeaders(), &deviceResponse, proxyURL); err != nil {
		return nil, err
	}
	if strings.TrimSpace(deviceResponse.DeviceCode) == "" || strings.TrimSpace(deviceResponse.UserCode) == "" || strings.TrimSpace(deviceResponse.VerificationURI) == "" {
		return nil, infraerrors.New(http.StatusBadGateway, "COPILOT_DEVICE_OAUTH_INVALID", "GitHub 返回了无效的设备授权响应")
	}
	if deviceResponse.ExpiresIn <= 0 {
		deviceResponse.ExpiresIn = 900
	}
	if deviceResponse.Interval < 5 {
		deviceResponse.Interval = 5
	}

	now := time.Now().UTC()
	flow := &copilotOAuthFlow{
		ID:          uuid.NewString(),
		AdminUserID: adminUserID,
		Settings: copilotAccountFlowSettings{
			CopilotAccountSettings: settings,
			proxyURL:               proxyURL,
		},
		DeviceCode:      deviceResponse.DeviceCode,
		UserCode:        deviceResponse.UserCode,
		VerificationURI: deviceResponse.VerificationURI,
		Status:          CopilotOAuthStatusPending,
		IntervalSeconds: deviceResponse.Interval,
		NextPollAt:      now,
		ExpiresAt:       now.Add(time.Duration(deviceResponse.ExpiresIn) * time.Second),
		CreatedAt:       now,
	}

	s.copilotFlowsMu.Lock()
	s.cleanupCopilotFlowsLocked(now)
	if s.countPendingCopilotFlowsLocked(adminUserID) >= copilotMaxPendingFlowsPerAdmin {
		s.copilotFlowsMu.Unlock()
		flow.clearSensitiveState()
		return nil, infraerrors.New(http.StatusTooManyRequests, "COPILOT_OAUTH_FLOW_LIMIT", "未完成的 Copilot 授权流程过多，请先完成或取消已有流程")
	}
	s.copilotFlows[flow.ID] = flow
	s.copilotFlowsMu.Unlock()
	return copilotOAuthFlowSnapshot(flow), nil
}

// PollCopilotOAuthFlow preserves the original callback shape for embedded
// callers. New management APIs use PollCopilotOAuthFlowWithSettings.
func (s *OpenAIOAuthService) PollCopilotOAuthFlow(
	ctx context.Context,
	adminUserID int64,
	flowID string,
	createAccount func(context.Context, string, map[string]any) (*Account, error),
) (*CopilotOAuthFlowResult, error) {
	return s.PollCopilotOAuthFlowWithSettings(
		ctx,
		adminUserID,
		flowID,
		func(ctx context.Context, settings CopilotAccountSettings, credentials, _ map[string]any) (*Account, error) {
			if createAccount == nil {
				return nil, infraerrors.New(http.StatusInternalServerError, "COPILOT_ACCOUNT_CREATOR_MISSING", "Copilot 账号创建服务不可用")
			}
			return createAccount(ctx, settings.Name, credentials)
		},
	)
}

// PollCopilotOAuthFlowWithSettings advances one device-flow poll. Account
// creation executes while holding the per-flow lock, preventing duplicate
// accounts on concurrent polls.
func (s *OpenAIOAuthService) PollCopilotOAuthFlowWithSettings(
	ctx context.Context,
	adminUserID int64,
	flowID string,
	createAccount CopilotAccountCreator,
) (*CopilotOAuthFlowResult, error) {
	if s == nil || adminUserID <= 0 {
		return nil, infraerrors.New(http.StatusUnauthorized, "COPILOT_OAUTH_UNAUTHORIZED", "管理员登录状态无效")
	}
	s.ensureCopilotRuntime()
	flow := s.getCopilotFlow(flowID, adminUserID)
	if flow == nil {
		return nil, infraerrors.New(http.StatusNotFound, "COPILOT_OAUTH_FLOW_NOT_FOUND", "Copilot 授权流程不存在")
	}
	return s.pollCapturedCopilotOAuthFlowWithSettings(ctx, adminUserID, flow, createAccount)
}

// pollCapturedCopilotOAuthFlowWithSettings advances a flow pointer that was
// resolved while it was still registered. Cancel can remove that flow before
// this function acquires flow.mu, so cancellation must first mark it terminal.
func (s *OpenAIOAuthService) pollCapturedCopilotOAuthFlowWithSettings(
	ctx context.Context,
	adminUserID int64,
	flow *copilotOAuthFlow,
	createAccount CopilotAccountCreator,
) (*CopilotOAuthFlowResult, error) {
	flow.mu.Lock()
	defer flow.mu.Unlock()
	now := time.Now().UTC()
	if flow.Status == CopilotOAuthStatusCompleted || flow.Status == CopilotOAuthStatusFailed || flow.Status == CopilotOAuthStatusExpired {
		return copilotOAuthFlowSnapshot(flow), nil
	}
	if flow.GitHubToken == "" && !now.Before(flow.ExpiresAt) {
		flow.Status = CopilotOAuthStatusExpired
		flow.clearSensitiveState()
		return copilotOAuthFlowSnapshot(flow), nil
	}
	if flow.GitHubToken == "" && now.Before(flow.NextPollAt) {
		return copilotOAuthFlowSnapshot(flow), nil
	}

	githubToken := strings.TrimSpace(flow.GitHubToken)
	if githubToken == "" {
		flow.NextPollAt = now.Add(time.Duration(flow.IntervalSeconds) * time.Second)
		values := url.Values{
			"client_id":   {copilotDeviceClientID},
			"device_code": {flow.DeviceCode},
			"grant_type":  {"urn:ietf:params:oauth:grant-type:device_code"},
		}
		var tokenResponse copilotAccessTokenResponse
		if err := s.copilotRequestJSONWithProxy(ctx, http.MethodPost, s.copilotEndpoints.accessTokenURL, strings.NewReader(values.Encode()), copilotFormHeaders(), &tokenResponse, flow.Settings.proxyURL); err != nil {
			return nil, err
		}

		switch tokenResponse.Error {
		case "authorization_pending":
			return copilotOAuthFlowSnapshot(flow), nil
		case "slow_down":
			flow.IntervalSeconds += 5
			if tokenResponse.Interval > flow.IntervalSeconds {
				flow.IntervalSeconds = tokenResponse.Interval
			}
			flow.NextPollAt = now.Add(time.Duration(flow.IntervalSeconds) * time.Second)
			return copilotOAuthFlowSnapshot(flow), nil
		case "expired_token":
			flow.Status = CopilotOAuthStatusExpired
			flow.clearSensitiveState()
			return copilotOAuthFlowSnapshot(flow), nil
		case "access_denied":
			flow.Status = CopilotOAuthStatusFailed
			flow.clearSensitiveState()
			return copilotOAuthFlowSnapshot(flow), nil
		case "":
			// Continue below.
		default:
			flow.Status = CopilotOAuthStatusFailed
			flow.clearSensitiveState()
			return nil, infraerrors.New(http.StatusBadGateway, "COPILOT_DEVICE_OAUTH_REJECTED", "GitHub 拒绝了 Copilot 授权请求")
		}
		githubToken = strings.TrimSpace(tokenResponse.AccessToken)
		if len(githubToken) < 8 {
			flow.Status = CopilotOAuthStatusFailed
			flow.clearSensitiveState()
			return nil, infraerrors.New(http.StatusBadGateway, "COPILOT_DEVICE_OAUTH_INVALID", "GitHub 返回了无效的授权响应")
		}
		flow.GitHubToken = githubToken
		flow.DeviceCode = ""
		flow.AccountCreateExpiresAt = now.Add(copilotAccountCreationRetryWindow)
	}
	if !flow.AccountCreateExpiresAt.IsZero() && !now.Before(flow.AccountCreateExpiresAt) {
		flow.Status = CopilotOAuthStatusExpired
		flow.clearSensitiveState()
		return copilotOAuthFlowSnapshot(flow), nil
	}

	githubUser, err := s.getCopilotGitHubUserWithProxy(ctx, githubToken, flow.Settings.proxyURL)
	if err != nil {
		return nil, err
	}
	copilotToken, err := s.exchangeCopilotTokenWithProxy(ctx, githubToken, flow.Settings.proxyURL)
	if err != nil {
		return nil, err
	}
	if createAccount == nil {
		return nil, infraerrors.New(http.StatusInternalServerError, "COPILOT_ACCOUNT_CREATOR_MISSING", "Copilot 账号创建服务不可用")
	}

	settings := cloneCopilotAccountSettings(flow.Settings.CopilotAccountSettings)
	settings.IdempotencyKey = "copilot-device:" + strconv.FormatInt(adminUserID, 10) + ":" + flow.ID
	credentials, extra := buildCopilotConfiguredAccount(settings, githubToken, githubUser, copilotToken)
	if strings.TrimSpace(settings.BillingPAT) != "" && strings.TrimSpace(settings.BillingUsername) == "" {
		settings.BillingUsername = strings.TrimSpace(githubUser.Login)
		credentials["billing_username"] = settings.BillingUsername
	}
	account, err := createAccount(ctx, settings, credentials, extra)
	if err != nil {
		return nil, err
	}
	completedAt := time.Now().UTC()
	flow.Status = CopilotOAuthStatusCompleted
	flow.CompletedAt = &completedAt
	flow.ProviderAccountID = &account.ID
	flow.clearSensitiveState()
	result := copilotOAuthFlowSnapshot(flow)
	result.ProviderAccount = account
	return result, nil
}

// ExchangeCopilotToken exchanges a long-lived GitHub OAuth token for the short-
// lived token accepted by api.githubcopilot.com.
func (s *OpenAIOAuthService) ExchangeCopilotToken(ctx context.Context, githubToken string) (*copilotTokenInfo, error) {
	return s.exchangeCopilotTokenWithProxy(ctx, githubToken, "")
}

func (s *OpenAIOAuthService) exchangeCopilotTokenWithProxy(ctx context.Context, githubToken, proxyURL string) (*copilotTokenInfo, error) {
	if s == nil {
		return nil, infraerrors.New(http.StatusInternalServerError, "COPILOT_RUNTIME_UNAVAILABLE", "GitHub Copilot OAuth 服务不可用")
	}
	s.ensureCopilotRuntime()
	githubToken = strings.TrimSpace(githubToken)
	if githubToken == "" {
		return nil, infraerrors.New(http.StatusBadRequest, "COPILOT_GITHUB_TOKEN_REQUIRED", "GitHub OAuth Token 缺失")
	}
	var response copilotTokenExchangeResponse
	if err := s.copilotRequestJSONWithProxy(ctx, http.MethodGet, s.copilotEndpoints.tokenExchangeURL, nil, copilotHeaders(githubToken, true), &response, proxyURL); err != nil {
		return nil, err
	}
	if len(strings.TrimSpace(response.Token)) < 8 {
		return nil, infraerrors.New(http.StatusBadGateway, "COPILOT_TOKEN_EXCHANGE_FAILED", "GitHub 账号没有可用的 Copilot 权限，或 Token 交换失败")
	}

	now := time.Now().UTC()
	expiresAt := now.Add(30 * time.Minute)
	if response.ExpiresAt > 0 {
		expiresAt = time.Unix(response.ExpiresAt, 0).UTC()
	}
	refreshDelay := time.Until(expiresAt)
	if response.RefreshIn > 0 {
		refreshDelay = time.Duration(response.RefreshIn) * time.Second
	}
	refreshAt := now.Add(refreshDelay - time.Minute)
	if refreshAt.Before(now.Add(30 * time.Second)) {
		refreshAt = now.Add(30 * time.Second)
	}
	latestRefresh := expiresAt.Add(-time.Minute)
	if refreshAt.After(latestRefresh) {
		refreshAt = latestRefresh
	}
	return &copilotTokenInfo{AccessToken: response.Token, ExpiresAt: expiresAt, RefreshAt: refreshAt}, nil
}

// CreateCopilotAccountFromGitHubToken validates a manually supplied GitHub
// token, exchanges it for a Copilot token, and persists the account through the
// server-owned callback. No credential material is returned.
func (s *OpenAIOAuthService) CreateCopilotAccountFromGitHubToken(
	ctx context.Context,
	settings CopilotAccountSettings,
	githubToken string,
	createAccount CopilotAccountCreator,
) (*Account, error) {
	if s == nil {
		return nil, infraerrors.New(http.StatusInternalServerError, "COPILOT_RUNTIME_UNAVAILABLE", "GitHub Copilot OAuth 服务不可用")
	}
	if createAccount == nil {
		return nil, infraerrors.New(http.StatusInternalServerError, "COPILOT_ACCOUNT_CREATOR_MISSING", "Copilot 账号创建服务不可用")
	}
	var err error
	settings, err = normalizeCopilotAccountSettings(settings)
	if err != nil {
		return nil, err
	}
	githubToken = strings.TrimSpace(githubToken)
	if len(githubToken) < 8 {
		return nil, infraerrors.New(http.StatusBadRequest, "COPILOT_GITHUB_TOKEN_REQUIRED", "请输入有效的 GitHub Token")
	}
	proxyURL, err := s.resolveCopilotProxyURL(ctx, settings.ProxyID)
	if err != nil {
		return nil, err
	}
	githubUser, err := s.getCopilotGitHubUserWithProxy(ctx, githubToken, proxyURL)
	if err != nil {
		return nil, err
	}
	copilotToken, err := s.exchangeCopilotTokenWithProxy(ctx, githubToken, proxyURL)
	if err != nil {
		return nil, err
	}
	credentials, extra := buildCopilotConfiguredAccount(settings, githubToken, githubUser, copilotToken)
	if strings.TrimSpace(settings.BillingPAT) != "" && strings.TrimSpace(settings.BillingUsername) == "" {
		settings.BillingUsername = strings.TrimSpace(githubUser.Login)
		credentials["billing_username"] = settings.BillingUsername
	}
	return createAccount(ctx, settings, credentials, extra)
}

func BuildCopilotAccountCredentials(githubToken string, user *copilotGitHubUser, token *copilotTokenInfo) map[string]any {
	credentials := map[string]any{
		"oauth_profile":       CopilotOAuthProfile,
		"github_access_token": githubToken,
		"access_token":        token.AccessToken,
		"expires_at":          token.ExpiresAt.UTC().Format(time.RFC3339),
		"refresh_at":          token.RefreshAt.UTC().Format(time.RFC3339),
		"base_url":            CopilotAPIBaseURL,
	}
	if user != nil {
		if strings.TrimSpace(user.Login) != "" {
			credentials["github_login"] = strings.TrimSpace(user.Login)
		}
		if user.ID > 0 {
			credentials["github_user_id"] = strconv.FormatInt(user.ID, 10)
		}
	}
	return credentials
}

func buildCopilotConfiguredAccount(
	settings CopilotAccountSettings,
	githubToken string,
	user *copilotGitHubUser,
	token *copilotTokenInfo,
) (map[string]any, map[string]any) {
	credentials := BuildCopilotAccountCredentials(githubToken, user, token)
	if len(settings.ModelMapping) > 0 {
		mapping := make(map[string]any, len(settings.ModelMapping))
		for requested, upstream := range settings.ModelMapping {
			requested = strings.TrimSpace(requested)
			upstream = strings.TrimSpace(upstream)
			if requested != "" && upstream != "" {
				mapping[requested] = upstream
			}
		}
		if len(mapping) > 0 {
			credentials["model_mapping"] = mapping
		}
	}
	if billingPAT := strings.TrimSpace(settings.BillingPAT); billingPAT != "" {
		credentials["billing_pat"] = billingPAT
		if username := strings.TrimSpace(settings.BillingUsername); username != "" {
			credentials["billing_username"] = username
		}
	}
	extra := make(map[string]any, 3)
	if settings.BillingCreditLimit != nil {
		extra["billing_credit_limit"] = *settings.BillingCreditLimit
	}
	if settings.BillingSafetyMargin != nil {
		extra["billing_safety_margin"] = *settings.BillingSafetyMargin
	}
	if settings.BillingAutoPauseDisabled != nil {
		extra["billing_auto_pause_disabled"] = *settings.BillingAutoPauseDisabled
	}
	if len(extra) == 0 {
		extra = nil
	}
	return credentials, extra
}

func normalizeCopilotAccountSettings(settings CopilotAccountSettings) (CopilotAccountSettings, error) {
	settings = cloneCopilotAccountSettings(settings)
	settings.Name = strings.TrimSpace(settings.Name)
	settings.BillingUsername = strings.TrimSpace(settings.BillingUsername)
	settings.BillingPAT = strings.TrimSpace(settings.BillingPAT)
	if settings.Name == "" {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_OAUTH_NAME_REQUIRED", "请输入账号名称")
	}
	if utf8.RuneCountInString(settings.Name) > 100 {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_OAUTH_NAME_TOO_LONG", "账号名称不能超过 100 个字符")
	}
	if settings.Concurrency <= 0 {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_CONCURRENCY_INVALID", "concurrency must be greater than 0")
	}
	if settings.Priority < 0 {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_PRIORITY_INVALID", "priority must be >= 0")
	}
	if settings.LoadFactor != nil && (*settings.LoadFactor < 0 || *settings.LoadFactor > 10000) {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_LOAD_FACTOR_INVALID", "load_factor must be between 0 and 10000")
	}
	if settings.RateMultiplier != nil && *settings.RateMultiplier < 0 {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_RATE_MULTIPLIER_INVALID", "rate_multiplier must be >= 0")
	}
	if settings.BillingCreditLimit != nil && *settings.BillingCreditLimit <= 0 {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_BILLING_LIMIT_INVALID", "billing_credit_limit must be greater than 0")
	}
	if settings.BillingSafetyMargin != nil && *settings.BillingSafetyMargin < 0 {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_BILLING_MARGIN_INVALID", "billing_safety_margin must be >= 0")
	}
	if settings.BillingUsername != "" && (len(settings.BillingUsername) > 100 || strings.ContainsAny(settings.BillingUsername, "/?#")) {
		return settings, infraerrors.New(http.StatusBadRequest, "COPILOT_BILLING_USERNAME_INVALID", "GitHub Billing 用户名无效")
	}
	return settings, nil
}

func cloneCopilotAccountSettings(settings CopilotAccountSettings) CopilotAccountSettings {
	settings.GroupIDs = append([]int64(nil), settings.GroupIDs...)
	if settings.ModelMapping != nil {
		mapping := make(map[string]string, len(settings.ModelMapping))
		for key, value := range settings.ModelMapping {
			mapping[key] = value
		}
		settings.ModelMapping = mapping
	}
	return settings
}

// normalizeCopilotModel uses the dotted Claude model spelling expected by the
// Copilot endpoint (for example claude-sonnet-4-5 -> claude-sonnet-4.5).
func normalizeCopilotModel(model string) string {
	model = strings.TrimSpace(model)
	match := copilotHyphenatedClaudeModel.FindStringSubmatch(model)
	if len(match) == 4 {
		// Claude Code may append Anthropic's dated suffix to the public model
		// alias. Copilot's catalog uses the undated dotted ID, so retaining the
		// suffix would turn a valid alias into an unknown upstream model.
		return match[1] + "." + match[2]
	}
	return model
}

// copilotModelForClient converts Copilot's dotted Claude spelling back to the
// public hyphenated form when a response contains the upstream model id.
func copilotModelForClient(model string) string {
	model = strings.TrimSpace(model)
	match := copilotDottedClaudeModel.FindStringSubmatch(model)
	if len(match) == 4 {
		return match[1] + "-" + match[2] + match[3]
	}
	return model
}

func (s *OpenAIOAuthService) getCopilotGitHubUser(ctx context.Context, githubToken string) (*copilotGitHubUser, error) {
	return s.getCopilotGitHubUserWithProxy(ctx, githubToken, "")
}

func (s *OpenAIOAuthService) getCopilotGitHubUserWithProxy(ctx context.Context, githubToken, proxyURL string) (*copilotGitHubUser, error) {
	s.ensureCopilotRuntime()
	var user copilotGitHubUser
	if err := s.copilotRequestJSONWithProxy(ctx, http.MethodGet, s.copilotEndpoints.githubUserURL, nil, map[string]string{
		"Authorization": "token " + githubToken,
		"Accept":        "application/json",
		"User-Agent":    "ParaGateway",
	}, &user, proxyURL); err != nil {
		return nil, err
	}
	if strings.TrimSpace(user.Login) == "" {
		return nil, infraerrors.New(http.StatusBadGateway, "GITHUB_USER_FAILED", "GitHub Token 无法读取用户信息")
	}
	return &user, nil
}

func (s *OpenAIOAuthService) getCopilotFlow(flowID string, adminUserID int64) *copilotOAuthFlow {
	s.ensureCopilotRuntime()
	if _, err := uuid.Parse(strings.TrimSpace(flowID)); err != nil {
		return nil
	}
	s.copilotFlowsMu.RLock()
	flow := s.copilotFlows[flowID]
	s.copilotFlowsMu.RUnlock()
	if flow == nil || flow.AdminUserID != adminUserID {
		return nil
	}
	return flow
}

func (s *OpenAIOAuthService) ensureCopilotFlowCapacity(adminUserID int64, now time.Time) error {
	s.copilotFlowsMu.Lock()
	defer s.copilotFlowsMu.Unlock()
	s.cleanupCopilotFlowsLocked(now)
	if s.countPendingCopilotFlowsLocked(adminUserID) >= copilotMaxPendingFlowsPerAdmin {
		return infraerrors.New(http.StatusTooManyRequests, "COPILOT_OAUTH_FLOW_LIMIT", "未完成的 Copilot 授权流程过多，请先完成或取消已有流程")
	}
	return nil
}

// countPendingCopilotFlowsLocked requires copilotFlowsMu to be held. The lock
// order for all multi-lock operations is copilotFlowsMu followed by flow.mu.
func (s *OpenAIOAuthService) countPendingCopilotFlowsLocked(adminUserID int64) int {
	count := 0
	for _, flow := range s.copilotFlows {
		flow.mu.Lock()
		if flow.AdminUserID == adminUserID && flow.Status == CopilotOAuthStatusPending {
			count++
		}
		flow.mu.Unlock()
	}
	return count
}

// CancelCopilotOAuthFlow removes a pending flow owned by the authenticated
// administrator and clears all server-side tokens and account settings.
func (s *OpenAIOAuthService) CancelCopilotOAuthFlow(adminUserID int64, flowID string) error {
	if s == nil || adminUserID <= 0 {
		return infraerrors.New(http.StatusUnauthorized, "COPILOT_OAUTH_UNAUTHORIZED", "管理员登录状态无效")
	}
	s.ensureCopilotRuntime()
	flowID = strings.TrimSpace(flowID)
	if _, err := uuid.Parse(flowID); err != nil {
		return infraerrors.New(http.StatusNotFound, "COPILOT_OAUTH_FLOW_NOT_FOUND", "Copilot 授权流程不存在")
	}

	s.copilotFlowsMu.Lock()
	defer s.copilotFlowsMu.Unlock()
	flow := s.copilotFlows[flowID]
	if flow == nil || flow.AdminUserID != adminUserID {
		return infraerrors.New(http.StatusNotFound, "COPILOT_OAUTH_FLOW_NOT_FOUND", "Copilot 授权流程不存在")
	}
	flow.mu.Lock()
	if flow.Status == CopilotOAuthStatusPending {
		flow.Status = CopilotOAuthStatusExpired
	}
	flow.clearSensitiveState()
	delete(s.copilotFlows, flowID)
	flow.mu.Unlock()
	return nil
}

func (s *OpenAIOAuthService) cleanupCopilotFlowsLocked(now time.Time) {
	for id, flow := range s.copilotFlows {
		flow.mu.Lock()
		if !now.Before(flow.CreatedAt.Add(copilotFlowRetention)) {
			flow.clearSensitiveState()
			delete(s.copilotFlows, id)
			flow.mu.Unlock()
			continue
		}
		if flow.Status == CopilotOAuthStatusPending {
			deviceAuthorizationExpired := flow.GitHubToken == "" && !flow.ExpiresAt.IsZero() && !now.Before(flow.ExpiresAt)
			accountCreationExpired := flow.GitHubToken != "" && !flow.AccountCreateExpiresAt.IsZero() && !now.Before(flow.AccountCreateExpiresAt)
			if deviceAuthorizationExpired || accountCreationExpired {
				flow.Status = CopilotOAuthStatusExpired
				flow.clearSensitiveState()
			}
		}
		flow.mu.Unlock()
	}
}

func copilotOAuthFlowSnapshot(flow *copilotOAuthFlow) *CopilotOAuthFlowResult {
	return &CopilotOAuthFlowResult{
		FlowID:            flow.ID,
		Profile:           CopilotOAuthProfile,
		Status:            flow.Status,
		UserCode:          flow.UserCode,
		VerificationURI:   flow.VerificationURI,
		ExpiresAt:         flow.ExpiresAt,
		IntervalSeconds:   flow.IntervalSeconds,
		NextPollAt:        flow.NextPollAt,
		ProviderAccountID: flow.ProviderAccountID,
		CompletedAt:       flow.CompletedAt,
	}
}

func copilotFormHeaders() map[string]string {
	return map[string]string{
		"Accept":       "application/json",
		"Content-Type": "application/x-www-form-urlencoded",
		"User-Agent":   "ParaGateway",
	}
}

func copilotHeaders(token string, githubToken bool) map[string]string {
	authorizationScheme := "Bearer"
	if githubToken {
		authorizationScheme = "token"
	}
	headers := map[string]string{
		"Authorization":                       authorizationScheme + " " + token,
		"Accept":                              "application/json",
		"Editor-Version":                      "vscode/1.98.1",
		"Editor-Plugin-Version":               "copilot-chat/0.26.7",
		"User-Agent":                          "GitHubCopilotChat/0.26.7",
		"X-GitHub-Api-Version":                "2025-04-01",
		"X-Vscode-User-Agent-Library-Version": "electron-fetch",
	}
	if !githubToken {
		headers["Content-Type"] = "application/json"
		headers["Copilot-Integration-Id"] = "vscode-chat"
		headers["OpenAI-Intent"] = "conversation-panel"
		headers["X-Request-Id"] = uuid.NewString()
		headers["X-Initiator"] = "user"
	}
	return headers
}

func (s *OpenAIOAuthService) copilotRequestJSON(
	ctx context.Context,
	method string,
	endpoint string,
	body io.Reader,
	headers map[string]string,
	destination any,
) error {
	return s.copilotRequestJSONWithProxy(ctx, method, endpoint, body, headers, destination, "")
}

func (s *OpenAIOAuthService) copilotRequestJSONWithProxy(
	ctx context.Context,
	method string,
	endpoint string,
	body io.Reader,
	headers map[string]string,
	destination any,
	proxyURL string,
) error {
	if s == nil {
		return infraerrors.New(http.StatusInternalServerError, "COPILOT_RUNTIME_UNAVAILABLE", "GitHub Copilot OAuth 服务不可用")
	}
	s.ensureCopilotRuntime()
	request, err := http.NewRequestWithContext(ctx, method, endpoint, body)
	if err != nil {
		return infraerrors.New(http.StatusBadGateway, "COPILOT_REQUEST_INVALID", "无法创建 GitHub Copilot 请求").WithCause(err)
	}
	for key, value := range headers {
		request.Header.Set(key, value)
	}
	client, err := copilotHTTPClientForProxy(s.copilotHTTPClient, proxyURL, copilotRequestTimeout)
	if err != nil {
		return infraerrors.New(http.StatusBadRequest, "COPILOT_PROXY_INVALID", "Copilot 代理配置无效").WithCause(err)
	}
	response, err := client.Do(request)
	if err != nil {
		return infraerrors.New(http.StatusBadGateway, "COPILOT_UPSTREAM_UNREACHABLE", "无法连接 GitHub Copilot 服务").WithCause(err)
	}
	defer response.Body.Close()
	limited := io.LimitReader(response.Body, copilotMaxResponseBytes+1)
	payload, err := io.ReadAll(limited)
	if err != nil {
		return infraerrors.New(http.StatusBadGateway, "COPILOT_UPSTREAM_INVALID", "无法读取 GitHub Copilot 响应").WithCause(err)
	}
	if len(payload) > copilotMaxResponseBytes {
		return infraerrors.New(http.StatusBadGateway, "COPILOT_UPSTREAM_TOO_LARGE", "GitHub Copilot 响应过大")
	}
	if response.StatusCode < 200 || response.StatusCode >= 300 {
		return infraerrors.Newf(http.StatusBadGateway, "COPILOT_UPSTREAM_REJECTED", "GitHub Copilot 服务拒绝了请求（HTTP %d）", response.StatusCode)
	}
	if err := json.Unmarshal(payload, destination); err != nil {
		return infraerrors.New(http.StatusBadGateway, "COPILOT_UPSTREAM_INVALID", "GitHub Copilot 返回了无效响应").WithCause(fmt.Errorf("decode response: %w", err))
	}
	return nil
}

func (s *OpenAIOAuthService) resolveCopilotProxyURL(ctx context.Context, proxyID *int64) (string, error) {
	if proxyID == nil {
		return "", nil
	}
	if s == nil || s.proxyRepo == nil {
		return "", infraerrors.New(http.StatusBadRequest, "COPILOT_PROXY_UNAVAILABLE", "Copilot 代理服务不可用")
	}
	proxy, err := s.proxyRepo.GetByID(ctx, *proxyID)
	if err != nil || proxy == nil {
		return "", infraerrors.New(http.StatusBadRequest, "COPILOT_PROXY_NOT_FOUND", "所选代理不存在").WithCause(err)
	}
	return proxy.URL(), nil
}

func copilotHTTPClientForProxy(fallback *http.Client, proxyURL string, timeout time.Duration) (*http.Client, error) {
	proxyURL = strings.TrimSpace(proxyURL)
	if proxyURL == "" {
		if fallback != nil {
			return fallback, nil
		}
		return &http.Client{Timeout: timeout}, nil
	}
	parsed, err := url.Parse(proxyURL)
	if err != nil {
		return nil, fmt.Errorf("parse proxy URL: %w", err)
	}
	if parsed.Scheme == "" || parsed.Host == "" {
		return nil, fmt.Errorf("parse proxy URL: scheme and host are required")
	}
	transport := http.DefaultTransport.(*http.Transport).Clone()
	transport.Proxy = nil
	if err := proxyutil.ConfigureTransportProxy(transport, parsed); err != nil {
		return nil, err
	}
	return &http.Client{Transport: transport, Timeout: timeout}, nil
}
