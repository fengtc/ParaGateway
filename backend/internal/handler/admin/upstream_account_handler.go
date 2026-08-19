package admin

import (
	"net/http"
	"strings"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/pkg/response"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
)

// UpstreamAccountHandler manages the independent Worker-style upstream pool.
// It never reads or writes the official accounts table.
type UpstreamAccountHandler struct {
	service *service.UpstreamAccountService
}

func NewUpstreamAccountHandler(upstream *service.UpstreamAccountService) *UpstreamAccountHandler {
	return &UpstreamAccountHandler{service: upstream}
}

type upstreamAccountRequest struct {
	Name                          string `json:"name"`
	ProviderType                  string `json:"provider_type"`
	BaseURL                       string `json:"base_url"`
	AuthType                      string `json:"auth_type"`
	APIKey                        string `json:"api_key"`
	WIFClientSecret               string `json:"wif_client_secret"`
	WIFSubjectTokenURL            string `json:"wif_subject_token_url"`
	WIFClientID                   string `json:"wif_client_id"`
	WIFClientAuthMethod           string `json:"wif_client_auth_method"`
	WIFAudience                   string `json:"wif_audience"`
	WIFScope                      string `json:"wif_scope"`
	WIFIdentityProviderID         string `json:"wif_identity_provider_id"`
	WIFServiceAccountID           string `json:"wif_service_account_id"`
	WIFFederationRuleID           string `json:"wif_federation_rule_id"`
	WIFOrganizationID             string `json:"wif_organization_id"`
	WIFWorkspaceID                string `json:"wif_workspace_id"`
	IsActive                      bool   `json:"is_active"`
	Priority                      int    `json:"priority"`
	Weight                        int    `json:"weight"`
	MaxConcurrency                int    `json:"max_concurrency"`
	RPMLimit                      int    `json:"rpm_limit"`
	CircuitBreakerThreshold       int    `json:"circuit_breaker_threshold"`
	CircuitBreakerCooldownSeconds int    `json:"circuit_breaker_cooldown_seconds"`
}

type upstreamAccountDTO struct {
	ID                            string                  `json:"id"`
	Name                          string                  `json:"name"`
	ProviderType                  string                  `json:"provider_type"`
	BaseURL                       string                  `json:"base_url"`
	AuthType                      string                  `json:"auth_type"`
	MaskedCredential              string                  `json:"masked_credential"`
	OAuthProfile                  *string                 `json:"oauth_profile,omitempty"`
	OAuthAccountID                *string                 `json:"oauth_account_id,omitempty"`
	OAuthEmail                    *string                 `json:"oauth_email,omitempty"`
	OAuthExpiresAt                *time.Time              `json:"oauth_expires_at,omitempty"`
	WIFSubjectTokenURL            *string                 `json:"wif_subject_token_url,omitempty"`
	WIFClientID                   *string                 `json:"wif_client_id,omitempty"`
	WIFClientAuthMethod           *string                 `json:"wif_client_auth_method,omitempty"`
	WIFAudience                   *string                 `json:"wif_audience,omitempty"`
	WIFScope                      *string                 `json:"wif_scope,omitempty"`
	WIFIdentityProviderID         *string                 `json:"wif_identity_provider_id,omitempty"`
	WIFServiceAccountID           *string                 `json:"wif_service_account_id,omitempty"`
	WIFFederationRuleID           *string                 `json:"wif_federation_rule_id,omitempty"`
	WIFOrganizationID             *string                 `json:"wif_organization_id,omitempty"`
	WIFWorkspaceID                *string                 `json:"wif_workspace_id,omitempty"`
	IsActive                      bool                    `json:"is_active"`
	Priority                      int                     `json:"priority"`
	Weight                        int                     `json:"weight"`
	MaxConcurrency                int                     `json:"max_concurrency"`
	RPMLimit                      int                     `json:"rpm_limit"`
	CircuitBreakerThreshold       int                     `json:"circuit_breaker_threshold"`
	CircuitBreakerCooldownSeconds int                     `json:"circuit_breaker_cooldown_seconds"`
	QuotaStatus                   string                  `json:"quota_status"`
	QuotaUtilization              *float64                `json:"quota_utilization,omitempty"`
	QuotaResetsAt                 *time.Time              `json:"quota_resets_at,omitempty"`
	QuotaCheckedAt                *time.Time              `json:"quota_checked_at,omitempty"`
	UsageWindows                  upstreamUsageWindowsDTO `json:"usage_windows"`
	CooldownUntil                 *time.Time              `json:"cooldown_until,omitempty"`
	CooldownReason                *string                 `json:"cooldown_reason,omitempty"`
	LastUpstreamStatus            *int                    `json:"last_upstream_status,omitempty"`
	LastSuccessAt                 *time.Time              `json:"last_success_at,omitempty"`
	LastFailureAt                 *time.Time              `json:"last_failure_at,omitempty"`
	CreatedAt                     time.Time               `json:"created_at"`
	UpdatedAt                     time.Time               `json:"updated_at"`
}

type upstreamUsageWindowsDTO struct {
	FiveHour       *upstreamUsageWindowDTO `json:"five_hour,omitempty"`
	SevenDay       *upstreamUsageWindowDTO `json:"seven_day,omitempty"`
	SevenDaySonnet *upstreamUsageWindowDTO `json:"seven_day_sonnet,omitempty"`
}

type upstreamUsageWindowDTO struct {
	Utilization float64    `json:"utilization"`
	ResetsAt    *time.Time `json:"resets_at,omitempty"`
}

func (h *UpstreamAccountHandler) List(c *gin.Context) {
	accounts, err := h.service.List(c.Request.Context())
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	items := make([]upstreamAccountDTO, 0, len(accounts))
	for i := range accounts {
		items = append(items, upstreamAccountFromService(&accounts[i]))
	}
	response.Success(c, items)
}

func (h *UpstreamAccountHandler) Get(c *gin.Context) {
	account, err := h.service.Get(c.Request.Context(), c.Param("id"))
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, upstreamAccountFromService(account))
}

func (h *UpstreamAccountHandler) Create(c *gin.Context) {
	req := defaultUpstreamAccountRequest()
	if err := c.ShouldBindJSON(&req); err != nil {
		response.BadRequest(c, "请求格式无效")
		return
	}
	account, err := h.service.Create(c.Request.Context(), req.toService())
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	c.JSON(http.StatusCreated, response.Response{Code: 0, Message: "success", Data: upstreamAccountFromService(account)})
}

func (h *UpstreamAccountHandler) Update(c *gin.Context) {
	req := defaultUpstreamAccountRequest()
	if err := c.ShouldBindJSON(&req); err != nil {
		response.BadRequest(c, "请求格式无效")
		return
	}
	account, err := h.service.Update(c.Request.Context(), c.Param("id"), req.toService())
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, upstreamAccountFromService(account))
}

func (h *UpstreamAccountHandler) SetScheduling(c *gin.Context) {
	var req struct {
		IsActive *bool `json:"is_active"`
	}
	if err := c.ShouldBindJSON(&req); err != nil || req.IsActive == nil {
		response.BadRequest(c, "必须提供 is_active")
		return
	}
	account, err := h.service.SetActive(c.Request.Context(), c.Param("id"), *req.IsActive)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, upstreamAccountFromService(account))
}

func (h *UpstreamAccountHandler) Delete(c *gin.Context) {
	if err := h.service.Delete(c.Request.Context(), c.Param("id")); err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, gin.H{"deleted": true})
}

func (h *UpstreamAccountHandler) TestDraft(c *gin.Context) {
	req := defaultUpstreamAccountRequest()
	if err := c.ShouldBindJSON(&req); err != nil {
		response.BadRequest(c, "请求格式无效")
		return
	}
	result, err := h.service.TestDraft(c.Request.Context(), req.toService())
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, result)
}

func (h *UpstreamAccountHandler) TestSaved(c *gin.Context) {
	result, err := h.service.TestSaved(c.Request.Context(), c.Param("id"))
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, result)
}

func defaultUpstreamAccountRequest() upstreamAccountRequest {
	return upstreamAccountRequest{
		ProviderType:                  service.UpstreamProviderOpenAI,
		BaseURL:                       "https://api.openai.com",
		AuthType:                      service.UpstreamAuthAPIKey,
		WIFClientAuthMethod:           string(service.WIFClientSecretBasic),
		IsActive:                      true,
		Priority:                      100,
		Weight:                        100,
		MaxConcurrency:                8,
		RPMLimit:                      120,
		CircuitBreakerThreshold:       3,
		CircuitBreakerCooldownSeconds: 60,
	}
}

func (r upstreamAccountRequest) toService() service.UpstreamAccountInput {
	return service.UpstreamAccountInput{
		Name: strings.TrimSpace(r.Name), ProviderType: r.ProviderType, BaseURL: r.BaseURL, AuthType: r.AuthType,
		APIKey: r.APIKey, WIFClientSecret: r.WIFClientSecret,
		WIFSubjectTokenURL: r.WIFSubjectTokenURL, WIFClientID: r.WIFClientID,
		WIFClientAuthMethod: r.WIFClientAuthMethod, WIFAudience: r.WIFAudience, WIFScope: r.WIFScope,
		WIFIdentityProviderID: r.WIFIdentityProviderID, WIFServiceAccountID: r.WIFServiceAccountID,
		WIFFederationRuleID: r.WIFFederationRuleID, WIFOrganizationID: r.WIFOrganizationID,
		WIFWorkspaceID: r.WIFWorkspaceID, IsActive: r.IsActive, Priority: r.Priority, Weight: r.Weight,
		MaxConcurrency: r.MaxConcurrency, RPMLimit: r.RPMLimit,
		CircuitBreakerThreshold:       r.CircuitBreakerThreshold,
		CircuitBreakerCooldownSeconds: r.CircuitBreakerCooldownSeconds,
	}
}

func upstreamAccountFromService(account *service.UpstreamAccount) upstreamAccountDTO {
	dto := upstreamAccountDTO{
		ID: account.ID, Name: account.Name, ProviderType: account.ProviderType, BaseURL: account.BaseURL,
		AuthType: account.AuthType, MaskedCredential: "********" + account.CredentialHint,
		OAuthProfile: account.OAuthProfile, OAuthAccountID: account.OAuthAccountID, OAuthEmail: account.OAuthEmail,
		OAuthExpiresAt: account.OAuthExpiresAt, WIFSubjectTokenURL: account.WIFSubjectTokenURL,
		WIFClientID: account.WIFClientID, WIFClientAuthMethod: account.WIFClientAuthMethod,
		WIFAudience: account.WIFAudience, WIFScope: account.WIFScope,
		WIFIdentityProviderID: account.WIFIdentityProviderID, WIFServiceAccountID: account.WIFServiceAccountID,
		WIFFederationRuleID: account.WIFFederationRuleID, WIFOrganizationID: account.WIFOrganizationID,
		WIFWorkspaceID: account.WIFWorkspaceID, IsActive: account.IsActive,
		Priority: account.Priority, Weight: account.Weight, MaxConcurrency: account.MaxConcurrency,
		RPMLimit: account.RPMLimit, CircuitBreakerThreshold: account.CircuitBreakerThreshold,
		CircuitBreakerCooldownSeconds: account.CircuitBreakerCooldownSeconds,
		QuotaStatus:                   account.QuotaStatus, QuotaUtilization: account.QuotaUtilization,
		QuotaResetsAt: account.QuotaResetsAt, QuotaCheckedAt: account.QuotaCheckedAt,
		CooldownUntil: account.CooldownUntil, CooldownReason: account.CooldownReason,
		LastUpstreamStatus: account.LastUpstreamStatus, LastSuccessAt: account.LastSuccessAt,
		LastFailureAt: account.LastFailureAt, CreatedAt: account.CreatedAt, UpdatedAt: account.UpdatedAt,
	}
	dto.UsageWindows.FiveHour = upstreamUsageWindow(account.QuotaFiveHourUtilization, account.QuotaFiveHourResetsAt)
	dto.UsageWindows.SevenDay = upstreamUsageWindow(account.QuotaSevenDayUtilization, account.QuotaSevenDayResetsAt)
	dto.UsageWindows.SevenDaySonnet = upstreamUsageWindow(account.QuotaSevenDaySonnetUtilization, account.QuotaSevenDaySonnetResetsAt)
	return dto
}

func upstreamUsageWindow(utilization *float64, resetsAt *time.Time) *upstreamUsageWindowDTO {
	if utilization == nil {
		return nil
	}
	return &upstreamUsageWindowDTO{Utilization: *utilization, ResetsAt: resetsAt}
}
