package handler

import (
	"net/http"

	middleware2 "github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
)

// CopilotModels restores the legacy /copilot/v1/models behavior: choose only a
// canonical GitHub Copilot account and return its live root /models response.
func (h *OpenAIGatewayHandler) CopilotModels(c *gin.Context) {
	apiKey, ok := middleware2.GetAPIKeyFromContext(c)
	if !ok || apiKey == nil {
		h.errorResponse(c, http.StatusUnauthorized, "authentication_error", "Invalid API key")
		return
	}
	if h == nil || h.gatewayService == nil {
		h.errorResponse(c, http.StatusServiceUnavailable, "api_error", "GitHub Copilot gateway is not configured")
		return
	}

	ctx := service.WithGitHubCopilotOnly(c.Request.Context())
	c.Request = c.Request.WithContext(ctx)
	account, err := h.gatewayService.SelectAccountForModelWithExclusions(ctx, apiKey.GroupID, "", "", nil)
	if err != nil || account == nil {
		h.errorResponse(c, http.StatusServiceUnavailable, "api_error", "No available GitHub Copilot accounts")
		return
	}
	setOpsSelectedAccount(c, account.ID, account.Platform)

	body, err := h.gatewayService.FetchGitHubCopilotModels(ctx, account)
	if err != nil {
		h.errorResponse(c, http.StatusBadGateway, "upstream_error", "Failed to list GitHub Copilot models")
		return
	}
	c.Data(http.StatusOK, "application/json", body)
}
