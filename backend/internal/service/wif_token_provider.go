package service

import (
	"bytes"
	"context"
	"crypto/sha256"
	"crypto/tls"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/netip"
	"net/url"
	"regexp"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"golang.org/x/sync/singleflight"
)

const (
	wifAuthTypeExtraKey            = "auth_type"
	wifAuthType                    = "wif"
	wifSubjectTokenURLExtraKey     = "wif_subject_token_url"
	wifClientIDExtraKey            = "wif_client_id"
	wifClientAuthMethodExtraKey    = "wif_client_auth_method"
	wifAudienceExtraKey            = "wif_audience"
	wifScopeExtraKey               = "wif_scope"
	wifIdentityProviderIDExtraKey  = "wif_identity_provider_id"
	wifServiceAccountIDExtraKey    = "wif_service_account_id"
	wifFederationRuleIDExtraKey    = "wif_federation_rule_id"
	wifOrganizationIDExtraKey      = "wif_organization_id"
	wifWorkspaceIDExtraKey         = "wif_workspace_id"
	wifClientSecretCredentialKey   = "api_key"
	wifClientSecretCiphertextKey   = "wif_client_secret_ciphertext"
	wifOpenAITokenURL              = "https://auth.openai.com/oauth/token"
	wifAnthropicTokenURL           = "https://api.anthropic.com/v1/oauth/token"
	wifTokenRequestTimeout         = 10 * time.Second
	wifRefreshAttemptBeforeExpiry  = 2 * time.Minute
	wifRefreshRequiredBeforeExpiry = 30 * time.Second
	wifMaxTokenResponseBytes       = 64 * 1024
	wifMaxURLLength                = 2048
	wifMaxConfigurationValueLength = 16 * 1024
	wifMaxCacheEntries             = 128
)

var wifDNSLabelPattern = regexp.MustCompile(`^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$`)

var wifPrivateHostSuffixes = []string{
	".internal", ".invalid", ".local", ".localhost", ".example", ".onion", ".test", ".home.arpa",
}

var wifManagedExtraKeys = map[string]struct{}{
	wifAuthTypeExtraKey:           {},
	wifSubjectTokenURLExtraKey:    {},
	wifClientIDExtraKey:           {},
	wifClientAuthMethodExtraKey:   {},
	wifAudienceExtraKey:           {},
	wifScopeExtraKey:              {},
	wifIdentityProviderIDExtraKey: {},
	wifServiceAccountIDExtraKey:   {},
	wifFederationRuleIDExtraKey:   {},
	wifOrganizationIDExtraKey:     {},
	wifWorkspaceIDExtraKey:        {},
}

var wifNonPublicPrefixes = mustWIFPrefixes(
	"0.0.0.0/8",
	"10.0.0.0/8",
	"100.64.0.0/10",
	"127.0.0.0/8",
	"169.254.0.0/16",
	"172.16.0.0/12",
	"192.0.0.0/24",
	"192.0.2.0/24",
	"192.168.0.0/16",
	"192.88.99.0/24",
	"198.18.0.0/15",
	"198.51.100.0/24",
	"203.0.113.0/24",
	"224.0.0.0/4",
	"240.0.0.0/4",
	"::/128",
	"::1/128",
	"64:ff9b::/96",
	"64:ff9b:1::/48",
	"100::/64",
	"2001::/32",
	"2001:2::/48",
	"2001:10::/28",
	"2001:20::/28",
	"2001:db8::/32",
	"2002::/16",
	"fec0::/10",
	"fc00::/7",
	"fe80::/10",
	"ff00::/8",
)

type WIFClientAuthMethod string

const (
	WIFClientSecretBasic WIFClientAuthMethod = "client_secret_basic"
	WIFClientSecretPost  WIFClientAuthMethod = "client_secret_post"
)

// WIFConfiguration is the complete, validated token-exchange configuration for
// one OpenAI or Anthropic API-key account. The management write path accepts a
// client secret once through credentials.api_key, encrypts it before any
// repository call, and persists only ClientSecretCiphertext. ClientSecret only
// exists in memory while exchanging tokens. All other fields are non-secret and
// live in Account.Extra.
type WIFConfiguration struct {
	Platform               string
	SubjectTokenURL        string
	ClientID               string
	ClientSecret           string
	ClientSecretCiphertext string
	ClientAuthMethod       WIFClientAuthMethod
	Audience               string
	Scope                  string
	IdentityProviderID     string
	ServiceAccountID       string
	FederationRuleID       string
	OrganizationID         string
	WorkspaceID            string
}

type WIFTokenResult struct {
	AccessToken string
	ExpiresAt   time.Time
}

type WIFTokenError struct {
	Code    string
	Message string
}

func (e *WIFTokenError) Error() string {
	if e == nil {
		return "WIF token exchange failed"
	}
	return e.Message
}

func newWIFTokenError(code, message string) error {
	return &WIFTokenError{Code: code, Message: message}
}

type wifCachedToken struct {
	result   WIFTokenResult
	lastUsed time.Time
}

// WIFTokenProvider exchanges an external client-credentials JWT for an
// official OpenAI/Anthropic bearer token. Tokens are memory-only, keyed by a
// SHA-256 fingerprint of the full configuration, refreshed before expiry, and
// protected by singleflight to prevent a thundering herd.
type WIFTokenProvider struct {
	client               *http.Client
	now                  func() time.Time
	encryptor            SecretEncryptor
	persistentEncryption bool

	mu    sync.Mutex
	cache map[string]wifCachedToken
	group singleflight.Group
}

// NewWIFTokenProvider creates the production provider. Its HTTP transport is
// intentionally not injectable: every production instance must retain the
// DNS/IP validation, proxy disablement, TLS floor, redirect policy and timeouts
// enforced by hardenedWIFHTTPClient.
func NewWIFTokenProvider() *WIFTokenProvider {
	return &WIFTokenProvider{
		client: hardenedWIFHTTPClient(),
		now:    time.Now,
		cache:  make(map[string]wifCachedToken),
	}
}

// SetSecretEncryptor configures at-rest encryption. persistent must only be
// true when the key survives process restarts; otherwise new WIF accounts are
// rejected rather than persisted with undecryptable credentials.
func (p *WIFTokenProvider) SetSecretEncryptor(encryptor SecretEncryptor, persistent bool) {
	if p != nil {
		p.encryptor = encryptor
		p.persistentEncryption = persistent
	}
}

func (p *WIFTokenProvider) ResolveAccessToken(ctx context.Context, account *Account) (string, error) {
	if p == nil {
		return "", newWIFTokenError("wif_provider_unavailable", "WIF token provider is not configured")
	}
	configuration, enabled, err := WIFConfigurationFromAccount(account)
	if err != nil {
		return "", err
	}
	if !enabled {
		return "", newWIFTokenError("not_wif_account", "account is not configured for WIF authentication")
	}
	if p.encryptor == nil {
		return "", newWIFTokenError("wif_secret_decryption_unavailable", "WIF client secret cannot be decrypted")
	}
	clientSecret, decryptErr := p.encryptor.Decrypt(configuration.ClientSecretCiphertext)
	if decryptErr != nil || strings.TrimSpace(clientSecret) == "" {
		return "", newWIFTokenError("wif_secret_decryption_failed", "WIF client secret cannot be decrypted")
	}
	configuration.ClientSecret = clientSecret
	configuration.ClientSecretCiphertext = ""
	result, err := p.Resolve(ctx, configuration)
	if err != nil {
		return "", err
	}
	return result.AccessToken, nil
}

func (p *WIFTokenProvider) Resolve(ctx context.Context, configuration WIFConfiguration) (WIFTokenResult, error) {
	if p == nil {
		return WIFTokenResult{}, newWIFTokenError("wif_provider_unavailable", "WIF token provider is not configured")
	}
	normalized, err := normalizeWIFConfiguration(configuration)
	if err != nil {
		return WIFTokenResult{}, err
	}
	if normalized.ClientSecret == "" {
		return WIFTokenResult{}, invalidWIFConfiguration(wifClientSecretCredentialKey)
	}
	fingerprint := wifConfigurationFingerprint(normalized)
	now := p.currentTime()
	cached, hasCached := p.cachedToken(fingerprint, now)
	if hasCached && cached.ExpiresAt.Sub(now) > wifRefreshAttemptBeforeExpiry {
		return cached, nil
	}

	resultChannel := p.group.DoChan(fingerprint, func() (any, error) {
		// A shared refresh must not inherit the first caller's cancellation: later
		// waiters still need the result. Its own hard timeout bounds all I/O.
		refreshContext, cancel := context.WithTimeout(context.WithoutCancel(ctx), wifTokenRequestTimeout)
		defer cancel()
		result, exchangeErr := p.exchange(refreshContext, normalized)
		if exchangeErr != nil {
			return WIFTokenResult{}, exchangeErr
		}
		p.storeToken(fingerprint, result)
		return result, nil
	})

	select {
	case <-ctx.Done():
		return WIFTokenResult{}, ctx.Err()
	case flightResult := <-resultChannel:
		if flightResult.Err != nil {
			if hasCached && cached.ExpiresAt.Sub(p.currentTime()) > wifRefreshRequiredBeforeExpiry {
				return cached, nil
			}
			return WIFTokenResult{}, flightResult.Err
		}
		result, ok := flightResult.Val.(WIFTokenResult)
		if !ok {
			return WIFTokenResult{}, newWIFTokenError("invalid_provider_exchange_response", "WIF provider token response is invalid")
		}
		return result, nil
	}
}

func (p *WIFTokenProvider) Invalidate(account *Account) {
	configuration, enabled, err := WIFConfigurationFromAccount(account)
	if err != nil || !enabled || p == nil || p.encryptor == nil {
		return
	}
	clientSecret, err := p.encryptor.Decrypt(configuration.ClientSecretCiphertext)
	if err != nil || strings.TrimSpace(clientSecret) == "" {
		return
	}
	configuration.ClientSecret = clientSecret
	configuration.ClientSecretCiphertext = ""
	fingerprint := wifConfigurationFingerprint(configuration)
	p.mu.Lock()
	delete(p.cache, fingerprint)
	p.mu.Unlock()
	p.group.Forget(fingerprint)
}

func (p *WIFTokenProvider) currentTime() time.Time {
	if p != nil && p.now != nil {
		return p.now().UTC()
	}
	return time.Now().UTC()
}

func (p *WIFTokenProvider) cachedToken(fingerprint string, now time.Time) (WIFTokenResult, bool) {
	p.mu.Lock()
	defer p.mu.Unlock()
	entry, ok := p.cache[fingerprint]
	if !ok {
		return WIFTokenResult{}, false
	}
	if !entry.result.ExpiresAt.After(now) {
		delete(p.cache, fingerprint)
		return WIFTokenResult{}, false
	}
	entry.lastUsed = now
	p.cache[fingerprint] = entry
	return entry.result, true
}

func (p *WIFTokenProvider) storeToken(fingerprint string, result WIFTokenResult) {
	now := p.currentTime()
	p.mu.Lock()
	defer p.mu.Unlock()
	for key, entry := range p.cache {
		if !entry.result.ExpiresAt.After(now) {
			delete(p.cache, key)
		}
	}
	p.cache[fingerprint] = wifCachedToken{result: result, lastUsed: now}
	if len(p.cache) <= wifMaxCacheEntries {
		return
	}
	type cacheAge struct {
		key      string
		lastUsed time.Time
	}
	ages := make([]cacheAge, 0, len(p.cache))
	for key, entry := range p.cache {
		ages = append(ages, cacheAge{key: key, lastUsed: entry.lastUsed})
	}
	sort.Slice(ages, func(i, j int) bool { return ages[i].lastUsed.Before(ages[j].lastUsed) })
	for index := 0; len(p.cache) > wifMaxCacheEntries && index < len(ages); index++ {
		delete(p.cache, ages[index].key)
	}
}

func (p *WIFTokenProvider) exchange(ctx context.Context, configuration WIFConfiguration) (WIFTokenResult, error) {
	subjectToken, err := p.requestSubjectToken(ctx, configuration)
	if err != nil {
		return WIFTokenResult{}, err
	}
	response, err := p.exchangeProviderToken(ctx, configuration, subjectToken)
	if err != nil {
		return WIFTokenResult{}, err
	}
	accessToken, _ := response["access_token"].(string)
	accessToken = strings.TrimSpace(accessToken)
	expiresIn, ok := positiveJSONInteger(response["expires_in"])
	if accessToken == "" || strings.IndexFunc(accessToken, func(r rune) bool { return r == ' ' || r == '\t' || r == '\r' || r == '\n' }) >= 0 || !ok {
		return WIFTokenResult{}, newWIFTokenError("invalid_provider_exchange_response", "WIF provider token response is invalid")
	}
	now := p.currentTime()
	if expiresIn > int64((365*24*time.Hour)/time.Second) {
		return WIFTokenResult{}, newWIFTokenError("invalid_provider_exchange_response", "WIF provider token lifetime is invalid")
	}
	expiresAt := now.Add(time.Duration(expiresIn) * time.Second)
	if !expiresAt.After(now) {
		return WIFTokenResult{}, newWIFTokenError("invalid_provider_exchange_response", "WIF provider token lifetime is invalid")
	}
	return WIFTokenResult{AccessToken: accessToken, ExpiresAt: expiresAt}, nil
}

func (p *WIFTokenProvider) requestSubjectToken(ctx context.Context, configuration WIFConfiguration) (string, error) {
	form := url.Values{"grant_type": {"client_credentials"}}
	headers := make(http.Header)
	headers.Set("Content-Type", "application/x-www-form-urlencoded")
	headers.Set("Accept", "application/json")
	if configuration.ClientAuthMethod == WIFClientSecretBasic {
		material := formEncodeWIFBasicValue(configuration.ClientID) + ":" + formEncodeWIFBasicValue(configuration.ClientSecret)
		headers.Set("Authorization", "Basic "+base64.StdEncoding.EncodeToString([]byte(material)))
	} else {
		form.Set("client_id", configuration.ClientID)
		form.Set("client_secret", configuration.ClientSecret)
	}
	if configuration.Scope != "" {
		form.Set("scope", configuration.Scope)
	}
	if configuration.Audience != "" {
		form.Set("audience", configuration.Audience)
	}
	response, err := p.performTokenRequest(ctx, configuration.SubjectTokenURL, "application/x-www-form-urlencoded", []byte(form.Encode()), headers, "subject_token")
	if err != nil {
		return "", err
	}
	accessToken, _ := response["access_token"].(string)
	if !isCompactWIFJWT(accessToken) {
		return "", newWIFTokenError("invalid_subject_token_response", "WIF subject token response is invalid")
	}
	return accessToken, nil
}

func (p *WIFTokenProvider) exchangeProviderToken(ctx context.Context, configuration WIFConfiguration, subjectToken string) (map[string]any, error) {
	requestBody := make(map[string]string)
	tokenURL := wifOpenAITokenURL
	if configuration.Platform == PlatformOpenAI {
		requestBody = map[string]string{
			"grant_type":           "urn:ietf:params:oauth:grant-type:token-exchange",
			"subject_token_type":   "urn:ietf:params:oauth:token-type:jwt",
			"subject_token":        subjectToken,
			"identity_provider_id": configuration.IdentityProviderID,
			"service_account_id":   configuration.ServiceAccountID,
		}
	} else {
		tokenURL = wifAnthropicTokenURL
		requestBody = map[string]string{
			"grant_type":         "urn:ietf:params:oauth:grant-type:jwt-bearer",
			"assertion":          subjectToken,
			"federation_rule_id": configuration.FederationRuleID,
			"organization_id":    configuration.OrganizationID,
			"service_account_id": configuration.ServiceAccountID,
		}
		if configuration.WorkspaceID != "" {
			requestBody["workspace_id"] = configuration.WorkspaceID
		}
	}
	body, err := json.Marshal(requestBody)
	if err != nil {
		return nil, newWIFTokenError("invalid_wif_configuration", "WIF configuration is invalid")
	}
	return p.performTokenRequest(ctx, tokenURL, "application/json", body, nil, "provider_exchange")
}

func (p *WIFTokenProvider) performTokenRequest(ctx context.Context, targetURL, contentType string, body []byte, headers http.Header, stage string) (map[string]any, error) {
	request, err := http.NewRequestWithContext(ctx, http.MethodPost, targetURL, bytes.NewReader(body))
	if err != nil {
		return nil, wifRequestFailed(stage)
	}
	request.Header.Set("Content-Type", contentType)
	request.Header.Set("Accept", "application/json")
	request.Header.Set("Cache-Control", "no-store")
	for key, values := range headers {
		for _, value := range values {
			request.Header.Add(key, value)
		}
	}
	response, err := p.client.Do(request)
	if err != nil {
		return nil, wifRequestFailed(stage)
	}
	defer func() { _ = response.Body.Close() }()
	if response.StatusCode < http.StatusOK || response.StatusCode >= http.StatusMultipleChoices {
		_, _ = io.Copy(io.Discard, io.LimitReader(response.Body, 4096))
		return nil, wifRequestFailed(stage)
	}
	if response.ContentLength > wifMaxTokenResponseBytes {
		return nil, wifResponseTooLarge(stage)
	}
	limited := io.LimitReader(response.Body, wifMaxTokenResponseBytes+1)
	responseBody, err := io.ReadAll(limited)
	if err != nil {
		return nil, wifInvalidResponse(stage)
	}
	if len(responseBody) > wifMaxTokenResponseBytes {
		return nil, wifResponseTooLarge(stage)
	}
	decoder := json.NewDecoder(bytes.NewReader(responseBody))
	decoder.UseNumber()
	var value map[string]any
	if err := decoder.Decode(&value); err != nil || value == nil {
		return nil, wifInvalidResponse(stage)
	}
	var trailing any
	if err := decoder.Decode(&trailing); !errors.Is(err, io.EOF) {
		return nil, wifInvalidResponse(stage)
	}
	return value, nil
}

func wifRequestFailed(stage string) error {
	if stage == "subject_token" {
		return newWIFTokenError("subject_token_request_failed", "WIF subject token request failed")
	}
	return newWIFTokenError("provider_exchange_request_failed", "WIF provider token exchange failed")
}

func wifResponseTooLarge(stage string) error {
	if stage == "subject_token" {
		return newWIFTokenError("subject_token_response_too_large", "WIF subject token response is too large")
	}
	return newWIFTokenError("provider_exchange_response_too_large", "WIF provider token response is too large")
}

func wifInvalidResponse(stage string) error {
	if stage == "subject_token" {
		return newWIFTokenError("invalid_subject_token_response", "WIF subject token response is invalid")
	}
	return newWIFTokenError("invalid_provider_exchange_response", "WIF provider token response is invalid")
}

func positiveJSONInteger(value any) (int64, bool) {
	number, ok := value.(json.Number)
	if !ok {
		return 0, false
	}
	parsed, err := number.Int64()
	return parsed, err == nil && parsed > 0
}

func formEncodeWIFBasicValue(value string) string {
	encoded := url.Values{"value": {value}}.Encode()
	return strings.TrimPrefix(encoded, "value=")
}

func isCompactWIFJWT(value string) bool {
	parts := strings.Split(value, ".")
	if len(parts) != 3 {
		return false
	}
	for _, part := range parts {
		if part == "" {
			return false
		}
		for _, char := range part {
			if (char >= 'a' && char <= 'z') || (char >= 'A' && char <= 'Z') || (char >= '0' && char <= '9') || char == '-' || char == '_' {
				continue
			}
			return false
		}
	}
	return true
}

func (a *Account) IsWIF() bool {
	if a == nil || a.Type != AccountTypeAPIKey || (a.Platform != PlatformOpenAI && a.Platform != PlatformAnthropic) {
		return false
	}
	return strings.EqualFold(strings.TrimSpace(wifExtraString(a.Extra, wifAuthTypeExtraKey)), wifAuthType)
}

func isAnthropicBearerTokenType(tokenType string) bool {
	return tokenType == "oauth" || tokenType == wifAuthType
}

func WIFConfigurationFromAccount(account *Account) (WIFConfiguration, bool, error) {
	if account == nil || !account.IsWIF() {
		return WIFConfiguration{}, false, nil
	}
	configuration := WIFConfiguration{
		Platform:               account.Platform,
		SubjectTokenURL:        wifExtraString(account.Extra, wifSubjectTokenURLExtraKey),
		ClientID:               wifExtraString(account.Extra, wifClientIDExtraKey),
		ClientSecretCiphertext: account.GetCredential(wifClientSecretCiphertextKey),
		ClientAuthMethod:       WIFClientAuthMethod(wifExtraString(account.Extra, wifClientAuthMethodExtraKey)),
		Audience:               wifExtraString(account.Extra, wifAudienceExtraKey),
		Scope:                  wifExtraString(account.Extra, wifScopeExtraKey),
		IdentityProviderID:     wifExtraString(account.Extra, wifIdentityProviderIDExtraKey),
		ServiceAccountID:       wifExtraString(account.Extra, wifServiceAccountIDExtraKey),
		FederationRuleID:       wifExtraString(account.Extra, wifFederationRuleIDExtraKey),
		OrganizationID:         wifExtraString(account.Extra, wifOrganizationIDExtraKey),
		WorkspaceID:            wifExtraString(account.Extra, wifWorkspaceIDExtraKey),
	}
	normalized, err := normalizeWIFConfiguration(configuration)
	if err != nil {
		return WIFConfiguration{}, true, err
	}
	if normalized.ClientSecretCiphertext == "" {
		return WIFConfiguration{}, true, invalidWIFConfiguration(wifClientSecretCiphertextKey)
	}
	if err := validateWIFOfficialUpstream(account); err != nil {
		return WIFConfiguration{}, true, err
	}
	return normalized, true, nil
}

// PrepareAccountForPersistence converts the one-time plaintext input in
// credentials.api_key into AES-GCM ciphertext and removes the plaintext before
// any repository call. Existing ciphertext is preserved by the normal
// sensitive-credential merge rules on edits.
func (p *WIFTokenProvider) PrepareAccountForPersistence(account *Account) error {
	if account == nil {
		return nil
	}
	if !account.IsWIF() {
		if account.Credentials != nil {
			if _, hadWIFCiphertext := account.Credentials[wifClientSecretCiphertextKey]; hadWIFCiphertext {
				delete(account.Credentials, wifClientSecretCiphertextKey)
				clearDisabledWIFExtra(account)
				if (account.Type == AccountTypeAPIKey || account.Type == AccountTypeUpstream) &&
					strings.TrimSpace(account.GetCredential(wifClientSecretCredentialKey)) == "" {
					return infraerrors.BadRequest("WIF_DISABLE_REQUIRES_API_KEY", "a new API key is required when disabling WIF authentication")
				}
			}
		}
		return nil
	}
	if p == nil || p.encryptor == nil || !p.persistentEncryption {
		return infraerrors.BadRequest("WIF_ENCRYPTION_KEY_REQUIRED", "WIF requires a fixed secret encryption key")
	}
	if account.Credentials == nil {
		account.Credentials = make(map[string]any)
	}
	plaintext := strings.TrimSpace(account.GetCredential(wifClientSecretCredentialKey))
	if plaintext != "" {
		if len(plaintext) > wifMaxConfigurationValueLength {
			return infraerrors.BadRequest("INVALID_WIF_CONFIGURATION", invalidWIFConfiguration(wifClientSecretCredentialKey).Error())
		}
		ciphertext, err := p.encryptor.Encrypt(plaintext)
		if err != nil {
			return infraerrors.New(http.StatusInternalServerError, "WIF_SECRET_ENCRYPTION_FAILED", "WIF client secret could not be encrypted")
		}
		account.Credentials[wifClientSecretCiphertextKey] = ciphertext
		delete(account.Credentials, wifClientSecretCredentialKey)
	}
	return ValidateAndNormalizeWIFAccountConfiguration(account)
}

// clearDisabledWIFExtra removes the now-inert federation configuration after
// an account leaves WIF. auth_type is shared account metadata, so only remove
// it when its value is specifically "wif"; this avoids clobbering a future
// target account type's own authentication marker.
func clearDisabledWIFExtra(account *Account) {
	if account == nil || account.Extra == nil {
		return
	}
	for key := range wifManagedExtraKeys {
		if key == wifAuthTypeExtraKey {
			continue
		}
		delete(account.Extra, key)
	}
	if strings.EqualFold(wifExtraString(account.Extra, wifAuthTypeExtraKey), wifAuthType) {
		delete(account.Extra, wifAuthTypeExtraKey)
	}
}

// ValidateAndNormalizeWIFAccountConfiguration is called before account writes.
// It rejects incomplete configurations and canonicalizes the subject-token URL,
// preventing an account from entering the scheduler in a partially configured
// state. No route or new persisted secret type is introduced.
func ValidateAndNormalizeWIFAccountConfiguration(account *Account) error {
	configuration, enabled, err := WIFConfigurationFromAccount(account)
	if !enabled {
		return nil
	}
	if err != nil {
		return infraerrors.BadRequest("INVALID_WIF_CONFIGURATION", err.Error())
	}
	if account.Extra == nil {
		account.Extra = make(map[string]any)
	}
	account.Extra[wifAuthTypeExtraKey] = wifAuthType
	account.Extra[wifSubjectTokenURLExtraKey] = configuration.SubjectTokenURL
	account.Extra[wifClientIDExtraKey] = configuration.ClientID
	account.Extra[wifClientAuthMethodExtraKey] = string(configuration.ClientAuthMethod)
	account.Extra[wifServiceAccountIDExtraKey] = configuration.ServiceAccountID
	setOrDeleteWIFExtra(account.Extra, wifAudienceExtraKey, configuration.Audience)
	setOrDeleteWIFExtra(account.Extra, wifScopeExtraKey, configuration.Scope)
	setOrDeleteWIFExtra(account.Extra, wifIdentityProviderIDExtraKey, configuration.IdentityProviderID)
	setOrDeleteWIFExtra(account.Extra, wifFederationRuleIDExtraKey, configuration.FederationRuleID)
	setOrDeleteWIFExtra(account.Extra, wifOrganizationIDExtraKey, configuration.OrganizationID)
	setOrDeleteWIFExtra(account.Extra, wifWorkspaceIDExtraKey, configuration.WorkspaceID)
	return nil
}

func setOrDeleteWIFExtra(extra map[string]any, key, value string) {
	if value == "" {
		delete(extra, key)
		return
	}
	extra[key] = value
}

func containsWIFManagedExtraKey(extra map[string]any) bool {
	for key := range extra {
		if _, managed := wifManagedExtraKeys[key]; managed {
			return true
		}
	}
	return false
}

func wifCredentialBoundary(account *Account) string {
	if account == nil || !account.IsWIF() {
		return ""
	}
	return strings.Join([]string{
		strings.TrimSpace(wifExtraString(account.Extra, wifSubjectTokenURLExtraKey)),
		strings.TrimSpace(wifExtraString(account.Extra, wifClientIDExtraKey)),
		strings.ToLower(strings.TrimSpace(wifExtraString(account.Extra, wifClientAuthMethodExtraKey))),
	}, "\x00")
}

func wifExtraString(extra map[string]any, key string) string {
	if extra == nil {
		return ""
	}
	value, _ := extra[key].(string)
	return strings.TrimSpace(value)
}

func normalizeWIFConfiguration(configuration WIFConfiguration) (WIFConfiguration, error) {
	configuration.Platform = strings.ToLower(strings.TrimSpace(configuration.Platform))
	if configuration.Platform != PlatformOpenAI && configuration.Platform != PlatformAnthropic {
		return WIFConfiguration{}, invalidWIFConfiguration("platform")
	}
	var err error
	configuration.SubjectTokenURL, err = validateAndNormalizeWIFSubjectTokenURL(configuration.SubjectTokenURL)
	if err != nil {
		return WIFConfiguration{}, err
	}
	configuration.ClientID, err = requiredWIFString(configuration.ClientID, "wif_client_id")
	if err != nil {
		return WIFConfiguration{}, err
	}
	configuration.ClientSecret = strings.TrimSpace(configuration.ClientSecret)
	configuration.ClientSecretCiphertext = strings.TrimSpace(configuration.ClientSecretCiphertext)
	if len(configuration.ClientSecret) > wifMaxConfigurationValueLength || len(configuration.ClientSecretCiphertext) > wifMaxConfigurationValueLength*2 {
		return WIFConfiguration{}, invalidWIFConfiguration(wifClientSecretCredentialKey)
	}
	configuration.ServiceAccountID, err = requiredWIFString(configuration.ServiceAccountID, "wif_service_account_id")
	if err != nil {
		return WIFConfiguration{}, err
	}
	configuration.ClientAuthMethod = WIFClientAuthMethod(strings.TrimSpace(string(configuration.ClientAuthMethod)))
	if configuration.ClientAuthMethod != WIFClientSecretBasic && configuration.ClientAuthMethod != WIFClientSecretPost {
		return WIFConfiguration{}, invalidWIFConfiguration("wif_client_auth_method")
	}
	for field, value := range map[string]*string{
		"wif_audience":     &configuration.Audience,
		"wif_scope":        &configuration.Scope,
		"wif_workspace_id": &configuration.WorkspaceID,
	} {
		*value = strings.TrimSpace(*value)
		if len(*value) > wifMaxConfigurationValueLength {
			return WIFConfiguration{}, invalidWIFConfiguration(field)
		}
	}
	if configuration.Platform == PlatformOpenAI {
		configuration.IdentityProviderID, err = requiredWIFString(configuration.IdentityProviderID, "wif_identity_provider_id")
		if err != nil {
			return WIFConfiguration{}, err
		}
		configuration.FederationRuleID = ""
		configuration.OrganizationID = ""
		configuration.WorkspaceID = ""
	} else {
		configuration.FederationRuleID, err = requiredWIFString(configuration.FederationRuleID, "wif_federation_rule_id")
		if err != nil {
			return WIFConfiguration{}, err
		}
		configuration.OrganizationID, err = requiredWIFString(configuration.OrganizationID, "wif_organization_id")
		if err != nil {
			return WIFConfiguration{}, err
		}
		configuration.IdentityProviderID = ""
	}
	return configuration, nil
}

func requiredWIFString(value, field string) (string, error) {
	value = strings.TrimSpace(value)
	if value == "" || len(value) > wifMaxConfigurationValueLength {
		return "", invalidWIFConfiguration(field)
	}
	return value, nil
}

func invalidWIFConfiguration(field string) error {
	return newWIFTokenError("invalid_wif_configuration", fmt.Sprintf("WIF configuration field %s is missing or invalid", field))
}

func validateAndNormalizeWIFSubjectTokenURL(value string) (string, error) {
	value = strings.TrimSpace(value)
	if value == "" || len(value) > wifMaxURLLength {
		return "", invalidWIFConfiguration(wifSubjectTokenURLExtraKey)
	}
	parsed, err := url.Parse(value)
	if err != nil || !strings.EqualFold(parsed.Scheme, "https") || parsed.Opaque != "" || parsed.User != nil || parsed.RawQuery != "" || parsed.ForceQuery || parsed.Fragment != "" {
		return "", invalidWIFConfiguration(wifSubjectTokenURLExtraKey)
	}
	hostname := strings.ToLower(strings.TrimSuffix(parsed.Hostname(), "."))
	if !isPublicWIFDNSHostname(hostname) {
		return "", invalidWIFConfiguration(wifSubjectTokenURLExtraKey)
	}
	if port := parsed.Port(); port != "" {
		portNumber, parseErr := strconv.Atoi(port)
		if parseErr != nil || portNumber < 1 || portNumber > 65535 {
			return "", invalidWIFConfiguration(wifSubjectTokenURLExtraKey)
		}
		parsed.Host = net.JoinHostPort(hostname, port)
	} else {
		parsed.Host = hostname
	}
	parsed.Scheme = "https"
	return parsed.String(), nil
}

func isPublicWIFDNSHostname(hostname string) bool {
	if hostname == "" || len(hostname) > 253 || !strings.Contains(hostname, ".") || strings.Contains(hostname, ":") || net.ParseIP(hostname) != nil {
		return false
	}
	for _, label := range strings.Split(hostname, ".") {
		if len(label) == 0 || len(label) > 63 || !wifDNSLabelPattern.MatchString(label) {
			return false
		}
	}
	for _, suffix := range wifPrivateHostSuffixes {
		if hostname == strings.TrimPrefix(suffix, ".") || strings.HasSuffix(hostname, suffix) {
			return false
		}
	}
	return true
}

func validateWIFOfficialUpstream(account *Account) error {
	if account == nil {
		return invalidWIFConfiguration("account")
	}
	expectedHost := "api.openai.com"
	baseURL := account.GetOpenAIBaseURL()
	if account.Platform == PlatformAnthropic {
		expectedHost = "api.anthropic.com"
		baseURL = account.GetBaseURL()
	}
	baseURL = strings.TrimSpace(baseURL)
	if baseURL == "" {
		return nil
	}
	parsed, err := url.Parse(baseURL)
	if err != nil || !strings.EqualFold(parsed.Scheme, "https") || parsed.Opaque != "" || parsed.User != nil || parsed.RawQuery != "" || parsed.ForceQuery || parsed.Fragment != "" || parsed.Port() != "" || !strings.EqualFold(strings.TrimSuffix(parsed.Hostname(), "."), expectedHost) {
		return newWIFTokenError("invalid_wif_upstream", "WIF bearer tokens may only be sent to the official provider API")
	}
	path := strings.TrimRight(parsed.EscapedPath(), "/")
	if path != "" && path != "/v1" {
		return newWIFTokenError("invalid_wif_upstream", "WIF bearer tokens may only be sent to the official provider API")
	}
	return nil
}

func wifConfigurationFingerprint(configuration WIFConfiguration) string {
	material, _ := json.Marshal([]string{
		configuration.Platform,
		configuration.SubjectTokenURL,
		configuration.ClientID,
		configuration.ClientSecret,
		string(configuration.ClientAuthMethod),
		configuration.Audience,
		configuration.Scope,
		configuration.IdentityProviderID,
		configuration.ServiceAccountID,
		configuration.FederationRuleID,
		configuration.OrganizationID,
		configuration.WorkspaceID,
	})
	digest := sha256.Sum256(material)
	return hex.EncodeToString(digest[:])
}

func hardenedWIFHTTPClient() *http.Client {
	return &http.Client{
		Transport: newWIFTransport(),
		Timeout:   wifTokenRequestTimeout,
		CheckRedirect: func(_ *http.Request, _ []*http.Request) error {
			return errors.New("WIF token redirects are disabled")
		},
	}
}

func newWIFTransport() *http.Transport {
	dialer := &net.Dialer{Timeout: 5 * time.Second, KeepAlive: 30 * time.Second}
	return &http.Transport{
		Proxy:                 nil,
		DialContext:           secureWIFDialContext(dialer, net.DefaultResolver),
		ForceAttemptHTTP2:     true,
		TLSHandshakeTimeout:   5 * time.Second,
		ResponseHeaderTimeout: wifTokenRequestTimeout,
		ExpectContinueTimeout: time.Second,
		IdleConnTimeout:       30 * time.Second,
		MaxIdleConns:          16,
		MaxIdleConnsPerHost:   4,
		TLSClientConfig:       &tls.Config{MinVersion: tls.VersionTLS12},
	}
}

type wifIPResolver interface {
	LookupIPAddr(ctx context.Context, host string) ([]net.IPAddr, error)
}

func secureWIFDialContext(dialer *net.Dialer, resolver wifIPResolver) func(context.Context, string, string) (net.Conn, error) {
	return func(ctx context.Context, network, address string) (net.Conn, error) {
		host, port, err := net.SplitHostPort(address)
		if err != nil || !isPublicWIFDNSHostname(strings.ToLower(strings.TrimSuffix(host, "."))) {
			return nil, errors.New("WIF destination is not a public DNS hostname")
		}
		addresses, err := resolver.LookupIPAddr(ctx, host)
		if err != nil || len(addresses) == 0 {
			return nil, errors.New("WIF destination DNS lookup failed")
		}
		publicAddresses := make([]net.IP, 0, len(addresses))
		for _, resolved := range addresses {
			if !isPublicWIFIP(resolved.IP) {
				// Reject mixed public/private answers as a DNS-rebinding defense.
				return nil, errors.New("WIF destination resolved to a non-public address")
			}
			publicAddresses = append(publicAddresses, resolved.IP)
		}
		var lastErr error
		for _, resolved := range publicAddresses {
			connection, dialErr := dialer.DialContext(ctx, network, net.JoinHostPort(resolved.String(), port))
			if dialErr == nil {
				return connection, nil
			}
			lastErr = dialErr
		}
		if lastErr == nil {
			lastErr = errors.New("WIF destination connection failed")
		}
		return nil, lastErr
	}
}

func isPublicWIFIP(ip net.IP) bool {
	address, ok := netip.AddrFromSlice(ip)
	if !ok {
		return false
	}
	address = address.Unmap()
	if !address.IsValid() || !address.IsGlobalUnicast() || address.IsPrivate() || address.IsLoopback() || address.IsLinkLocalUnicast() || address.IsLinkLocalMulticast() || address.IsMulticast() || address.IsUnspecified() {
		return false
	}
	for _, prefix := range wifNonPublicPrefixes {
		if prefix.Contains(address) {
			return false
		}
	}
	return true
}

func mustWIFPrefixes(values ...string) []netip.Prefix {
	result := make([]netip.Prefix, 0, len(values))
	for _, value := range values {
		result = append(result, netip.MustParsePrefix(value))
	}
	return result
}
