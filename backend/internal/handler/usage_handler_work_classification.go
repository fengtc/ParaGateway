package handler

import (
	"strconv"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/pkg/response"
	middleware2 "github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
)

// ListOwnWorkClassifications returns only the authenticated user's structured
// work metadata and classifications. It never returns request content.
// GET /api/v1/usage/work-classifications
func (h *UsageHandler) ListOwnWorkClassifications(c *gin.Context) {
	subject, ok := middleware2.GetAuthSubjectFromContext(c)
	if !ok || subject.UserID <= 0 {
		response.Unauthorized(c, "User not authenticated")
		return
	}
	if h.workDistributionService == nil {
		response.InternalError(c, "Work classification service unavailable")
		return
	}
	page, pageSize := response.ParsePagination(c)
	items, total, err := h.workDistributionService.ListOwnClassifications(c.Request.Context(), subject.UserID, page, pageSize)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Paginated(c, items, total, page, pageSize)
}

type workClassificationAppealRequest struct {
	WorkRelated string `json:"work_related"`
	Category    string `json:"category"`
	ReasonCode  string `json:"reason_code"`
}

// CreateWorkClassificationAppeal creates one pending structured appeal for an
// owned usage record. The reason is an allowlisted code, not free-form text.
// POST /api/v1/usage/work-classifications/:usage_log_id/appeals
func (h *UsageHandler) CreateWorkClassificationAppeal(c *gin.Context) {
	subject, ok := middleware2.GetAuthSubjectFromContext(c)
	if !ok || subject.UserID <= 0 {
		response.Unauthorized(c, "User not authenticated")
		return
	}
	if h.workDistributionService == nil {
		response.InternalError(c, "Work classification service unavailable")
		return
	}
	usageLogID, err := strconv.ParseInt(strings.TrimSpace(c.Param("usage_log_id")), 10, 64)
	if err != nil || usageLogID <= 0 {
		response.BadRequest(c, "Invalid usage_log_id")
		return
	}
	var request workClassificationAppealRequest
	if err := c.ShouldBindJSON(&request); err != nil {
		response.BadRequest(c, "Invalid request body")
		return
	}
	item, err := h.workDistributionService.CreateAppeal(c.Request.Context(), subject.UserID, service.CreateWorkReviewInput{
		UsageLogID: usageLogID, WorkRelated: request.WorkRelated, Category: request.Category, ReasonCode: request.ReasonCode,
	})
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, item)
}
