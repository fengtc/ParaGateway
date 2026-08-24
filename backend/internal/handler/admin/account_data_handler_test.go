package admin

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

type dataResponse struct {
	Code int         `json:"code"`
	Data dataPayload `json:"data"`
}

type dataPayload struct {
	Type           string        `json:"type"`
	Version        int           `json:"version"`
	Proxies        []dataProxy   `json:"proxies"`
	Accounts       []dataAccount `json:"accounts"`
	SkippedShadows int           `json:"skipped_shadows"`
}

type dataProxy struct {
	ProxyKey string `json:"proxy_key"`
	Name     string `json:"name"`
	Protocol string `json:"protocol"`
	Host     string `json:"host"`
	Port     int    `json:"port"`
	Username string `json:"username"`
	Password string `json:"password"`
	Status   string `json:"status"`
}

type dataAccount struct {
	Name        string         `json:"name"`
	Platform    string         `json:"platform"`
	Type        string         `json:"type"`
	Credentials map[string]any `json:"credentials"`
	Extra       map[string]any `json:"extra"`
	ProxyKey    *string        `json:"proxy_key"`
	Concurrency int            `json:"concurrency"`
	Priority    int            `json:"priority"`
}

func setupAccountDataRouter() (*gin.Engine, *stubAdminService) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	adminSvc := newStubAdminService()

	h := NewAccountHandler(
		adminSvc,
		nil,
		nil,
		nil,
		nil,
		nil,
		nil,
		nil,
		nil,
		nil,
		nil,
		nil,
		nil,
		nil,
	)

	router.GET("/api/v1/admin/accounts/data", h.ExportData)
	router.POST("/api/v1/admin/accounts/data", h.ImportData)
	return router, adminSvc
}

func TestExportDataIncludesSecrets(t *testing.T) {
	router, adminSvc := setupAccountDataRouter()

	proxyID := int64(11)
	adminSvc.proxies = []service.Proxy{
		{
			ID:       proxyID,
			Name:     "proxy",
			Protocol: "http",
			Host:     "127.0.0.1",
			Port:     8080,
			Username: "user",
			Password: "pass",
			Status:   service.StatusActive,
		},
		{
			ID:       12,
			Name:     "orphan",
			Protocol: "https",
			Host:     "10.0.0.1",
			Port:     443,
			Username: "o",
			Password: "p",
			Status:   service.StatusActive,
		},
	}
	adminSvc.accounts = []service.Account{
		{
			ID:          21,
			Name:        "account",
			Platform:    service.PlatformOpenAI,
			Type:        service.AccountTypeOAuth,
			Credentials: map[string]any{"token": "secret"},
			Extra:       map[string]any{"note": "x"},
			ProxyID:     &proxyID,
			Concurrency: 3,
			Priority:    50,
			Status:      service.StatusDisabled,
		},
	}

	rec := httptest.NewRecorder()
	req := httptest.NewRequest(http.MethodGet, "/api/v1/admin/accounts/data", nil)
	router.ServeHTTP(rec, req)
	require.Equal(t, http.StatusOK, rec.Code)

	var resp dataResponse
	require.NoError(t, json.Unmarshal(rec.Body.Bytes(), &resp))
	require.Equal(t, 0, resp.Code)
	require.Empty(t, resp.Data.Type)
	require.Equal(t, 0, resp.Data.Version)
	require.Len(t, resp.Data.Proxies, 1)
	require.Equal(t, "pass", resp.Data.Proxies[0].Password)
	require.Len(t, resp.Data.Accounts, 1)
	require.Equal(t, "secret", resp.Data.Accounts[0].Credentials["token"])
}

func TestExportDataWithoutProxies(t *testing.T) {
	router, adminSvc := setupAccountDataRouter()

	proxyID := int64(11)
	adminSvc.proxies = []service.Proxy{
		{
			ID:       proxyID,
			Name:     "proxy",
			Protocol: "http",
			Host:     "127.0.0.1",
			Port:     8080,
			Username: "user",
			Password: "pass",
			Status:   service.StatusActive,
		},
	}
	adminSvc.accounts = []service.Account{
		{
			ID:          21,
			Name:        "account",
			Platform:    service.PlatformOpenAI,
			Type:        service.AccountTypeOAuth,
			Credentials: map[string]any{"token": "secret"},
			ProxyID:     &proxyID,
			Concurrency: 3,
			Priority:    50,
			Status:      service.StatusDisabled,
		},
	}

	rec := httptest.NewRecorder()
	req := httptest.NewRequest(http.MethodGet, "/api/v1/admin/accounts/data?include_proxies=false", nil)
	router.ServeHTTP(rec, req)
	require.Equal(t, http.StatusOK, rec.Code)

	var resp dataResponse
	require.NoError(t, json.Unmarshal(rec.Body.Bytes(), &resp))
	require.Equal(t, 0, resp.Code)
	require.Len(t, resp.Data.Proxies, 0)
	require.Len(t, resp.Data.Accounts, 1)
	require.Nil(t, resp.Data.Accounts[0].ProxyKey)
}

// TestExportDataExcludesSparkShadow 验证外审第5轮 P1/P2:导出时排除 spark 影子账号
// (影子无凭据、导入侧强制 credentials 非空,混入会产出无法还原的坏备份),并透出跳过计数。
func TestExportDataExcludesSparkShadow(t *testing.T) {
	router, adminSvc := setupAccountDataRouter()

	parentID := int64(21)
	adminSvc.accounts = []service.Account{
		{
			ID:          parentID,
			Name:        "mother",
			Platform:    service.PlatformOpenAI,
			Type:        service.AccountTypeOAuth,
			Credentials: map[string]any{"token": "secret"},
			Status:      service.StatusActive,
		},
		{
			ID:              22,
			Name:            "mother (Spark)",
			Platform:        service.PlatformOpenAI,
			Type:            service.AccountTypeOAuth,
			Credentials:     map[string]any{}, // 影子恒空凭据
			ParentAccountID: &parentID,        // 影子标记
			QuotaDimension:  service.QuotaDimensionSpark,
			Status:          service.StatusActive,
		},
	}

	rec := httptest.NewRecorder()
	req := httptest.NewRequest(http.MethodGet, "/api/v1/admin/accounts/data?include_proxies=false", nil)
	router.ServeHTTP(rec, req)
	require.Equal(t, http.StatusOK, rec.Code)

	var resp dataResponse
	require.NoError(t, json.Unmarshal(rec.Body.Bytes(), &resp))
	require.Equal(t, 0, resp.Code)
	require.Len(t, resp.Data.Accounts, 1, "影子应被排除,仅导出母账号")
	require.Equal(t, "mother", resp.Data.Accounts[0].Name)
	require.Equal(t, 1, resp.Data.SkippedShadows, "跳过的影子数量应透出")
}

func TestExportDataPassesAccountFiltersAndSort(t *testing.T) {
	router, adminSvc := setupAccountDataRouter()
	adminSvc.accounts = []service.Account{
		{ID: 1, Name: "acc-1", Status: service.StatusActive},
	}

	rec := httptest.NewRecorder()
	req := httptest.NewRequest(
		http.MethodGet,
		"/api/v1/admin/accounts/data?platform=openai&type=oauth&status=active&group=12&privacy_mode=blocked&search=keyword&sort_by=priority&sort_order=desc",
		nil,
	)
	router.ServeHTTP(rec, req)
	require.Equal(t, http.StatusOK, rec.Code)

	require.Equal(t, 1, adminSvc.lastListAccounts.calls)
	require.Equal(t, "openai", adminSvc.lastListAccounts.platform)
	require.Equal(t, "oauth", adminSvc.lastListAccounts.accountType)
	require.Equal(t, "active", adminSvc.lastListAccounts.status)
	require.Equal(t, int64(12), adminSvc.lastListAccounts.groupID)
	require.Equal(t, "blocked", adminSvc.lastListAccounts.privacyMode)
	require.Equal(t, "keyword", adminSvc.lastListAccounts.search)
	require.Equal(t, "priority", adminSvc.lastListAccounts.sortBy)
	require.Equal(t, "desc", adminSvc.lastListAccounts.sortOrder)
}

func TestExportDataSelectedIDsOverrideFilters(t *testing.T) {
	router, adminSvc := setupAccountDataRouter()

	rec := httptest.NewRecorder()
	req := httptest.NewRequest(
		http.MethodGet,
		"/api/v1/admin/accounts/data?ids=1,2&platform=openai&search=keyword&sort_by=priority&sort_order=desc",
		nil,
	)
	router.ServeHTTP(rec, req)
	require.Equal(t, http.StatusOK, rec.Code)

	var resp dataResponse
	require.NoError(t, json.Unmarshal(rec.Body.Bytes(), &resp))
	require.Equal(t, 0, resp.Code)
	require.Len(t, resp.Data.Accounts, 2)
	require.Equal(t, 0, adminSvc.lastListAccounts.calls)
}

func TestImportDataReusesProxyAndSkipsDefaultGroup(t *testing.T) {
	router, adminSvc := setupAccountDataRouter()

	adminSvc.proxies = []service.Proxy{
		{
			ID:       1,
			Name:     "proxy",
			Protocol: "socks5",
			Host:     "1.2.3.4",
			Port:     1080,
			Username: "u",
			Password: "p",
			Status:   service.StatusActive,
		},
	}

	dataPayload := map[string]any{
		"data": map[string]any{
			"type":    dataType,
			"version": dataVersion,
			"proxies": []map[string]any{
				{
					"proxy_key": "socks5|1.2.3.4|1080|u|p",
					"name":      "proxy",
					"protocol":  "socks5",
					"host":      "1.2.3.4",
					"port":      1080,
					"username":  "u",
					"password":  "p",
					"status":    "active",
				},
			},
			"accounts": []map[string]any{
				{
					"name":        "acc",
					"platform":    service.PlatformOpenAI,
					"type":        service.AccountTypeOAuth,
					"credentials": map[string]any{"token": "x"},
					"proxy_key":   "socks5|1.2.3.4|1080|u|p",
					"concurrency": 3,
					"priority":    50,
				},
			},
		},
		"skip_default_group_bind": true,
	}

	body, _ := json.Marshal(dataPayload)
	rec := httptest.NewRecorder()
	req := httptest.NewRequest(http.MethodPost, "/api/v1/admin/accounts/data", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	router.ServeHTTP(rec, req)
	require.Equal(t, http.StatusOK, rec.Code)

	require.Len(t, adminSvc.createdProxies, 0)
	require.Len(t, adminSvc.createdAccounts, 1)
	require.True(t, adminSvc.createdAccounts[0].SkipDefaultGroupBind)
}

func TestNormalizeLegacyCopilotImportPreservesAccountFieldsAndCredentials(t *testing.T) {
	notes := "legacy account"
	proxyKey := "https|proxy.example|443|user|pass"
	rateMultiplier := 1.25
	expiresAt := int64(1_800_000_000)
	item := DataAccount{
		Name:                          "legacy-copilot",
		Notes:                         &notes,
		Platform:                      "copilot",
		Type:                          service.AccountTypeAPIKey,
		Credentials:                   map[string]any{"github_token": "  gh-legacy\t", "billing_username": "octocat", "billing_pat": "billing-secret", "base_url": "https://api.individual.githubcopilot.com", "model_mapping": map[string]any{"claude-*": "claude-sonnet-4"}, "custom": "preserved"},
		Extra:                         map[string]any{"billing_credit_limit": 20000.0, "billing_safety_margin": 200.0},
		ProxyKey:                      &proxyKey,
		Concurrency:                   7,
		Priority:                      13,
		Weight:                        90,
		RPMLimit:                      21,
		CircuitBreakerThreshold:       3,
		CircuitBreakerCooldownSeconds: 60,
		RateMultiplier:                &rateMultiplier,
		ExpiresAt:                     &expiresAt,
	}
	originalCredentials := item.Credentials

	require.NoError(t, normalizeLegacyCopilotImport(&item))
	require.Equal(t, service.PlatformOpenAI, item.Platform)
	require.Equal(t, service.AccountTypeOAuth, item.Type)
	require.Equal(t, "gh-legacy", item.Credentials["github_access_token"])
	require.Equal(t, service.CopilotOAuthProfile, item.Credentials["oauth_profile"])
	require.NotContains(t, item.Credentials, "github_token")
	require.Equal(t, "  gh-legacy\t", originalCredentials["github_token"])
	require.NotContains(t, originalCredentials, "github_access_token")
	require.NotContains(t, originalCredentials, "oauth_profile")
	for key, want := range map[string]any{
		"billing_username": "octocat",
		"billing_pat":      "billing-secret",
		"base_url":         "https://api.individual.githubcopilot.com",
		"custom":           "preserved",
	} {
		require.Equal(t, want, item.Credentials[key], key)
	}
	require.Equal(t, map[string]any{"claude-*": "claude-sonnet-4"}, item.Credentials["model_mapping"])
	require.Equal(t, map[string]any{"billing_credit_limit": 20000.0, "billing_safety_margin": 200.0}, item.Extra)
	require.Equal(t, notes, *item.Notes)
	require.Equal(t, proxyKey, *item.ProxyKey)
	require.Equal(t, 7, item.Concurrency)
	require.Equal(t, 13, item.Priority)
	require.Equal(t, 90, item.Weight)
	require.Equal(t, 21, item.RPMLimit)
	require.Equal(t, 3, item.CircuitBreakerThreshold)
	require.Equal(t, 60, item.CircuitBreakerCooldownSeconds)
	require.Equal(t, rateMultiplier, *item.RateMultiplier)
	require.Equal(t, expiresAt, *item.ExpiresAt)
}

func TestNormalizeLegacyCopilotImportRejectsMalformedLegacyRecords(t *testing.T) {
	canonicalSecret := "canonical-secret-must-not-echo"
	tests := []struct {
		name string
		item DataAccount
		want string
	}{
		{
			name: "missing github token",
			item: DataAccount{Platform: "copilot", Type: service.AccountTypeAPIKey, Credentials: map[string]any{}},
			want: "non-empty github_token",
		},
		{
			name: "wrong type",
			item: DataAccount{Platform: "copilot", Type: service.AccountTypeOAuth, Credentials: map[string]any{"github_token": "gh-legacy"}},
			want: "type apikey",
		},
		{
			name: "canonical access token already present",
			item: DataAccount{Platform: "copilot", Type: service.AccountTypeAPIKey, Credentials: map[string]any{"github_token": "gh-legacy", "github_access_token": canonicalSecret}},
			want: "contains canonical OAuth credentials",
		},
		{
			name: "canonical OAuth profile already present",
			item: DataAccount{Platform: "copilot", Type: service.AccountTypeAPIKey, Credentials: map[string]any{"github_token": "gh-legacy", "oauth_profile": canonicalSecret}},
			want: "contains canonical OAuth credentials",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			err := normalizeLegacyCopilotImport(&tt.item)
			require.ErrorContains(t, err, tt.want)
			require.NotContains(t, err.Error(), canonicalSecret)
			require.Equal(t, "copilot", tt.item.Platform)
		})
	}
}

func TestNormalizeLegacyCopilotImportLeavesNonLegacyAccountUntouched(t *testing.T) {
	item := DataAccount{
		Name:        "ordinary-openai",
		Platform:    service.PlatformOpenAI,
		Type:        service.AccountTypeAPIKey,
		Credentials: map[string]any{"api_key": "sk-normal", "github_token": "unrelated-value"},
		Extra:       map[string]any{"preserved": true},
	}
	want := item

	require.NoError(t, normalizeLegacyCopilotImport(&item))
	require.Equal(t, want, item)
}

func TestImportDataLegacyCopilotNormalizesAndRejectsMalformedRecordsWithoutEchoingSecrets(t *testing.T) {
	router, adminSvc := setupAccountDataRouter()
	dataPayload := map[string]any{
		"data": map[string]any{
			"type":    dataType,
			"version": dataVersion,
			"proxies": []map[string]any{},
			"accounts": []map[string]any{
				{
					"name":     "legacy-valid",
					"platform": "copilot",
					"type":     "apikey",
					"credentials": map[string]any{
						"github_token":     "gh-import-secret",
						"billing_username": "octocat",
						"billing_pat":      "billing-import-secret",
						"base_url":         "https://api.individual.githubcopilot.com",
						"model_mapping":    map[string]any{"claude-*": "claude-sonnet-4"},
					},
					"extra":       map[string]any{"billing_credit_limit": 1000.0},
					"concurrency": 4,
					"priority":    9,
				},
				{
					"name":        "legacy-invalid",
					"platform":    "copilot",
					"type":        "apikey",
					"credentials": map[string]any{"billing_pat": "must-not-echo"},
				},
			},
		},
	}

	body, err := json.Marshal(dataPayload)
	require.NoError(t, err)
	rec := httptest.NewRecorder()
	req := httptest.NewRequest(http.MethodPost, "/api/v1/admin/accounts/data", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	router.ServeHTTP(rec, req)
	require.Equal(t, http.StatusOK, rec.Code)
	require.NotContains(t, rec.Body.String(), "gh-import-secret")
	require.NotContains(t, rec.Body.String(), "billing-import-secret")
	require.NotContains(t, rec.Body.String(), "must-not-echo")

	var response struct {
		Code int `json:"code"`
		Data struct {
			AccountCreated int               `json:"account_created"`
			AccountFailed  int               `json:"account_failed"`
			Errors         []DataImportError `json:"errors"`
		} `json:"data"`
	}
	require.NoError(t, json.Unmarshal(rec.Body.Bytes(), &response))
	require.Equal(t, 0, response.Code)
	require.Equal(t, 1, response.Data.AccountCreated)
	require.Equal(t, 1, response.Data.AccountFailed)
	require.Len(t, response.Data.Errors, 1)
	require.Equal(t, "legacy-invalid", response.Data.Errors[0].Name)
	require.Contains(t, response.Data.Errors[0].Message, "non-empty github_token")

	require.Len(t, adminSvc.createdAccounts, 1)
	created := adminSvc.createdAccounts[0]
	require.Equal(t, service.PlatformOpenAI, created.Platform)
	require.Equal(t, service.AccountTypeOAuth, created.Type)
	require.Equal(t, "gh-import-secret", created.Credentials["github_access_token"])
	require.Equal(t, service.CopilotOAuthProfile, created.Credentials["oauth_profile"])
	require.NotContains(t, created.Credentials, "github_token")
	require.Equal(t, "octocat", created.Credentials["billing_username"])
	require.Equal(t, "billing-import-secret", created.Credentials["billing_pat"])
	require.Equal(t, "https://api.individual.githubcopilot.com", created.Credentials["base_url"])
	require.Equal(t, map[string]any{"claude-*": "claude-sonnet-4"}, created.Credentials["model_mapping"])
	require.Equal(t, map[string]any{"billing_credit_limit": 1000.0}, created.Extra)
}
