package service

import (
	"context"
	"crypto/tls"
	"encoding/base64"
	"encoding/json"
	"errors"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

type wifRoundTripFunc func(*http.Request) (*http.Response, error)

func (f wifRoundTripFunc) RoundTrip(request *http.Request) (*http.Response, error) {
	return f(request)
}

// newWIFTokenProviderForTest is deliberately test-only. Production callers
// have no HTTP transport injection point and always use NewWIFTokenProvider's
// SSRF-hardened transport.
func newWIFTokenProviderForTest(client *http.Client) *WIFTokenProvider {
	provider := NewWIFTokenProvider()
	clone := *client
	clone.Timeout = wifTokenRequestTimeout
	clone.CheckRedirect = func(_ *http.Request, _ []*http.Request) error {
		return errors.New("WIF token redirects are disabled")
	}
	provider.client = &clone
	return provider
}

func TestWIFProductionProviderAlwaysUsesHardenedTransport(t *testing.T) {
	provider := NewWIFTokenProvider()
	require.NotNil(t, provider.client)
	require.Equal(t, wifTokenRequestTimeout, provider.client.Timeout)
	require.NotNil(t, provider.client.CheckRedirect)
	require.Error(t, provider.client.CheckRedirect(&http.Request{}, nil))

	transport, ok := provider.client.Transport.(*http.Transport)
	require.True(t, ok, "production WIF provider must use the SSRF-hardened transport")
	require.Nil(t, transport.Proxy, "environment proxies must not receive WIF credentials")
	require.NotNil(t, transport.DialContext, "DNS/IP validation must be installed")
	require.NotNil(t, transport.TLSClientConfig)
	require.Equal(t, uint16(tls.VersionTLS12), transport.TLSClientConfig.MinVersion)
}

type wifTestEncryptor struct{}

func (wifTestEncryptor) Encrypt(plaintext string) (string, error) {
	return "wif.enc." + base64.RawURLEncoding.EncodeToString([]byte(plaintext)), nil
}

func (wifTestEncryptor) Decrypt(ciphertext string) (string, error) {
	encoded, ok := strings.CutPrefix(ciphertext, "wif.enc.")
	if !ok {
		return "", errors.New("invalid ciphertext")
	}
	plaintext, err := base64.RawURLEncoding.DecodeString(encoded)
	if err != nil {
		return "", errors.New("invalid ciphertext")
	}
	return string(plaintext), nil
}

type wifCapturedRequest struct {
	URL           string
	Authorization string
	ContentType   string
	Body          string
}

func wifJSONResponse(request *http.Request, status int, body string) *http.Response {
	return &http.Response{
		StatusCode: status,
		Header:     make(http.Header),
		Body:       io.NopCloser(strings.NewReader(body)),
		Request:    request,
	}
}

func captureWIFRequest(request *http.Request) (wifCapturedRequest, error) {
	body, err := io.ReadAll(request.Body)
	if err != nil {
		return wifCapturedRequest{}, err
	}
	return wifCapturedRequest{
		URL:           request.URL.String(),
		Authorization: request.Header.Get("Authorization"),
		ContentType:   request.Header.Get("Content-Type"),
		Body:          string(body),
	}, nil
}

func openAIWIFAccount(clientSecret string) *Account {
	credentials := map[string]any{"base_url": "https://api.openai.com"}
	if clientSecret != "" {
		credentials[wifClientSecretCredentialKey] = clientSecret
	}
	return &Account{
		ID:          101,
		Platform:    PlatformOpenAI,
		Type:        AccountTypeAPIKey,
		Credentials: credentials,
		Extra: map[string]any{
			wifAuthTypeExtraKey:           wifAuthType,
			wifSubjectTokenURLExtraKey:    "https://issuer.example.com/oauth/token",
			wifClientIDExtraKey:           "client id",
			wifClientAuthMethodExtraKey:   string(WIFClientSecretBasic),
			wifAudienceExtraKey:           "openai-audience",
			wifScopeExtraKey:              "read write",
			wifIdentityProviderIDExtraKey: "idp-1",
			wifServiceAccountIDExtraKey:   "service-1",
		},
	}
}

func anthropicWIFAccount(clientSecret string) *Account {
	credentials := map[string]any{"base_url": "https://api.anthropic.com"}
	if clientSecret != "" {
		credentials[wifClientSecretCredentialKey] = clientSecret
	}
	return &Account{
		ID:          102,
		Platform:    PlatformAnthropic,
		Type:        AccountTypeAPIKey,
		Credentials: credentials,
		Extra: map[string]any{
			wifAuthTypeExtraKey:         wifAuthType,
			wifSubjectTokenURLExtraKey:  "https://issuer.example.com/oauth/token",
			wifClientIDExtraKey:         "anthropic-client",
			wifClientAuthMethodExtraKey: string(WIFClientSecretPost),
			wifFederationRuleIDExtraKey: "rule-1",
			wifOrganizationIDExtraKey:   "org-1",
			wifServiceAccountIDExtraKey: "service-2",
			wifWorkspaceIDExtraKey:      "workspace-1",
		},
	}
}

func TestWIFPrepareAccountForPersistenceEncryptsAndRemovesPlaintext(t *testing.T) {
	provider := NewWIFTokenProvider()
	provider.SetSecretEncryptor(wifTestEncryptor{}, true)
	account := openAIWIFAccount("do-not-persist-this-secret")

	require.NoError(t, provider.PrepareAccountForPersistence(account))
	require.NotContains(t, account.Credentials, wifClientSecretCredentialKey)
	ciphertext := account.GetCredential(wifClientSecretCiphertextKey)
	require.NotEmpty(t, ciphertext)
	require.NotEqual(t, "do-not-persist-this-secret", ciphertext)
	plaintext, err := (wifTestEncryptor{}).Decrypt(ciphertext)
	require.NoError(t, err)
	require.Equal(t, "do-not-persist-this-secret", plaintext)

	configuration, enabled, err := WIFConfigurationFromAccount(account)
	require.NoError(t, err)
	require.True(t, enabled)
	require.Empty(t, configuration.ClientSecret)
	require.Equal(t, ciphertext, configuration.ClientSecretCiphertext)
}

func TestWIFPrepareAccountRequiresPersistentEncryptionKey(t *testing.T) {
	provider := NewWIFTokenProvider()
	provider.SetSecretEncryptor(wifTestEncryptor{}, false)
	account := openAIWIFAccount("never-persist")

	err := provider.PrepareAccountForPersistence(account)
	require.Error(t, err)
	require.Contains(t, err.Error(), "fixed secret encryption key")
	require.Equal(t, "never-persist", account.GetCredential(wifClientSecretCredentialKey))
	require.Empty(t, account.GetCredential(wifClientSecretCiphertextKey))
}

func TestWIFPrepareAccountDisableCredentialRequirementsFollowTargetType(t *testing.T) {
	provider := NewWIFTokenProvider()
	provider.SetSecretEncryptor(wifTestEncryptor{}, true)

	for _, targetType := range []string{
		AccountTypeOAuth,
		AccountTypeSetupToken,
		AccountTypeBedrock,
		AccountTypeServiceAccount,
	} {
		t.Run(targetType, func(t *testing.T) {
			account := openAIWIFAccount("old-wif-secret")
			require.NoError(t, provider.PrepareAccountForPersistence(account))
			require.NotEmpty(t, account.GetCredential(wifClientSecretCiphertextKey))

			account.Type = targetType
			account.Credentials["access_token"] = "target-type-credential"
			require.NoError(t, provider.PrepareAccountForPersistence(account))
			require.Empty(t, account.GetCredential(wifClientSecretCiphertextKey))
			require.Equal(t, "target-type-credential", account.GetCredential("access_token"))
			require.NotEqual(t, wifAuthType, wifExtraString(account.Extra, wifAuthTypeExtraKey))
			require.NotContains(t, account.Extra, wifSubjectTokenURLExtraKey)
		})
	}

	for _, targetType := range []string{AccountTypeAPIKey, AccountTypeUpstream} {
		t.Run(targetType+"_requires_new_api_key", func(t *testing.T) {
			account := openAIWIFAccount("old-wif-secret")
			require.NoError(t, provider.PrepareAccountForPersistence(account))
			account.Type = targetType
			delete(account.Extra, wifAuthTypeExtraKey)

			err := provider.PrepareAccountForPersistence(account)
			require.Error(t, err)
			require.Contains(t, err.Error(), "new API key is required")
		})

		t.Run(targetType+"_accepts_new_api_key", func(t *testing.T) {
			account := openAIWIFAccount("old-wif-secret")
			require.NoError(t, provider.PrepareAccountForPersistence(account))
			account.Type = targetType
			delete(account.Extra, wifAuthTypeExtraKey)
			account.Credentials[wifClientSecretCredentialKey] = "new-provider-api-key"

			require.NoError(t, provider.PrepareAccountForPersistence(account))
			require.Empty(t, account.GetCredential(wifClientSecretCiphertextKey))
			require.Equal(t, "new-provider-api-key", account.GetCredential(wifClientSecretCredentialKey))
		})
	}
}

func TestWIFCannotReachChatGPTLiveTransport(t *testing.T) {
	account := openAIWIFAccount("must-never-reach-chatgpt")
	service := &OpenAIGatewayService{}

	_, err := service.createUpstreamLiveCall(context.Background(), account, &LiveCallRequest{}, "attestation")
	require.ErrorIs(t, err, ErrLiveUnavailable)

	_, err = service.liveSidebandHeaders(context.Background(), account, nil)
	require.ErrorIs(t, err, ErrLiveUnavailable)
}

func TestWIFOpenAIBasicExchangeGatewayAndCache(t *testing.T) {
	var mu sync.Mutex
	requests := make([]wifCapturedRequest, 0, 2)
	client := &http.Client{Transport: wifRoundTripFunc(func(request *http.Request) (*http.Response, error) {
		captured, err := captureWIFRequest(request)
		if err != nil {
			return nil, err
		}
		mu.Lock()
		requests = append(requests, captured)
		mu.Unlock()
		switch request.URL.Hostname() {
		case "issuer.example.com":
			return wifJSONResponse(request, http.StatusOK, `{"access_token":"aaa.bbb.ccc"}`), nil
		case "auth.openai.com":
			return wifJSONResponse(request, http.StatusOK, `{"access_token":"openai-wif-token","expires_in":3600}`), nil
		default:
			return nil, errors.New("unexpected host")
		}
	})}
	provider := newWIFTokenProviderForTest(client)
	provider.SetSecretEncryptor(wifTestEncryptor{}, true)
	account := openAIWIFAccount("client:secret/value")
	require.NoError(t, provider.PrepareAccountForPersistence(account))
	persistedSnapshot, err := json.Marshal(account)
	require.NoError(t, err)
	require.NotContains(t, string(persistedSnapshot), "client:secret/value")
	var hydratedAccount Account
	require.NoError(t, json.Unmarshal(persistedSnapshot, &hydratedAccount))
	account = &hydratedAccount

	openAIProvider := NewOpenAITokenProvider(nil, nil, nil)
	openAIProvider.SetWIFTokenProvider(provider)
	gateway := &OpenAIGatewayService{openAITokenProvider: openAIProvider}
	for range 2 {
		token, tokenType, err := gateway.GetAccessToken(context.Background(), account)
		require.NoError(t, err)
		require.Equal(t, "openai-wif-token", token)
		require.Equal(t, wifAuthType, tokenType)
	}

	mu.Lock()
	captured := append([]wifCapturedRequest(nil), requests...)
	mu.Unlock()
	require.Len(t, captured, 2, "second gateway resolution must use the WIF cache")
	require.Equal(t, "https://issuer.example.com/oauth/token", captured[0].URL)
	require.Equal(t, "application/x-www-form-urlencoded", captured[0].ContentType)
	decodedBasic, err := base64.StdEncoding.DecodeString(strings.TrimPrefix(captured[0].Authorization, "Basic "))
	require.NoError(t, err)
	require.Equal(t, "client+id:client%3Asecret%2Fvalue", string(decodedBasic))
	subjectForm, err := url.ParseQuery(captured[0].Body)
	require.NoError(t, err)
	require.Equal(t, "client_credentials", subjectForm.Get("grant_type"))
	require.Equal(t, "openai-audience", subjectForm.Get("audience"))
	require.Equal(t, "read write", subjectForm.Get("scope"))
	require.Empty(t, subjectForm.Get("client_secret"))
	require.NotContains(t, captured[0].Body, "client:secret/value")

	require.Equal(t, wifOpenAITokenURL, captured[1].URL)
	var exchange map[string]string
	require.NoError(t, json.Unmarshal([]byte(captured[1].Body), &exchange))
	require.Equal(t, "aaa.bbb.ccc", exchange["subject_token"])
	require.Equal(t, "idp-1", exchange["identity_provider_id"])
	require.Equal(t, "service-1", exchange["service_account_id"])
	require.NotContains(t, captured[1].Body, "client:secret/value")
}

func TestWIFAnthropicPostExchangeGatewayAndBearerHeader(t *testing.T) {
	var mu sync.Mutex
	requests := make([]wifCapturedRequest, 0, 2)
	client := &http.Client{Transport: wifRoundTripFunc(func(request *http.Request) (*http.Response, error) {
		captured, err := captureWIFRequest(request)
		if err != nil {
			return nil, err
		}
		mu.Lock()
		requests = append(requests, captured)
		mu.Unlock()
		switch request.URL.Hostname() {
		case "issuer.example.com":
			return wifJSONResponse(request, http.StatusOK, `{"access_token":"ddd.eee.fff"}`), nil
		case "api.anthropic.com":
			return wifJSONResponse(request, http.StatusOK, `{"access_token":"anthropic-wif-token","expires_in":1800}`), nil
		default:
			return nil, errors.New("unexpected host")
		}
	})}
	provider := newWIFTokenProviderForTest(client)
	provider.SetSecretEncryptor(wifTestEncryptor{}, true)
	account := anthropicWIFAccount("anthropic-client-secret")
	require.NoError(t, provider.PrepareAccountForPersistence(account))
	persistedSnapshot, err := json.Marshal(account)
	require.NoError(t, err)
	require.NotContains(t, string(persistedSnapshot), "anthropic-client-secret")
	var hydratedAccount Account
	require.NoError(t, json.Unmarshal(persistedSnapshot, &hydratedAccount))
	account = &hydratedAccount

	claudeProvider := NewClaudeTokenProvider(nil, nil, nil)
	claudeProvider.SetWIFTokenProvider(provider)
	gateway := &GatewayService{claudeTokenProvider: claudeProvider}
	token, tokenType, err := gateway.GetAccessToken(context.Background(), account)
	require.NoError(t, err)
	require.Equal(t, "anthropic-wif-token", token)
	require.Equal(t, wifAuthType, tokenType)

	mu.Lock()
	captured := append([]wifCapturedRequest(nil), requests...)
	mu.Unlock()
	require.Len(t, captured, 2)
	require.Empty(t, captured[0].Authorization)
	subjectForm, err := url.ParseQuery(captured[0].Body)
	require.NoError(t, err)
	require.Equal(t, "anthropic-client", subjectForm.Get("client_id"))
	require.Equal(t, "anthropic-client-secret", subjectForm.Get("client_secret"))

	require.Equal(t, wifAnthropicTokenURL, captured[1].URL)
	var exchange map[string]string
	require.NoError(t, json.Unmarshal([]byte(captured[1].Body), &exchange))
	require.Equal(t, "ddd.eee.fff", exchange["assertion"])
	require.Equal(t, "rule-1", exchange["federation_rule_id"])
	require.Equal(t, "org-1", exchange["organization_id"])
	require.Equal(t, "service-2", exchange["service_account_id"])
	require.Equal(t, "workspace-1", exchange["workspace_id"])
	require.NotContains(t, captured[1].Body, "anthropic-client-secret")

	gin.SetMode(gin.TestMode)
	recorder := httptest.NewRecorder()
	ginContext, _ := gin.CreateTestContext(recorder)
	ginContext.Request = httptest.NewRequest(http.MethodPost, "/v1/messages", nil)
	ginContext.Request.Header.Set("Authorization", "Bearer inbound-secret")
	ginContext.Request.Header.Set("X-Api-Key", "inbound-api-key")
	gateway.cfg = &config.Config{Security: config.SecurityConfig{URLAllowlist: config.URLAllowlistConfig{Enabled: false}}}
	upstreamRequest, _, err := gateway.buildUpstreamRequestAnthropicAPIKeyPassthroughWithTokenType(
		context.Background(), ginContext, account, []byte(`{"model":"claude-sonnet-4"}`), token, tokenType,
	)
	require.NoError(t, err)
	require.Equal(t, "Bearer anthropic-wif-token", getHeaderRaw(upstreamRequest.Header, "authorization"))
	require.Empty(t, getHeaderRaw(upstreamRequest.Header, "x-api-key"))
}

func TestWIFResolveUsesSingleflight(t *testing.T) {
	var requests atomic.Int32
	client := &http.Client{Transport: wifRoundTripFunc(func(request *http.Request) (*http.Response, error) {
		requests.Add(1)
		if request.URL.Hostname() == "issuer.example.com" {
			time.Sleep(40 * time.Millisecond)
			return wifJSONResponse(request, http.StatusOK, `{"access_token":"aaa.bbb.ccc"}`), nil
		}
		if request.URL.Hostname() == "auth.openai.com" {
			return wifJSONResponse(request, http.StatusOK, `{"access_token":"shared-token","expires_in":3600}`), nil
		}
		return nil, errors.New("unexpected host")
	})}
	provider := newWIFTokenProviderForTest(client)
	configuration := WIFConfiguration{
		Platform:           PlatformOpenAI,
		SubjectTokenURL:    "https://issuer.example.com/token",
		ClientID:           "client",
		ClientSecret:       "secret",
		ClientAuthMethod:   WIFClientSecretPost,
		IdentityProviderID: "idp",
		ServiceAccountID:   "service",
	}

	const callers = 20
	start := make(chan struct{})
	results := make(chan error, callers)
	for range callers {
		go func() {
			<-start
			result, err := provider.Resolve(context.Background(), configuration)
			if err == nil && result.AccessToken != "shared-token" {
				err = errors.New("unexpected token")
			}
			results <- err
		}()
	}
	close(start)
	for range callers {
		require.NoError(t, <-results)
	}
	require.Equal(t, int32(2), requests.Load(), "one subject request and one provider exchange expected")
}

func TestWIFStaleTokenFallbackHonorsThirtySecondFloor(t *testing.T) {
	now := time.Date(2026, time.August, 15, 12, 0, 0, 0, time.UTC)
	var subjectCalls atomic.Int32
	client := &http.Client{Transport: wifRoundTripFunc(func(request *http.Request) (*http.Response, error) {
		if request.URL.Hostname() == "issuer.example.com" {
			if subjectCalls.Add(1) > 1 {
				return nil, errors.New("secret-containing-network-error-must-be-redacted")
			}
			return wifJSONResponse(request, http.StatusOK, `{"access_token":"aaa.bbb.ccc"}`), nil
		}
		return wifJSONResponse(request, http.StatusOK, `{"access_token":"stale-token","expires_in":300}`), nil
	})}
	provider := newWIFTokenProviderForTest(client)
	provider.now = func() time.Time { return now }
	configuration := WIFConfiguration{
		Platform:           PlatformOpenAI,
		SubjectTokenURL:    "https://issuer.example.com/token",
		ClientID:           "client",
		ClientSecret:       "top-secret-value",
		ClientAuthMethod:   WIFClientSecretPost,
		IdentityProviderID: "idp",
		ServiceAccountID:   "service",
	}

	first, err := provider.Resolve(context.Background(), configuration)
	require.NoError(t, err)
	now = now.Add(190 * time.Second) // 110 seconds remain: refresh, then stale fallback.
	stale, err := provider.Resolve(context.Background(), configuration)
	require.NoError(t, err)
	require.Equal(t, first.AccessToken, stale.AccessToken)

	now = now.Add(90 * time.Second) // 20 seconds remain: stale token is no longer safe.
	_, err = provider.Resolve(context.Background(), configuration)
	require.Error(t, err)
	require.NotContains(t, err.Error(), "top-secret-value")
	require.NotContains(t, err.Error(), "secret-containing-network-error")
}

func TestWIFSubjectTokenURLAndAddressValidation(t *testing.T) {
	valid, err := validateAndNormalizeWIFSubjectTokenURL(" HTTPS://ISSUER.EXAMPLE.COM:8443/oauth/token ")
	require.NoError(t, err)
	require.Equal(t, "https://issuer.example.com:8443/oauth/token", valid)

	invalidURLs := []string{
		"http://issuer.example.com/token",
		"https://localhost/token",
		"https://issuer.local/token",
		"https://127.0.0.1/token",
		"https://[::1]/token",
		"https://single-label/token",
		"https://user:password@issuer.example.com/token",
		"https://issuer.example.com/token?audience=x",
		"https://issuer.example.com/token?",
		"https://issuer.example.com/token#fragment",
		"https://issuer.example.com:70000/token",
	}
	for _, rawURL := range invalidURLs {
		t.Run(rawURL, func(t *testing.T) {
			_, err := validateAndNormalizeWIFSubjectTokenURL(rawURL)
			require.Error(t, err)
			require.NotContains(t, err.Error(), rawURL, "validation errors must not echo configured URLs")
		})
	}

	publicAddresses := []string{"8.8.8.8", "1.1.1.1", "2606:4700:4700::1111"}
	for _, address := range publicAddresses {
		require.True(t, isPublicWIFIP(net.ParseIP(address)), address)
	}
	privateOrSpecial := []string{
		"0.0.0.0", "10.0.0.1", "100.64.0.1", "127.0.0.1", "169.254.169.254",
		"192.0.2.1", "192.168.1.1", "198.18.0.1", "203.0.113.1", "255.255.255.255",
		"::1", "64:ff9b::a00:1", "2001::1", "2001:db8::1", "2002:0a00:0001::1", "fc00::1", "fe80::1",
	}
	for _, address := range privateOrSpecial {
		require.False(t, isPublicWIFIP(net.ParseIP(address)), address)
	}
}

func TestWIFCredentialBoundaryDetectsSecretDestinationChanges(t *testing.T) {
	account := openAIWIFAccount("secret")
	original := wifCredentialBoundary(account)
	require.NotEmpty(t, original)

	account.Extra[wifIdentityProviderIDExtraKey] = "another-idp"
	require.Equal(t, original, wifCredentialBoundary(account), "provider exchange IDs do not expose the client secret")
	account.Extra[wifSubjectTokenURLExtraKey] = "https://new-issuer.example.com/token"
	require.NotEqual(t, original, wifCredentialBoundary(account), "issuer changes must require a new client secret")

	account = openAIWIFAccount("secret")
	account.Extra[wifClientIDExtraKey] = "another-client"
	require.NotEqual(t, original, wifCredentialBoundary(account), "client ID changes must require a new client secret")

	account = openAIWIFAccount("secret")
	account.Extra[wifClientAuthMethodExtraKey] = string(WIFClientSecretPost)
	require.NotEqual(t, original, wifCredentialBoundary(account), "client authentication method changes must require a new client secret")
}

func TestWIFRejectsCustomProviderUpstreamAndRedactsExchangeErrors(t *testing.T) {
	provider := newWIFTokenProviderForTest(&http.Client{Transport: wifRoundTripFunc(func(request *http.Request) (*http.Response, error) {
		return wifJSONResponse(request, http.StatusUnauthorized, `{"error":"client-secret-in-body"}`), nil
	})})
	provider.SetSecretEncryptor(wifTestEncryptor{}, true)
	account := openAIWIFAccount("client-secret-in-body")
	account.Credentials["base_url"] = "https://relay.example.com"
	err := provider.PrepareAccountForPersistence(account)
	require.Error(t, err)
	require.NotContains(t, err.Error(), "relay.example.com")
	require.NotContains(t, err.Error(), "client-secret-in-body")
	require.NotContains(t, account.Credentials, wifClientSecretCredentialKey)

	configuration := WIFConfiguration{
		Platform:           PlatformOpenAI,
		SubjectTokenURL:    "https://issuer.example.com/token",
		ClientID:           "client",
		ClientSecret:       "client-secret-in-body",
		ClientAuthMethod:   WIFClientSecretPost,
		IdentityProviderID: "idp",
		ServiceAccountID:   "service",
	}
	_, err = provider.Resolve(context.Background(), configuration)
	require.Error(t, err)
	require.Equal(t, "WIF subject token request failed", err.Error())
	require.NotContains(t, err.Error(), "client-secret-in-body")
}
