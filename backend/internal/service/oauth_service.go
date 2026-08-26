package service

import (
	"context"
	"fmt"
	"log"
	"strings"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/pkg/oauth"
	"github.com/Wei-Shaw/sub2api/internal/pkg/openai"
	"github.com/Wei-Shaw/sub2api/internal/pkg/xai"
)

// OpenAIOAuthClient interface for OpenAI OAuth operations
type OpenAIOAuthClient interface {
	ExchangeCode(ctx context.Context, code, codeVerifier, redirectURI, proxyURL, clientID string) (*openai.TokenResponse, error)
	RefreshToken(ctx context.Context, refreshToken, proxyURL string) (*openai.TokenResponse, error)
	RefreshTokenWithClientID(ctx context.Context, refreshToken, proxyURL string, clientID string) (*openai.TokenResponse, error)
}

// GrokOAuthClient interface for xAI/Grok OAuth operations.
type GrokOAuthClient interface {
	ExchangeCode(ctx context.Context, code, codeVerifier, redirectURI, proxyURL, clientID string) (*xai.TokenResponse, error)
	RefreshToken(ctx context.Context, refreshToken, proxyURL, clientID string) (*xai.TokenResponse, error)
	// LoginWithPassword exchanges email/password for a short-lived Web SSO cookie.
	// Callers must convert via ConvertSSOToBuild and must not persist password or raw SSO.
	LoginWithPassword(ctx context.Context, email, password, proxyURL string) (*GrokPasswordLoginResult, error)
	ConvertSSOToBuild(ctx context.Context, ssoToken, proxyURL string) (*xai.TokenResponse, error)
}

// GrokOAuthTokenService is the narrow refresh port used by Grok token providers.
type GrokOAuthTokenService interface {
	RefreshAccountToken(ctx context.Context, account *Account) (*GrokTokenInfo, error)
	BuildAccountCredentials(tokenInfo *GrokTokenInfo) map[string]any
}

// ClaudeOAuthClient handles HTTP requests for Claude OAuth flows
type ClaudeOAuthClient interface {
	GetOrganizationUUID(ctx context.Context, sessionKey, proxyURL string) (string, error)
	GetAuthorizationCode(ctx context.Context, sessionKey, orgUUID, scope, codeChallenge, state, proxyURL string) (string, error)
	ExchangeCodeForToken(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*oauth.TokenResponse, error)
	RefreshToken(ctx context.Context, refreshToken, proxyURL string) (*oauth.TokenResponse, error)
}

// ClaudeOAuthProfile contains non-secret subscription metadata returned by
// Anthropic's OAuth profile endpoint.
type ClaudeOAuthProfile struct {
	OrganizationType       string
	RateLimitTier          string
	SubscriptionExpiresAt string
	HasClaudeMax           *bool
	HasClaudePro           *bool
}

// ClaudeOAuthProfileClient is an optional extension implemented by clients
// that can query Anthropic OAuth profile metadata. Keeping it separate avoids
// forcing every ClaudeOAuthClient mock and integration to implement it.
type ClaudeOAuthProfileClient interface {
	GetOAuthProfile(ctx context.Context, accessToken, proxyURL string) (*ClaudeOAuthProfile, error)
}

const claudeOAuthProfileTimeout = 8 * time.Second

// OAuthService handles OAuth authentication flows
type OAuthService struct {
	sessionStore *oauth.SessionStore
	proxyRepo    ProxyRepository
	oauthClient  ClaudeOAuthClient
}

// NewOAuthService creates a new OAuth service
func NewOAuthService(proxyRepo ProxyRepository, oauthClient ClaudeOAuthClient) *OAuthService {
	return &OAuthService{
		sessionStore: oauth.NewSessionStore(),
		proxyRepo:    proxyRepo,
		oauthClient:  oauthClient,
	}
}

// GenerateAuthURLResult contains the authorization URL and session info
type GenerateAuthURLResult struct {
	AuthURL   string `json:"auth_url"`
	SessionID string `json:"session_id"`
}

// GenerateAuthURL generates an OAuth authorization URL with full scope
func (s *OAuthService) GenerateAuthURL(ctx context.Context, proxyID *int64) (*GenerateAuthURLResult, error) {
	return s.generateAuthURLWithScope(ctx, oauth.ScopeOAuth, proxyID)
}

// GenerateSetupTokenURL generates an OAuth authorization URL for setup token (inference only)
func (s *OAuthService) GenerateSetupTokenURL(ctx context.Context, proxyID *int64) (*GenerateAuthURLResult, error) {
	scope := oauth.ScopeInference
	return s.generateAuthURLWithScope(ctx, scope, proxyID)
}

func (s *OAuthService) generateAuthURLWithScope(ctx context.Context, scope string, proxyID *int64) (*GenerateAuthURLResult, error) {
	// Generate PKCE values
	state, err := oauth.GenerateState()
	if err != nil {
		return nil, fmt.Errorf("failed to generate state: %w", err)
	}

	codeVerifier, err := oauth.GenerateCodeVerifier()
	if err != nil {
		return nil, fmt.Errorf("failed to generate code verifier: %w", err)
	}

	codeChallenge := oauth.GenerateCodeChallenge(codeVerifier)

	// Generate session ID
	sessionID, err := oauth.GenerateSessionID()
	if err != nil {
		return nil, fmt.Errorf("failed to generate session ID: %w", err)
	}

	// Get proxy URL if specified
	var proxyURL string
	if proxyID != nil {
		proxy, err := s.proxyRepo.GetByID(ctx, *proxyID)
		if err == nil && proxy != nil {
			proxyURL = proxy.URL()
		}
	}

	// Store session
	session := &oauth.OAuthSession{
		State:        state,
		CodeVerifier: codeVerifier,
		Scope:        scope,
		ProxyURL:     proxyURL,
		CreatedAt:    time.Now(),
	}
	s.sessionStore.Set(sessionID, session)

	// Build authorization URL
	authURL := oauth.BuildAuthorizationURL(state, codeChallenge, scope)

	return &GenerateAuthURLResult{
		AuthURL:   authURL,
		SessionID: sessionID,
	}, nil
}

// ExchangeCodeInput represents the input for code exchange
type ExchangeCodeInput struct {
	SessionID string
	Code      string
	ProxyID   *int64
}

// TokenInfo represents the token information stored in credentials
type TokenInfo struct {
	AccessToken           string `json:"access_token"`
	TokenType             string `json:"token_type"`
	ExpiresIn             int64  `json:"expires_in"`
	ExpiresAt             int64  `json:"expires_at"`
	RefreshToken          string `json:"refresh_token,omitempty"`
	Scope                 string `json:"scope,omitempty"`
	OrgUUID               string `json:"org_uuid,omitempty"`
	AccountUUID           string `json:"account_uuid,omitempty"`
	EmailAddress          string `json:"email_address,omitempty"`
	PlanType              string `json:"plan_type,omitempty"`
	SubscriptionExpiresAt string `json:"subscription_expires_at,omitempty"`
	// SubscriptionMetadataResolved distinguishes a successful authoritative
	// profile result from a best-effort lookup failure during credential merge.
	SubscriptionMetadataResolved bool `json:"-"`
}

// ExchangeCode exchanges authorization code for tokens
func (s *OAuthService) ExchangeCode(ctx context.Context, input *ExchangeCodeInput) (*TokenInfo, error) {
	// Get session
	session, ok := s.sessionStore.Get(input.SessionID)
	if !ok {
		return nil, fmt.Errorf("session not found or expired")
	}

	// Get proxy URL
	proxyURL := session.ProxyURL
	if input.ProxyID != nil {
		proxy, err := s.proxyRepo.GetByID(ctx, *input.ProxyID)
		if err == nil && proxy != nil {
			proxyURL = proxy.URL()
		}
	}

	// Determine if this is a setup token (scope is inference only)
	isSetupToken := session.Scope == oauth.ScopeInference

	// Exchange code for token
	tokenInfo, err := s.exchangeCodeForToken(ctx, input.Code, session.CodeVerifier, session.State, proxyURL, isSetupToken)
	if err != nil {
		return nil, err
	}

	// Delete session after successful exchange
	s.sessionStore.Delete(input.SessionID)

	return tokenInfo, nil
}

// CookieAuthInput represents the input for cookie-based authentication
type CookieAuthInput struct {
	SessionKey string
	ProxyID    *int64
	Scope      string // "full" or "inference"
}

// CookieAuth performs OAuth using sessionKey (cookie-based auto-auth)
func (s *OAuthService) CookieAuth(ctx context.Context, input *CookieAuthInput) (*TokenInfo, error) {
	// Get proxy URL if specified
	var proxyURL string
	if input.ProxyID != nil {
		proxy, err := s.proxyRepo.GetByID(ctx, *input.ProxyID)
		if err == nil && proxy != nil {
			proxyURL = proxy.URL()
		}
	}

	// Determine scope and if this is a setup token
	// Internal API call uses ScopeAPI (org:create_api_key not supported)
	scope := oauth.ScopeAPI
	isSetupToken := false
	if input.Scope == "inference" {
		scope = oauth.ScopeInference
		isSetupToken = true
	}

	// Step 1: Get organization info using sessionKey
	orgUUID, err := s.getOrganizationUUID(ctx, input.SessionKey, proxyURL)
	if err != nil {
		return nil, fmt.Errorf("failed to get organization info: %w", err)
	}

	// Step 2: Generate PKCE values
	codeVerifier, err := oauth.GenerateCodeVerifier()
	if err != nil {
		return nil, fmt.Errorf("failed to generate code verifier: %w", err)
	}
	codeChallenge := oauth.GenerateCodeChallenge(codeVerifier)

	state, err := oauth.GenerateState()
	if err != nil {
		return nil, fmt.Errorf("failed to generate state: %w", err)
	}

	// Step 3: Get authorization code using cookie
	authCode, err := s.getAuthorizationCode(ctx, input.SessionKey, orgUUID, scope, codeChallenge, state, proxyURL)
	if err != nil {
		return nil, fmt.Errorf("failed to get authorization code: %w", err)
	}

	// Step 4: Exchange code for token
	tokenInfo, err := s.exchangeCodeForToken(ctx, authCode, codeVerifier, state, proxyURL, isSetupToken)
	if err != nil {
		return nil, fmt.Errorf("failed to exchange code: %w", err)
	}

	// Ensure org_uuid is set (from step 1 if not from token response)
	if tokenInfo.OrgUUID == "" && orgUUID != "" {
		tokenInfo.OrgUUID = orgUUID
		log.Printf("[OAuth] Set org_uuid from cookie auth")
	}

	return tokenInfo, nil
}

// getOrganizationUUID gets the organization UUID from claude.ai using sessionKey
func (s *OAuthService) getOrganizationUUID(ctx context.Context, sessionKey, proxyURL string) (string, error) {
	return s.oauthClient.GetOrganizationUUID(ctx, sessionKey, proxyURL)
}

// getAuthorizationCode gets the authorization code using sessionKey
func (s *OAuthService) getAuthorizationCode(ctx context.Context, sessionKey, orgUUID, scope, codeChallenge, state, proxyURL string) (string, error) {
	return s.oauthClient.GetAuthorizationCode(ctx, sessionKey, orgUUID, scope, codeChallenge, state, proxyURL)
}

// exchangeCodeForToken exchanges authorization code for tokens
func (s *OAuthService) exchangeCodeForToken(ctx context.Context, code, codeVerifier, state, proxyURL string, isSetupToken bool) (*TokenInfo, error) {
	tokenResp, err := s.oauthClient.ExchangeCodeForToken(ctx, code, codeVerifier, state, proxyURL, isSetupToken)
	if err != nil {
		return nil, err
	}

	tokenInfo := &TokenInfo{
		AccessToken:  tokenResp.AccessToken,
		TokenType:    tokenResp.TokenType,
		ExpiresIn:    tokenResp.ExpiresIn,
		ExpiresAt:    time.Now().Unix() + tokenResp.ExpiresIn,
		RefreshToken: tokenResp.RefreshToken,
		Scope:        tokenResp.Scope,
	}

	if tokenResp.Organization != nil && tokenResp.Organization.UUID != "" {
		tokenInfo.OrgUUID = tokenResp.Organization.UUID
		log.Printf("[OAuth] Got org_uuid")
	}
	if tokenResp.Account != nil {
		if tokenResp.Account.UUID != "" {
			tokenInfo.AccountUUID = tokenResp.Account.UUID
			log.Printf("[OAuth] Got account_uuid")
		}
		if tokenResp.Account.EmailAddress != "" {
			tokenInfo.EmailAddress = tokenResp.Account.EmailAddress
			log.Printf("[OAuth] Got email_address")
		}
	}

	if !isSetupToken {
		s.enrichTokenInfoWithOAuthProfile(ctx, tokenInfo, proxyURL)
	}

	return tokenInfo, nil
}

// RefreshToken refreshes an OAuth token
func (s *OAuthService) RefreshToken(ctx context.Context, refreshToken string, proxyURL string) (*TokenInfo, error) {
	return s.refreshToken(ctx, refreshToken, proxyURL, true)
}

func (s *OAuthService) refreshToken(ctx context.Context, refreshToken, proxyURL string, enrichProfile bool) (*TokenInfo, error) {
	tokenResp, err := s.oauthClient.RefreshToken(ctx, refreshToken, proxyURL)
	if err != nil {
		return nil, err
	}

	tokenInfo := &TokenInfo{
		AccessToken:  tokenResp.AccessToken,
		TokenType:    tokenResp.TokenType,
		ExpiresIn:    tokenResp.ExpiresIn,
		ExpiresAt:    time.Now().Unix() + tokenResp.ExpiresIn,
		RefreshToken: tokenResp.RefreshToken,
		Scope:        tokenResp.Scope,
	}
	if enrichProfile {
		s.enrichTokenInfoWithOAuthProfile(ctx, tokenInfo, proxyURL)
	}
	return tokenInfo, nil
}

func (s *OAuthService) enrichTokenInfoWithOAuthProfile(ctx context.Context, tokenInfo *TokenInfo, proxyURL string) {
	if s == nil || tokenInfo == nil || strings.TrimSpace(tokenInfo.AccessToken) == "" {
		return
	}
	profileClient, ok := s.oauthClient.(ClaudeOAuthProfileClient)
	if !ok {
		return
	}

	profileCtx, cancel := context.WithTimeout(ctx, claudeOAuthProfileTimeout)
	defer cancel()

	profile, err := profileClient.GetOAuthProfile(profileCtx, tokenInfo.AccessToken, proxyURL)
	if err != nil {
		log.Printf("[WARN] Claude OAuth profile lookup failed: %v", err)
		return
	}
	if profile == nil {
		return
	}

	tokenInfo.PlanType = claudeOAuthPlanType(
		profile.OrganizationType,
		profile.RateLimitTier,
		profile.HasClaudeMax,
		profile.HasClaudePro,
	)
	tokenInfo.SubscriptionExpiresAt = strings.TrimSpace(profile.SubscriptionExpiresAt)
	tokenInfo.SubscriptionMetadataResolved = tokenInfo.PlanType != "" ||
		(profile.HasClaudeMax != nil && profile.HasClaudePro != nil)
}

func claudeOAuthPlanType(organizationType, rateLimitTier string, hasClaudeMax, hasClaudePro *bool) string {
	organizationType = normalizeClaudeOAuthPlanSignal(organizationType)
	rateLimitTier = normalizeClaudeOAuthPlanSignal(rateLimitTier)

	switch organizationType {
	case "enterprise", "claude_enterprise":
		return "enterprise"
	case "team", "claude_team":
		return "team"
	case "max", "claude_max":
		switch {
		case claudeOAuthTierIs(rateLimitTier, "max_20x", "max20x"):
			return "max_20x"
		case claudeOAuthTierIs(rateLimitTier, "max_5x", "max5x"):
			return "max_5x"
		default:
			return "max"
		}
	case "pro", "claude_pro":
		return "pro"
	case "free", "claude_free":
		return "free"
	}

	// Fall back to an explicit tier only when the profile omitted an
	// organization type that can identify the subscription.
	switch {
	case claudeOAuthTierIs(rateLimitTier, "max_20x", "max20x"):
		return "max_20x"
	case claudeOAuthTierIs(rateLimitTier, "max_5x", "max5x"):
		return "max_5x"
	case claudeOAuthTierIs(rateLimitTier, "max"):
		return "max"
	case claudeOAuthTierIs(rateLimitTier, "pro"):
		return "pro"
	case claudeOAuthTierIs(rateLimitTier, "enterprise"):
		return "enterprise"
	case claudeOAuthTierIs(rateLimitTier, "team"):
		return "team"
	case claudeOAuthTierIs(rateLimitTier, "free"):
		return "free"
	}

	if hasClaudeMax != nil && *hasClaudeMax {
		return "max"
	}
	if hasClaudePro != nil && *hasClaudePro {
		return "pro"
	}
	return ""
}

func claudeOAuthTierIs(rateLimitTier string, values ...string) bool {
	for _, value := range values {
		if rateLimitTier == value || strings.HasSuffix(rateLimitTier, "_"+value) {
			return true
		}
	}
	return false
}

func normalizeClaudeOAuthPlanSignal(value string) string {
	value = strings.ToLower(strings.TrimSpace(value))
	replacer := strings.NewReplacer("-", "_", " ", "_", "/", "_")
	return replacer.Replace(value)
}

// RefreshAccountToken refreshes token for an account
func (s *OAuthService) RefreshAccountToken(ctx context.Context, account *Account) (*TokenInfo, error) {
	refreshToken := account.GetCredential("refresh_token")
	if refreshToken == "" {
		return nil, fmt.Errorf("no refresh token available")
	}

	var proxyURL string
	if account.ProxyID != nil {
		proxy, err := s.proxyRepo.GetByID(ctx, *account.ProxyID)
		if err == nil && proxy != nil {
			proxyURL = proxy.URL()
		}
	}

	return s.refreshToken(ctx, refreshToken, proxyURL, account.Type == AccountTypeOAuth)
}

// Stop stops the session store cleanup goroutine
func (s *OAuthService) Stop() {
	s.sessionStore.Stop()
}
