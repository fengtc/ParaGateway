package admin

import (
	"strconv"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/pkg/response"
	"github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
)

type WorkDistributionHandler struct {
	service *service.WorkDistributionService
}

func NewWorkDistributionHandler(svc *service.WorkDistributionService) *WorkDistributionHandler {
	return &WorkDistributionHandler{service: svc}
}

// Summary returns privacy-protected work classification aggregates.
// GET /api/v1/admin/work-distribution/summary
func (h *WorkDistributionHandler) Summary(c *gin.Context) {
	startTime, endTime := parseTimeRange(c)
	userID, ok := parseOptionalPositiveInt64(c, "user_id")
	if !ok {
		return
	}
	minSampleSize := int64(5)
	if raw := strings.TrimSpace(c.Query("min_sample_size")); raw != "" {
		value, err := strconv.ParseInt(raw, 10, 64)
		if err != nil || value <= 0 {
			response.BadRequest(c, "Invalid min_sample_size")
			return
		}
		minSampleSize = value
	}
	minCohortSize := int64(5)
	if raw := strings.TrimSpace(c.Query("min_cohort_size")); raw != "" {
		value, err := strconv.ParseInt(raw, 10, 64)
		if err != nil || value <= 0 {
			response.BadRequest(c, "Invalid min_cohort_size")
			return
		}
		minCohortSize = value
	}
	userLimit := 100
	if raw := strings.TrimSpace(c.Query("user_limit")); raw != "" {
		value, err := strconv.Atoi(raw)
		if err != nil || value <= 0 {
			response.BadRequest(c, "Invalid user_limit")
			return
		}
		if value > 500 {
			value = 500
		}
		userLimit = value
	}
	result, err := h.service.GetSummary(c.Request.Context(), service.WorkDistributionSummaryFilter{
		WorkDistributionFilter: service.WorkDistributionFilter{
			StartTime: startTime, EndTime: endTime, UserID: userID,
			Department: strings.TrimSpace(c.Query("department")), Role: strings.TrimSpace(c.Query("role")),
		},
		Metric: strings.TrimSpace(c.Query("metric")), MinSampleSize: minSampleSize, MinCohortSize: minCohortSize, UserLimit: userLimit,
	})
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, result)
}

// ListRecords returns only structured usage/classification fields for users
// meeting the server-side minimum sample threshold.
// GET /api/v1/admin/work-distribution/records
func (h *WorkDistributionHandler) ListRecords(c *gin.Context) {
	startTime, endTime := parseTimeRange(c)
	userID, ok := parseOptionalPositiveInt64(c, "user_id")
	if !ok {
		return
	}
	page, pageSize := response.ParsePagination(c)
	minSampleSize := int64(5)
	if raw := strings.TrimSpace(c.Query("min_sample_size")); raw != "" {
		value, err := strconv.ParseInt(raw, 10, 64)
		if err != nil || value <= 0 {
			response.BadRequest(c, "Invalid min_sample_size")
			return
		}
		minSampleSize = value
	}
	minCohortSize := int64(5)
	if raw := strings.TrimSpace(c.Query("min_cohort_size")); raw != "" {
		value, err := strconv.ParseInt(raw, 10, 64)
		if err != nil || value <= 0 {
			response.BadRequest(c, "Invalid min_cohort_size")
			return
		}
		minCohortSize = value
	}
	items, total, err := h.service.ListRecords(c.Request.Context(), service.WorkDistributionRecordFilter{
		WorkDistributionFilter: service.WorkDistributionFilter{
			StartTime: startTime, EndTime: endTime, UserID: userID,
			Department: strings.TrimSpace(c.Query("department")), Role: strings.TrimSpace(c.Query("role")),
		},
		Category: strings.TrimSpace(c.Query("category")), WorkRelated: strings.TrimSpace(c.Query("work_related")),
		ReviewStatus: strings.TrimSpace(c.Query("review_status")), MinSampleSize: minSampleSize, MinCohortSize: minCohortSize,
		Page: page, PageSize: pageSize,
	})
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Paginated(c, items, total, page, pageSize)
}

type workCorrectionRequest struct {
	WorkRelated string `json:"work_related"`
	Category    string `json:"category"`
	ReasonCode  string `json:"reason_code"`
}

// CreateCorrection creates a pending structured review; it never stores request content.
// POST /api/v1/admin/work-distribution/records/:usage_log_id/correction
func (h *WorkDistributionHandler) CreateCorrection(c *gin.Context) {
	usageLogID, err := strconv.ParseInt(strings.TrimSpace(c.Param("usage_log_id")), 10, 64)
	if err != nil || usageLogID <= 0 {
		response.BadRequest(c, "Invalid usage_log_id")
		return
	}
	subject, ok := middleware.GetAuthSubjectFromContext(c)
	if !ok || subject.UserID <= 0 {
		response.Unauthorized(c, "Unauthorized")
		return
	}
	var request workCorrectionRequest
	if err := c.ShouldBindJSON(&request); err != nil {
		response.BadRequest(c, "Invalid request body")
		return
	}
	item, err := h.service.CreateCorrection(c.Request.Context(), service.CreateWorkReviewInput{
		UsageLogID: usageLogID, WorkRelated: request.WorkRelated, Category: request.Category,
		ReasonCode: request.ReasonCode, RequestedBy: subject.UserID,
	})
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, item)
}

// ListReviews lists pending and resolved structured reviews.
// GET /api/v1/admin/work-distribution/reviews
func (h *WorkDistributionHandler) ListReviews(c *gin.Context) {
	page, pageSize := response.ParsePagination(c)
	userID, ok := parseOptionalPositiveInt64(c, "user_id")
	if !ok {
		return
	}
	items, total, err := h.service.ListReviews(c.Request.Context(), service.WorkDistributionReviewFilter{
		Status: strings.TrimSpace(c.Query("status")), UserID: userID, Page: page, PageSize: pageSize,
	})
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Paginated(c, items, total, page, pageSize)
}

type workReviewResolutionRequest struct {
	Decision       string `json:"decision"`
	ResolutionNote string `json:"resolution_note"`
}

// ResolveReview approves or rejects a pending review.
// POST /api/v1/admin/work-distribution/reviews/:review_id/resolve
func (h *WorkDistributionHandler) ResolveReview(c *gin.Context) {
	reviewID, err := strconv.ParseInt(strings.TrimSpace(c.Param("review_id")), 10, 64)
	if err != nil || reviewID <= 0 {
		response.BadRequest(c, "Invalid review_id")
		return
	}
	subject, ok := middleware.GetAuthSubjectFromContext(c)
	if !ok || subject.UserID <= 0 {
		response.Unauthorized(c, "Unauthorized")
		return
	}
	var request workReviewResolutionRequest
	if err := c.ShouldBindJSON(&request); err != nil {
		response.BadRequest(c, "Invalid request body")
		return
	}
	item, err := h.service.ResolveReview(c.Request.Context(), service.ResolveWorkReviewInput{
		ReviewID: reviewID, Decision: request.Decision, ResolutionNote: request.ResolutionNote, ResolvedBy: subject.UserID,
	})
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, item)
}

func parseOptionalPositiveInt64(c *gin.Context, name string) (int64, bool) {
	raw := strings.TrimSpace(c.Query(name))
	if raw == "" {
		return 0, true
	}
	value, err := strconv.ParseInt(raw, 10, 64)
	if err != nil || value <= 0 {
		response.BadRequest(c, "Invalid "+name)
		return 0, false
	}
	return value, true
}
