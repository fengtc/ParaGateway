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
	"github.com/google/uuid"
)

var (
	copilotHyphenatedClaudeModel = regexp.MustCompile(`^(claude-(?:sonnet|opus|haiku)-[0-9]+)-([0-9]+)(-?[0-9]{8})?$`)
	copilotDottedClaudeModel     = regexp.MustCompile(`^(claude-(?:sonnet|opus|haiku)-[0-9]+)\.([0-9]+)(.*)$`)
)

const (
	CopilotOAuthProfile     = "github_copilot"
	CopilotAPIBaseURL       = "https://api.githubcopilot.com"
	copilotDeviceClientID   = "Iv1.b507a08c87ecfe98"
	copilotRequestTimeout   = 30 * time.Second
	copilotMaxResponseBytes = 128 * 1024
	copilotFlowRetention    = 24 * time.Hour

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
	deviceCodeURL    string
	accessTokenURL   string
	githubUserURL    string
	tokenExchangeURL string
}

func defaultCopilotOAuthEndpoints() copilotOAuthEndpoints {
	return copilotOAuthEndpoints{
		deviceCodeURL:    "https://github.com/login/device/code",
		accessTokenURL:   "https://github.com/login/oauth/access_token",
		githubUserURL:    "https://api.github.com/user",
		tokenExchangeURL: "https://api.github.com/copilot_internal/v2/token",
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
}

type copilotOAuthFlow struct {
	mu                sync.Mutex
	ID                string
	AdminUserID       int64
	AccountName       string
	DeviceCode        string
	UserCode          string
	VerificationURI   string
	Status            string
	IntervalSeconds   int
	NextPollAt        time.Time
	ExpiresAt         time.Time
	CreatedAt         time.Time
	CompletedAt       *time.Time
	ProviderAccountID *int64
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

// StartCopilotOAuthFlow starts GitHub's device authorization flow. Device codes
// stay in process memory and are scoped to the authenticated administrator.
func (s *OpenAIOAuthService) StartCopilotOAuthFlow(ctx context.Context, adminUserID int64, accountName string) (*CopilotOAuthFlowResult, error) {
	if s == nil || adminUserID <= 0 {
		return nil, infraerrors.New(http.StatusUnauthorized, "COPILOT_OAUTH_UNAUTHORIZED", "管理员登录状态无效")
	}
	s.ensureCopilotRuntime()
	accountName = strings.TrimSpace(accountName)
	if accountName == "" {
		return nil, infraerrors.New(http.StatusBadRequest, "COPILOT_OAUTH_NAME_REQUIRED", "请输入账号名称")
	}
	if utf8.RuneCountInString(accountName) > 100 {
		return nil, infraerrors.New(http.StatusBadRequest, "COPILOT_OAUTH_NAME_TOO_LONG", "账号名称不能超过 100 个字符")
	}

	values := url.Values{
		"client_id": {copilotDeviceClientID},
		"scope":     {"read:user"},
	}
	var deviceResponse copilotDeviceCodeResponse
	if err := s.copilotRequestJSON(ctx, http.MethodPost, s.copilotEndpoints.deviceCodeURL, strings.NewReader(values.Encode()), copilotFormHeaders(), &deviceResponse); err != nil {
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
		ID:              uuid.NewString(),
		AdminUserID:     adminUserID,
		AccountName:     accountName,
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
	s.copilotFlows[flow.ID] = flow
	s.copilotFlowsMu.Unlock()
	return copilotOAuthFlowSnapshot(flow), nil
}

// PollCopilotOAuthFlow advances one device-flow poll. Account creation executes
// while holding the per-flow lock, preventing duplicate accounts on concurrent polls.
func (s *OpenAIOAuthService) PollCopilotOAuthFlow(
	ctx context.Context,
	adminUserID int64,
	flowID string,
	createAccount func(context.Context, string, map[string]any) (*Account, error),
) (*CopilotOAuthFlowResult, error) {
	if s == nil || adminUserID <= 0 {
		return nil, infraerrors.New(http.StatusUnauthorized, "COPILOT_OAUTH_UNAUTHORIZED", "管理员登录状态无效")
	}
	s.ensureCopilotRuntime()
	flow := s.getCopilotFlow(flowID, adminUserID)
	if flow == nil {
		return nil, infraerrors.New(http.StatusNotFound, "COPILOT_OAUTH_FLOW_NOT_FOUND", "Copilot 授权流程不存在")
	}

	flow.mu.Lock()
	defer flow.mu.Unlock()
	now := time.Now().UTC()
	if flow.Status == CopilotOAuthStatusCompleted || flow.Status == CopilotOAuthStatusFailed || flow.Status == CopilotOAuthStatusExpired {
		return copilotOAuthFlowSnapshot(flow), nil
	}
	if !now.Before(flow.ExpiresAt) {
		flow.Status = CopilotOAuthStatusExpired
		flow.DeviceCode = ""
		return copilotOAuthFlowSnapshot(flow), nil
	}
	if now.Before(flow.NextPollAt) {
		return copilotOAuthFlowSnapshot(flow), nil
	}

	flow.NextPollAt = now.Add(time.Duration(flow.IntervalSeconds) * time.Second)
	values := url.Values{
		"client_id":   {copilotDeviceClientID},
		"device_code": {flow.DeviceCode},
		"grant_type":  {"urn:ietf:params:oauth:grant-type:device_code"},
	}
	var tokenResponse copilotAccessTokenResponse
	if err := s.copilotRequestJSON(ctx, http.MethodPost, s.copilotEndpoints.accessTokenURL, strings.NewReader(values.Encode()), copilotFormHeaders(), &tokenResponse); err != nil {
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
		flow.DeviceCode = ""
		return copilotOAuthFlowSnapshot(flow), nil
	case "access_denied":
		flow.Status = CopilotOAuthStatusFailed
		flow.DeviceCode = ""
		return copilotOAuthFlowSnapshot(flow), nil
	case "":
		// Continue below.
	default:
		flow.Status = CopilotOAuthStatusFailed
		flow.DeviceCode = ""
		return nil, infraerrors.New(http.StatusBadGateway, "COPILOT_DEVICE_OAUTH_REJECTED", "GitHub 拒绝了 Copilot 授权请求")
	}
	githubToken := strings.TrimSpace(tokenResponse.AccessToken)
	if len(githubToken) < 8 {
		flow.Status = CopilotOAuthStatusFailed
		flow.DeviceCode = ""
		return nil, infraerrors.New(http.StatusBadGateway, "COPILOT_DEVICE_OAUTH_INVALID", "GitHub 返回了无效的授权响应")
	}

	githubUser, err := s.getCopilotGitHubUser(ctx, githubToken)
	if err != nil {
		return nil, err
	}
	copilotToken, err := s.ExchangeCopilotToken(ctx, githubToken)
	if err != nil {
		return nil, err
	}
	if createAccount == nil {
		return nil, infraerrors.New(http.StatusInternalServerError, "COPILOT_ACCOUNT_CREATOR_MISSING", "Copilot 账号创建服务不可用")
	}

	credentials := BuildCopilotAccountCredentials(githubToken, githubUser, copilotToken)
	account, err := createAccount(ctx, flow.AccountName, credentials)
	if err != nil {
		return nil, err
	}
	completedAt := time.Now().UTC()
	flow.Status = CopilotOAuthStatusCompleted
	flow.DeviceCode = ""
	flow.CompletedAt = &completedAt
	flow.ProviderAccountID = &account.ID
	result := copilotOAuthFlowSnapshot(flow)
	result.ProviderAccount = account
	return result, nil
}

// ExchangeCopilotToken exchanges a long-lived GitHub OAuth token for the short-
// lived token accepted by api.githubcopilot.com.
func (s *OpenAIOAuthService) ExchangeCopilotToken(ctx context.Context, githubToken string) (*copilotTokenInfo, error) {
	if s == nil {
		return nil, infraerrors.New(http.StatusInternalServerError, "COPILOT_RUNTIME_UNAVAILABLE", "GitHub Copilot OAuth 服务不可用")
	}
	s.ensureCopilotRuntime()
	githubToken = strings.TrimSpace(githubToken)
	if githubToken == "" {
		return nil, infraerrors.New(http.StatusBadRequest, "COPILOT_GITHUB_TOKEN_REQUIRED", "GitHub OAuth Token 缺失")
	}
	var response copilotTokenExchangeResponse
	if err := s.copilotRequestJSON(ctx, http.MethodGet, s.copilotEndpoints.tokenExchangeURL, nil, copilotHeaders(githubToken, true), &response); err != nil {
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

// normalizeCopilotModel uses the dotted Claude model spelling expected by the
// Copilot endpoint (for example claude-sonnet-4-5 -> claude-sonnet-4.5).
func normalizeCopilotModel(model string) string {
	model = strings.TrimSpace(model)
	match := copilotHyphenatedClaudeModel.FindStringSubmatch(model)
	if len(match) == 4 {
		return match[1] + "." + match[2] + match[3]
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
	s.ensureCopilotRuntime()
	var user copilotGitHubUser
	if err := s.copilotRequestJSON(ctx, http.MethodGet, s.copilotEndpoints.githubUserURL, nil, map[string]string{
		"Authorization": "token " + githubToken,
		"Accept":        "application/json",
		"User-Agent":    "ParaGateway",
	}, &user); err != nil {
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

func (s *OpenAIOAuthService) cleanupCopilotFlowsLocked(now time.Time) {
	for id, flow := range s.copilotFlows {
		if now.After(flow.CreatedAt.Add(copilotFlowRetention)) {
			delete(s.copilotFlows, id)
		}
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
	client := s.copilotHTTPClient
	if client == nil {
		client = &http.Client{Timeout: copilotRequestTimeout}
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
