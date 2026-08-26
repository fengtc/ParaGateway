//go:build unit

package service

import (
	"context"
	"fmt"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/pkg/oauth"
	"github.com/Wei-Shaw/sub2api/internal/pkg/pagination"
)

// --- mock: ClaudeOAuthClient ---

type mockClaudeOAuthClient struct {
	getOrgUUIDFunc   func(ctx context.Context, sessionKey, proxyURL string) (string, error)
	getAuthCodeFunc  func(ctx context.Context, sessionKey, orgUUID, scope, codeChallenge, state, proxyURL string) (string, error)
	exchangeCodeFunc func(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*oauth.TokenResponse, error)
	refreshTokenFunc func(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error)
}

type mockClaudeOAuthProfileClient struct {
	*mockClaudeOAuthClient
	getOAuthProfileFunc func(ctx context.Context, accessToken, proxyURL string) (*ClaudeOAuthProfile, error)
}

func (m *mockClaudeOAuthProfileClient) GetOAuthProfile(ctx context.Context, accessToken, proxyURL string) (*ClaudeOAuthProfile, error) {
	if m.getOAuthProfileFunc != nil {
		return m.getOAuthProfileFunc(ctx, accessToken, proxyURL)
	}
	return nil, nil
}

func (m *mockClaudeOAuthClient) GetOrganizationUUID(ctx context.Context, sessionKey, proxyURL string) (string, error) {
	if m.getOrgUUIDFunc != nil {
		return m.getOrgUUIDFunc(ctx, sessionKey, proxyURL)
	}
	panic("GetOrganizationUUID not implemented")
}

func (m *mockClaudeOAuthClient) GetAuthorizationCode(ctx context.Context, sessionKey, orgUUID, scope, codeChallenge, state, proxyURL string) (string, error) {
	if m.getAuthCodeFunc != nil {
		return m.getAuthCodeFunc(ctx, sessionKey, orgUUID, scope, codeChallenge, state, proxyURL)
	}
	panic("GetAuthorizationCode not implemented")
}

func (m *mockClaudeOAuthClient) ExchangeCodeForToken(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*oauth.TokenResponse, error) {
	if m.exchangeCodeFunc != nil {
		return m.exchangeCodeFunc(ctx, code, codeVerifier, state, proxyURL, isSetupToken)
	}
	panic("ExchangeCodeForToken not implemented")
}

func (m *mockClaudeOAuthClient) RefreshToken(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error) {
	if m.refreshTokenFunc != nil {
		return m.refreshTokenFunc(ctx, refreshToken, proxyURL)
	}
	panic("RefreshToken not implemented")
}

func TestClaudeOAuthPlanType(t *testing.T) {
	t.Parallel()

	tests := []struct {
		name             string
		organizationType string
		rateLimitTier    string
		hasClaudeMax     *bool
		hasClaudePro     *bool
		want             string
	}{
		{name: "pro", organizationType: "claude_pro", rateLimitTier: "default_claude_pro", want: "pro"},
		{name: "max", organizationType: "claude_max", want: "max"},
		{name: "max_5x", organizationType: "claude_max", rateLimitTier: "default_claude_max_5x", want: "max_5x"},
		{name: "max_20x", organizationType: "claude_max", rateLimitTier: "DEFAULT-CLAUDE-MAX-20X", want: "max_20x"},
		{name: "max_organization_precedes_non_max_tier", organizationType: "claude_max", rateLimitTier: "default_claude_pro", want: "max"},
		{name: "team_precedes_member_tier", organizationType: "claude_team", rateLimitTier: "default_claude_pro", want: "team"},
		{name: "enterprise", organizationType: "enterprise", rateLimitTier: "default_claude_max_20x", want: "enterprise"},
		{name: "free", organizationType: "claude_free", want: "free"},
		{name: "tier_precedes_max_flag", rateLimitTier: "default_claude_pro", hasClaudeMax: claudeOAuthTestBoolPtr(true), want: "pro"},
		{name: "max_flag_fallback", hasClaudeMax: claudeOAuthTestBoolPtr(true), hasClaudePro: claudeOAuthTestBoolPtr(true), want: "max"},
		{name: "pro_flag_fallback", hasClaudeMax: claudeOAuthTestBoolPtr(false), hasClaudePro: claudeOAuthTestBoolPtr(true), want: "pro"},
		{name: "explicit_false_flags_are_not_free", hasClaudeMax: claudeOAuthTestBoolPtr(false), hasClaudePro: claudeOAuthTestBoolPtr(false), want: ""},
		{name: "unknown", organizationType: "unknown", rateLimitTier: "unknown", want: ""},
		{name: "unknown_values_containing_pro_are_not_inferred", organizationType: "production", rateLimitTier: "experimental_profile", want: ""},
	}

	for _, tt := range tests {
		tt := tt
		t.Run(tt.name, func(t *testing.T) {
			t.Parallel()
			if got := claudeOAuthPlanType(tt.organizationType, tt.rateLimitTier, tt.hasClaudeMax, tt.hasClaudePro); got != tt.want {
				t.Fatalf("claudeOAuthPlanType() = %q, want %q", got, tt.want)
			}
		})
	}
}

func claudeOAuthTestBoolPtr(value bool) *bool {
	return &value
}

// --- mock: ProxyRepository (最小实现，仅覆盖 OAuthService 依赖的方法) ---

type mockProxyRepoForOAuth struct {
	getByIDFunc func(ctx context.Context, id int64) (*Proxy, error)
}

func (m *mockProxyRepoForOAuth) Create(ctx context.Context, proxy *Proxy) error {
	panic("Create not implemented")
}
func (m *mockProxyRepoForOAuth) GetByID(ctx context.Context, id int64) (*Proxy, error) {
	if m.getByIDFunc != nil {
		return m.getByIDFunc(ctx, id)
	}
	return nil, fmt.Errorf("proxy not found")
}
func (m *mockProxyRepoForOAuth) ListByIDs(ctx context.Context, ids []int64) ([]Proxy, error) {
	panic("ListByIDs not implemented")
}
func (m *mockProxyRepoForOAuth) Update(ctx context.Context, proxy *Proxy) error {
	panic("Update not implemented")
}
func (m *mockProxyRepoForOAuth) Delete(ctx context.Context, id int64) error {
	panic("Delete not implemented")
}
func (m *mockProxyRepoForOAuth) List(ctx context.Context, params pagination.PaginationParams) ([]Proxy, *pagination.PaginationResult, error) {
	panic("List not implemented")
}
func (m *mockProxyRepoForOAuth) ListWithFilters(ctx context.Context, params pagination.PaginationParams, protocol, status, search string) ([]Proxy, *pagination.PaginationResult, error) {
	panic("ListWithFilters not implemented")
}
func (m *mockProxyRepoForOAuth) ListWithFiltersAndAccountCount(ctx context.Context, params pagination.PaginationParams, protocol, status, search string) ([]ProxyWithAccountCount, *pagination.PaginationResult, error) {
	panic("ListWithFiltersAndAccountCount not implemented")
}
func (m *mockProxyRepoForOAuth) ListActive(ctx context.Context) ([]Proxy, error) {
	panic("ListActive not implemented")
}
func (m *mockProxyRepoForOAuth) ListActiveWithAccountCount(ctx context.Context) ([]ProxyWithAccountCount, error) {
	panic("ListActiveWithAccountCount not implemented")
}
func (m *mockProxyRepoForOAuth) ExistsByHostPortAuth(ctx context.Context, host string, port int, username, password string) (bool, error) {
	panic("ExistsByHostPortAuth not implemented")
}
func (m *mockProxyRepoForOAuth) CountAccountsByProxyID(ctx context.Context, proxyID int64) (int64, error) {
	panic("CountAccountsByProxyID not implemented")
}
func (m *mockProxyRepoForOAuth) ListAccountSummariesByProxyID(ctx context.Context, proxyID int64) ([]ProxyAccountSummary, error) {
	panic("ListAccountSummariesByProxyID not implemented")
}
func (m *mockProxyRepoForOAuth) SweepExpiredProxies(ctx context.Context, now time.Time) (int64, error) {
	panic("SweepExpiredProxies not implemented")
}
func (m *mockProxyRepoForOAuth) ListAllForFallback(ctx context.Context) ([]Proxy, error) {
	panic("ListAllForFallback not implemented")
}
func (m *mockProxyRepoForOAuth) CountExpired(ctx context.Context) (int64, error) {
	panic("CountExpired not implemented")
}
func (m *mockProxyRepoForOAuth) CountExpiringSoon(ctx context.Context, now time.Time) (int64, error) {
	panic("CountExpiringSoon not implemented")
}

// =====================
// 测试用例
// =====================

func TestNewOAuthService(t *testing.T) {
	t.Parallel()

	proxyRepo := &mockProxyRepoForOAuth{}
	client := &mockClaudeOAuthClient{}
	svc := NewOAuthService(proxyRepo, client)

	if svc == nil {
		t.Fatal("NewOAuthService 返回 nil")
	}
	if svc.proxyRepo != proxyRepo {
		t.Fatal("proxyRepo 未正确设置")
	}
	if svc.oauthClient != client {
		t.Fatal("oauthClient 未正确设置")
	}
	if svc.sessionStore == nil {
		t.Fatal("sessionStore 应被自动初始化")
	}

	// 清理
	svc.Stop()
}

func TestOAuthService_GenerateAuthURL(t *testing.T) {
	t.Parallel()

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, &mockClaudeOAuthClient{})
	defer svc.Stop()

	result, err := svc.GenerateAuthURL(context.Background(), nil)
	if err != nil {
		t.Fatalf("GenerateAuthURL 返回错误: %v", err)
	}
	if result == nil {
		t.Fatal("GenerateAuthURL 返回 nil")
	}
	if result.AuthURL == "" {
		t.Fatal("AuthURL 为空")
	}
	if result.SessionID == "" {
		t.Fatal("SessionID 为空")
	}

	// 验证 session 已存储
	session, ok := svc.sessionStore.Get(result.SessionID)
	if !ok {
		t.Fatal("session 未在 sessionStore 中找到")
	}
	if session.Scope != oauth.ScopeOAuth {
		t.Fatalf("scope 不匹配: got=%q want=%q", session.Scope, oauth.ScopeOAuth)
	}
}

func TestOAuthService_GenerateAuthURL_WithProxy(t *testing.T) {
	t.Parallel()

	proxyRepo := &mockProxyRepoForOAuth{
		getByIDFunc: func(ctx context.Context, id int64) (*Proxy, error) {
			return &Proxy{
				ID:       1,
				Protocol: "http",
				Host:     "proxy.example.com",
				Port:     8080,
			}, nil
		},
	}
	svc := NewOAuthService(proxyRepo, &mockClaudeOAuthClient{})
	defer svc.Stop()

	proxyID := int64(1)
	result, err := svc.GenerateAuthURL(context.Background(), &proxyID)
	if err != nil {
		t.Fatalf("GenerateAuthURL 返回错误: %v", err)
	}

	session, ok := svc.sessionStore.Get(result.SessionID)
	if !ok {
		t.Fatal("session 未在 sessionStore 中找到")
	}
	if session.ProxyURL != "http://proxy.example.com:8080" {
		t.Fatalf("ProxyURL 不匹配: got=%q", session.ProxyURL)
	}
}

func TestOAuthService_GenerateSetupTokenURL(t *testing.T) {
	t.Parallel()

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, &mockClaudeOAuthClient{})
	defer svc.Stop()

	result, err := svc.GenerateSetupTokenURL(context.Background(), nil)
	if err != nil {
		t.Fatalf("GenerateSetupTokenURL 返回错误: %v", err)
	}
	if result == nil {
		t.Fatal("GenerateSetupTokenURL 返回 nil")
	}

	// 验证 scope 是 inference
	session, ok := svc.sessionStore.Get(result.SessionID)
	if !ok {
		t.Fatal("session 未在 sessionStore 中找到")
	}
	if session.Scope != oauth.ScopeInference {
		t.Fatalf("scope 不匹配: got=%q want=%q", session.Scope, oauth.ScopeInference)
	}
}

func TestOAuthService_ExchangeCode_SessionNotFound(t *testing.T) {
	t.Parallel()

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, &mockClaudeOAuthClient{})
	defer svc.Stop()

	_, err := svc.ExchangeCode(context.Background(), &ExchangeCodeInput{
		SessionID: "nonexistent-session",
		Code:      "test-code",
	})
	if err == nil {
		t.Fatal("ExchangeCode 应返回错误（session 不存在）")
	}
	if err.Error() != "session not found or expired" {
		t.Fatalf("错误信息不匹配: got=%q", err.Error())
	}
}

func TestOAuthService_ExchangeCode_Success(t *testing.T) {
	t.Parallel()

	exchangeCalled := false
	client := &mockClaudeOAuthClient{
		exchangeCodeFunc: func(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*oauth.TokenResponse, error) {
			exchangeCalled = true
			if code != "auth-code-123" {
				t.Errorf("code 不匹配: got=%q", code)
			}
			if isSetupToken {
				t.Error("isSetupToken 应为 false（ScopeOAuth）")
			}
			return &oauth.TokenResponse{
				AccessToken:  "access-token-abc",
				TokenType:    "Bearer",
				ExpiresIn:    3600,
				RefreshToken: "refresh-token-xyz",
				Scope:        oauth.ScopeOAuth,
				Organization: &oauth.OrgInfo{UUID: "org-uuid-111"},
				Account:      &oauth.AccountInfo{UUID: "acc-uuid-222", EmailAddress: "test@example.com"},
			}, nil
		},
	}

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
	defer svc.Stop()

	// 先生成 URL 以创建 session
	result, err := svc.GenerateAuthURL(context.Background(), nil)
	if err != nil {
		t.Fatalf("GenerateAuthURL 返回错误: %v", err)
	}

	// 交换 code
	tokenInfo, err := svc.ExchangeCode(context.Background(), &ExchangeCodeInput{
		SessionID: result.SessionID,
		Code:      "auth-code-123",
	})
	if err != nil {
		t.Fatalf("ExchangeCode 返回错误: %v", err)
	}

	if !exchangeCalled {
		t.Fatal("ExchangeCodeForToken 未被调用")
	}
	if tokenInfo.AccessToken != "access-token-abc" {
		t.Fatalf("AccessToken 不匹配: got=%q", tokenInfo.AccessToken)
	}
	if tokenInfo.TokenType != "Bearer" {
		t.Fatalf("TokenType 不匹配: got=%q", tokenInfo.TokenType)
	}
	if tokenInfo.RefreshToken != "refresh-token-xyz" {
		t.Fatalf("RefreshToken 不匹配: got=%q", tokenInfo.RefreshToken)
	}
	if tokenInfo.OrgUUID != "org-uuid-111" {
		t.Fatalf("OrgUUID 不匹配: got=%q", tokenInfo.OrgUUID)
	}
	if tokenInfo.AccountUUID != "acc-uuid-222" {
		t.Fatalf("AccountUUID 不匹配: got=%q", tokenInfo.AccountUUID)
	}
	if tokenInfo.EmailAddress != "test@example.com" {
		t.Fatalf("EmailAddress 不匹配: got=%q", tokenInfo.EmailAddress)
	}
	if tokenInfo.ExpiresIn != 3600 {
		t.Fatalf("ExpiresIn 不匹配: got=%d", tokenInfo.ExpiresIn)
	}
	if tokenInfo.ExpiresAt == 0 {
		t.Fatal("ExpiresAt 不应为 0")
	}

	// 验证 session 已被删除
	_, ok := svc.sessionStore.Get(result.SessionID)
	if ok {
		t.Fatal("session 应在交换成功后被删除")
	}
}

func TestOAuthService_ExchangeCode_EnrichesProfileBestEffort(t *testing.T) {
	t.Parallel()

	tests := []struct {
		name                    string
		profile                 *ClaudeOAuthProfile
		profileErr              error
		wantPlanType            string
		wantSubscriptionExpires string
		wantMetadataResolved    bool
	}{
		{
			name: "explicit_subscription_expiry",
			profile: &ClaudeOAuthProfile{
				OrganizationType:       "claude_max",
				RateLimitTier:          "default_claude_max_5x",
				SubscriptionExpiresAt: "2026-09-04T00:00:00Z",
			},
			wantPlanType:            "max_5x",
			wantSubscriptionExpires: "2026-09-04T00:00:00Z",
			wantMetadataResolved:    true,
		},
		{
			name: "explicit_no_paid_subscription",
			profile: &ClaudeOAuthProfile{
				HasClaudeMax: claudeOAuthTestBoolPtr(false),
				HasClaudePro: claudeOAuthTestBoolPtr(false),
			},
			wantMetadataResolved: true,
		},
		{
			name:       "profile_failure_does_not_fail_exchange",
			profileErr: fmt.Errorf("profile unavailable"),
		},
	}

	for _, tt := range tests {
		tt := tt
		t.Run(tt.name, func(t *testing.T) {
			t.Parallel()

			client := &mockClaudeOAuthProfileClient{
				mockClaudeOAuthClient: &mockClaudeOAuthClient{
					exchangeCodeFunc: func(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*oauth.TokenResponse, error) {
						return &oauth.TokenResponse{
							AccessToken: "access-token",
							TokenType:   "Bearer",
							ExpiresIn:   3600,
						}, nil
					},
				},
				getOAuthProfileFunc: func(ctx context.Context, accessToken, proxyURL string) (*ClaudeOAuthProfile, error) {
					if accessToken != "access-token" {
						t.Errorf("accessToken 不匹配: got=%q", accessToken)
					}
					deadline, ok := ctx.Deadline()
					if !ok {
						t.Error("profile context 应包含独立超时")
					} else if remaining := time.Until(deadline); remaining <= 0 || remaining > claudeOAuthProfileTimeout {
						t.Errorf("profile context 超时不合理: remaining=%s", remaining)
					}
					return tt.profile, tt.profileErr
				},
			}

			svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
			defer svc.Stop()

			result, err := svc.GenerateAuthURL(context.Background(), nil)
			if err != nil {
				t.Fatalf("GenerateAuthURL 返回错误: %v", err)
			}
			tokenInfo, err := svc.ExchangeCode(context.Background(), &ExchangeCodeInput{
				SessionID: result.SessionID,
				Code:      "auth-code",
			})
			if err != nil {
				t.Fatalf("ExchangeCode 返回错误: %v", err)
			}
			if tokenInfo.PlanType != tt.wantPlanType {
				t.Fatalf("PlanType = %q, want %q", tokenInfo.PlanType, tt.wantPlanType)
			}
			if tokenInfo.SubscriptionExpiresAt != tt.wantSubscriptionExpires {
				t.Fatalf("SubscriptionExpiresAt = %q, want %q", tokenInfo.SubscriptionExpiresAt, tt.wantSubscriptionExpires)
			}
			if tokenInfo.SubscriptionMetadataResolved != tt.wantMetadataResolved {
				t.Fatalf("SubscriptionMetadataResolved = %t, want %t", tokenInfo.SubscriptionMetadataResolved, tt.wantMetadataResolved)
			}
		})
	}
}

func TestOAuthService_ExchangeCode_SetupToken(t *testing.T) {
	t.Parallel()

	profileCalls := 0
	client := &mockClaudeOAuthProfileClient{
		mockClaudeOAuthClient: &mockClaudeOAuthClient{
			exchangeCodeFunc: func(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*oauth.TokenResponse, error) {
				if !isSetupToken {
					t.Error("isSetupToken 应为 true（ScopeInference）")
				}
				return &oauth.TokenResponse{
					AccessToken: "setup-token",
					TokenType:   "Bearer",
					ExpiresIn:   3600,
					Scope:       oauth.ScopeInference,
				}, nil
			},
		},
		getOAuthProfileFunc: func(ctx context.Context, accessToken, proxyURL string) (*ClaudeOAuthProfile, error) {
			profileCalls++
			return &ClaudeOAuthProfile{OrganizationType: "claude_pro"}, nil
		},
	}

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
	defer svc.Stop()

	// 使用 SetupToken URL（inference scope）
	result, err := svc.GenerateSetupTokenURL(context.Background(), nil)
	if err != nil {
		t.Fatalf("GenerateSetupTokenURL 返回错误: %v", err)
	}

	tokenInfo, err := svc.ExchangeCode(context.Background(), &ExchangeCodeInput{
		SessionID: result.SessionID,
		Code:      "setup-code",
	})
	if err != nil {
		t.Fatalf("ExchangeCode 返回错误: %v", err)
	}
	if tokenInfo.AccessToken != "setup-token" {
		t.Fatalf("AccessToken 不匹配: got=%q", tokenInfo.AccessToken)
	}
	if profileCalls != 0 {
		t.Fatalf("setup-token 不应查询 OAuth profile，调用次数=%d", profileCalls)
	}
}

func TestOAuthService_ExchangeCode_ClientError(t *testing.T) {
	t.Parallel()

	client := &mockClaudeOAuthClient{
		exchangeCodeFunc: func(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*oauth.TokenResponse, error) {
			return nil, fmt.Errorf("upstream error: invalid code")
		},
	}

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
	defer svc.Stop()

	result, _ := svc.GenerateAuthURL(context.Background(), nil)
	_, err := svc.ExchangeCode(context.Background(), &ExchangeCodeInput{
		SessionID: result.SessionID,
		Code:      "bad-code",
	})
	if err == nil {
		t.Fatal("ExchangeCode 应返回错误")
	}
	if err.Error() != "upstream error: invalid code" {
		t.Fatalf("错误信息不匹配: got=%q", err.Error())
	}
}

func TestOAuthService_RefreshToken(t *testing.T) {
	t.Parallel()

	client := &mockClaudeOAuthClient{
		refreshTokenFunc: func(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error) {
			if refreshToken != "my-refresh-token" {
				t.Errorf("refreshToken 不匹配: got=%q", refreshToken)
			}
			if proxyURL != "" {
				t.Errorf("proxyURL 应为空: got=%q", proxyURL)
			}
			return &oauth.TokenResponse{
				AccessToken:  "new-access-token",
				TokenType:    "Bearer",
				ExpiresIn:    7200,
				RefreshToken: "new-refresh-token",
				Scope:        oauth.ScopeOAuth,
			}, nil
		},
	}

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
	defer svc.Stop()

	tokenInfo, err := svc.RefreshToken(context.Background(), "my-refresh-token", "")
	if err != nil {
		t.Fatalf("RefreshToken 返回错误: %v", err)
	}
	if tokenInfo.AccessToken != "new-access-token" {
		t.Fatalf("AccessToken 不匹配: got=%q", tokenInfo.AccessToken)
	}
	if tokenInfo.RefreshToken != "new-refresh-token" {
		t.Fatalf("RefreshToken 不匹配: got=%q", tokenInfo.RefreshToken)
	}
	if tokenInfo.ExpiresIn != 7200 {
		t.Fatalf("ExpiresIn 不匹配: got=%d", tokenInfo.ExpiresIn)
	}
	if tokenInfo.ExpiresAt == 0 {
		t.Fatal("ExpiresAt 不应为 0")
	}
}

func TestOAuthService_RefreshToken_Error(t *testing.T) {
	t.Parallel()

	client := &mockClaudeOAuthClient{
		refreshTokenFunc: func(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error) {
			return nil, fmt.Errorf("invalid_grant: token expired")
		},
	}

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
	defer svc.Stop()

	_, err := svc.RefreshToken(context.Background(), "expired-token", "")
	if err == nil {
		t.Fatal("RefreshToken 应返回错误")
	}
}

func TestOAuthService_RefreshAccountToken_NoRefreshToken(t *testing.T) {
	t.Parallel()

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, &mockClaudeOAuthClient{})
	defer svc.Stop()

	// 无 refresh_token 的账号
	account := &Account{
		ID:       1,
		Platform: PlatformAnthropic,
		Type:     AccountTypeOAuth,
		Credentials: map[string]any{
			"access_token": "some-token",
		},
	}
	_, err := svc.RefreshAccountToken(context.Background(), account)
	if err == nil {
		t.Fatal("RefreshAccountToken 应返回错误（无 refresh_token）")
	}
	if err.Error() != "no refresh token available" {
		t.Fatalf("错误信息不匹配: got=%q", err.Error())
	}
}

func TestOAuthService_RefreshAccountToken_EmptyRefreshToken(t *testing.T) {
	t.Parallel()

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, &mockClaudeOAuthClient{})
	defer svc.Stop()

	account := &Account{
		ID:       2,
		Platform: PlatformAnthropic,
		Type:     AccountTypeOAuth,
		Credentials: map[string]any{
			"access_token":  "some-token",
			"refresh_token": "",
		},
	}
	_, err := svc.RefreshAccountToken(context.Background(), account)
	if err == nil {
		t.Fatal("RefreshAccountToken 应返回错误（refresh_token 为空）")
	}
}

func TestOAuthService_RefreshAccountToken_Success(t *testing.T) {
	t.Parallel()

	client := &mockClaudeOAuthClient{
		refreshTokenFunc: func(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error) {
			if refreshToken != "account-refresh-token" {
				t.Errorf("refreshToken 不匹配: got=%q", refreshToken)
			}
			return &oauth.TokenResponse{
				AccessToken:  "refreshed-access",
				TokenType:    "Bearer",
				ExpiresIn:    3600,
				RefreshToken: "new-refresh",
			}, nil
		},
	}

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
	defer svc.Stop()

	account := &Account{
		ID:       3,
		Platform: PlatformAnthropic,
		Type:     AccountTypeOAuth,
		Credentials: map[string]any{
			"access_token":  "old-access",
			"refresh_token": "account-refresh-token",
		},
	}

	tokenInfo, err := svc.RefreshAccountToken(context.Background(), account)
	if err != nil {
		t.Fatalf("RefreshAccountToken 返回错误: %v", err)
	}
	if tokenInfo.AccessToken != "refreshed-access" {
		t.Fatalf("AccessToken 不匹配: got=%q", tokenInfo.AccessToken)
	}
}

func TestOAuthService_RefreshAccountToken_SetupTokenSkipsProfile(t *testing.T) {
	t.Parallel()

	profileCalls := 0
	client := &mockClaudeOAuthProfileClient{
		mockClaudeOAuthClient: &mockClaudeOAuthClient{
			refreshTokenFunc: func(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error) {
				return &oauth.TokenResponse{
					AccessToken: "refreshed-setup-token",
					TokenType:   "Bearer",
					ExpiresIn:   3600,
				}, nil
			},
		},
		getOAuthProfileFunc: func(ctx context.Context, accessToken, proxyURL string) (*ClaudeOAuthProfile, error) {
			profileCalls++
			return &ClaudeOAuthProfile{OrganizationType: "claude_pro"}, nil
		},
	}

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
	defer svc.Stop()

	tokenInfo, err := svc.RefreshAccountToken(context.Background(), &Account{
		ID:       31,
		Platform: PlatformAnthropic,
		Type:     AccountTypeSetupToken,
		Credentials: map[string]any{
			"refresh_token": "setup-refresh-token",
		},
	})
	if err != nil {
		t.Fatalf("RefreshAccountToken 返回错误: %v", err)
	}
	if tokenInfo.AccessToken != "refreshed-setup-token" {
		t.Fatalf("AccessToken 不匹配: got=%q", tokenInfo.AccessToken)
	}
	if profileCalls != 0 {
		t.Fatalf("setup-token 刷新不应查询 OAuth profile，调用次数=%d", profileCalls)
	}
}

func TestOAuthService_RefreshAccountToken_WithProxy(t *testing.T) {
	t.Parallel()

	proxyRepo := &mockProxyRepoForOAuth{
		getByIDFunc: func(ctx context.Context, id int64) (*Proxy, error) {
			return &Proxy{
				Protocol: "socks5",
				Host:     "socks.example.com",
				Port:     1080,
				Username: "user",
				Password: "pass",
			}, nil
		},
	}

	client := &mockClaudeOAuthClient{
		refreshTokenFunc: func(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error) {
			if proxyURL != "socks5://user:pass@socks.example.com:1080" {
				t.Errorf("proxyURL 不匹配: got=%q", proxyURL)
			}
			return &oauth.TokenResponse{
				AccessToken: "refreshed",
				ExpiresIn:   3600,
			}, nil
		},
	}

	svc := NewOAuthService(proxyRepo, client)
	defer svc.Stop()

	proxyID := int64(10)
	account := &Account{
		ID:       4,
		Platform: PlatformAnthropic,
		Type:     AccountTypeOAuth,
		ProxyID:  &proxyID,
		Credentials: map[string]any{
			"refresh_token": "rt-with-proxy",
		},
	}

	_, err := svc.RefreshAccountToken(context.Background(), account)
	if err != nil {
		t.Fatalf("RefreshAccountToken 返回错误: %v", err)
	}
}

func TestOAuthService_RefreshAccountToken_EnrichesProfileWithAccountProxy(t *testing.T) {
	t.Parallel()

	const wantProxyURL = "socks5://user:pass@socks.example.com:1080"
	proxyRepo := &mockProxyRepoForOAuth{
		getByIDFunc: func(ctx context.Context, id int64) (*Proxy, error) {
			return &Proxy{
				Protocol: "socks5",
				Host:     "socks.example.com",
				Port:     1080,
				Username: "user",
				Password: "pass",
			}, nil
		},
	}

	client := &mockClaudeOAuthProfileClient{
		mockClaudeOAuthClient: &mockClaudeOAuthClient{
			refreshTokenFunc: func(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error) {
				if proxyURL != wantProxyURL {
					t.Errorf("refresh proxyURL = %q, want %q", proxyURL, wantProxyURL)
				}
				return &oauth.TokenResponse{
					AccessToken: "refreshed-access-token",
					TokenType:   "Bearer",
					ExpiresIn:   28800,
				}, nil
			},
		},
		getOAuthProfileFunc: func(ctx context.Context, accessToken, proxyURL string) (*ClaudeOAuthProfile, error) {
			if proxyURL != wantProxyURL {
				t.Errorf("profile proxyURL = %q, want %q", proxyURL, wantProxyURL)
			}
			return &ClaudeOAuthProfile{
				OrganizationType:       "claude_pro",
				RateLimitTier:          "default_claude_pro",
				SubscriptionExpiresAt: "2026-09-04T00:00:00Z",
			}, nil
		},
	}

	svc := NewOAuthService(proxyRepo, client)
	defer svc.Stop()

	proxyID := int64(10)
	tokenInfo, err := svc.RefreshAccountToken(context.Background(), &Account{
		ID:       4,
		Platform: PlatformAnthropic,
		Type:     AccountTypeOAuth,
		ProxyID:  &proxyID,
		Credentials: map[string]any{
			"refresh_token": "refresh-token",
		},
	})
	if err != nil {
		t.Fatalf("RefreshAccountToken 返回错误: %v", err)
	}
	if tokenInfo.PlanType != "pro" {
		t.Fatalf("PlanType = %q, want %q", tokenInfo.PlanType, "pro")
	}
	if tokenInfo.SubscriptionExpiresAt != "2026-09-04T00:00:00Z" {
		t.Fatalf("SubscriptionExpiresAt = %q, want explicit subscription expiry", tokenInfo.SubscriptionExpiresAt)
	}
}

func TestOAuthService_ExchangeCode_NilOrg(t *testing.T) {
	t.Parallel()

	client := &mockClaudeOAuthClient{
		exchangeCodeFunc: func(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*oauth.TokenResponse, error) {
			return &oauth.TokenResponse{
				AccessToken:  "token-no-org",
				TokenType:    "Bearer",
				ExpiresIn:    3600,
				Organization: nil,
				Account:      nil,
			}, nil
		},
	}

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, client)
	defer svc.Stop()

	result, _ := svc.GenerateAuthURL(context.Background(), nil)
	tokenInfo, err := svc.ExchangeCode(context.Background(), &ExchangeCodeInput{
		SessionID: result.SessionID,
		Code:      "code",
	})
	if err != nil {
		t.Fatalf("ExchangeCode 返回错误: %v", err)
	}
	if tokenInfo.OrgUUID != "" {
		t.Fatalf("OrgUUID 应为空: got=%q", tokenInfo.OrgUUID)
	}
	if tokenInfo.AccountUUID != "" {
		t.Fatalf("AccountUUID 应为空: got=%q", tokenInfo.AccountUUID)
	}
}

func TestOAuthService_Stop_NoPanic(t *testing.T) {
	t.Parallel()

	svc := NewOAuthService(&mockProxyRepoForOAuth{}, &mockClaudeOAuthClient{})

	// 调用 Stop 不应 panic
	svc.Stop()

	// 多次调用也不应 panic
	svc.Stop()
}
