package admin

import (
	"strconv"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/pkg/response"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
)

type WorkDistributionHandler struct {
	service *service.WorkDistributionService
}

func NewWorkDistributionHandler(svc *service.WorkDistributionService) *WorkDistributionHandler {
	return &WorkDistributionHandler{service: svc}
}

// Summary returns work-content aggregates for administrators.
func (h *WorkDistributionHandler) Summary(c *gin.Context) {
	startTime, endTime := parseTimeRange(c)
	userID, ok := parseOptionalPositiveInt64(c, "user_id")
	if !ok {
		return
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
		WorkDistributionFilter: service.WorkDistributionFilter{StartTime: startTime, EndTime: endTime, UserID: userID, Department: strings.TrimSpace(c.Query("department")), Role: strings.TrimSpace(c.Query("role"))},
		Metric:                 strings.TrimSpace(c.Query("metric")), UserLimit: userLimit,
	})
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, result)
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
