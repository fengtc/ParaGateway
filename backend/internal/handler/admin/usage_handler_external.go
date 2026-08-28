package admin

import (
	"fmt"
	"strconv"
	"strings"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/pkg/pagination"
	"github.com/Wei-Shaw/sub2api/internal/pkg/response"
	"github.com/Wei-Shaw/sub2api/internal/pkg/timezone"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
)

type ExternalUsageLogsResponse struct {
	Items      []service.ExternalUsageLog        `json:"items"`
	Totals     *service.ExternalUsageLogTotals   `json:"totals"`
	Pagination ExternalUsageLogsPaginationResult `json:"pagination"`
}

type ExternalUsageLogsPaginationResult struct {
	Total    int64 `json:"total"`
	Page     int   `json:"page"`
	PageSize int   `json:"page_size"`
	Pages    int   `json:"pages"`
}

// ExternalLogs lists denormalized usage records using the legacy external API contract.
// GET /api/v1/admin/usage/external-logs
func (h *UsageHandler) ExternalLogs(c *gin.Context) {
	if h.usageService == nil {
		response.InternalError(c, "usage service is not available")
		return
	}

	page, pageSize := response.ParsePagination(c)
	if pageSize > 500 {
		pageSize = 500
	}

	startTime, err := parseExternalUsageTime(c.Query("start_time"), c.Query("start_date"), c.Query("timezone"), false)
	if err != nil {
		response.BadRequest(c, err.Error())
		return
	}
	endTime, err := parseExternalUsageTime(c.Query("end_time"), c.Query("end_date"), c.Query("timezone"), true)
	if err != nil {
		response.BadRequest(c, err.Error())
		return
	}
	if startTime == nil || endTime == nil {
		response.BadRequest(c, "start_time and end_time are required")
		return
	}
	if !startTime.Before(*endTime) {
		response.BadRequest(c, "start_time must be before end_time")
		return
	}
	if endTime.Sub(*startTime) > 366*24*time.Hour {
		response.BadRequest(c, "time range must be 366 days or less")
		return
	}

	filters := service.ExternalUsageLogFilters{
		StartTime: startTime,
		EndTime:   endTime,
		Model:     strings.TrimSpace(c.Query("model")),
	}
	if filters.UserID, err = parseExternalPositiveID(c, "user_id"); err != nil {
		response.BadRequest(c, err.Error())
		return
	}
	if filters.APIKeyID, err = parseExternalPositiveID(c, "api_key_id"); err != nil {
		response.BadRequest(c, err.Error())
		return
	}
	if filters.AccountID, err = parseExternalPositiveID(c, "account_id"); err != nil {
		response.BadRequest(c, err.Error())
		return
	}
	if filters.GroupID, err = parseExternalPositiveID(c, "group_id"); err != nil {
		response.BadRequest(c, err.Error())
		return
	}

	params := pagination.PaginationParams{
		Page:      page,
		PageSize:  pageSize,
		SortBy:    "created_at",
		SortOrder: c.DefaultQuery("sort_order", "desc"),
	}
	items, pageResult, totals, err := h.usageService.ListExternalUsageLogs(c.Request.Context(), params, filters)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}

	response.Success(c, ExternalUsageLogsResponse{
		Items:  items,
		Totals: totals,
		Pagination: ExternalUsageLogsPaginationResult{
			Total:    pageResult.Total,
			Page:     pageResult.Page,
			PageSize: pageResult.PageSize,
			Pages:    pageResult.Pages,
		},
	})
}

func parseExternalUsageTime(rawTime, rawDate, userTZ string, endOfDate bool) (*time.Time, error) {
	rawTime = strings.TrimSpace(rawTime)
	if rawTime != "" {
		if parsed, err := time.Parse(time.RFC3339, rawTime); err == nil {
			return &parsed, nil
		}
		if parsed, err := time.Parse("2006-01-02 15:04:05", rawTime); err == nil {
			return &parsed, nil
		}
		return nil, fmt.Errorf("invalid time format, use RFC3339 or YYYY-MM-DD HH:MM:SS")
	}

	rawDate = strings.TrimSpace(rawDate)
	if rawDate == "" {
		return nil, nil
	}
	parsed, err := timezone.ParseInUserLocation("2006-01-02", rawDate, userTZ)
	if err != nil {
		return nil, err
	}
	if endOfDate {
		parsed = parsed.AddDate(0, 0, 1)
	}
	return &parsed, nil
}

func parseExternalPositiveID(c *gin.Context, name string) (int64, error) {
	raw := strings.TrimSpace(c.Query(name))
	if raw == "" {
		return 0, nil
	}
	value, err := strconv.ParseInt(raw, 10, 64)
	if err != nil || value <= 0 {
		return 0, fmt.Errorf("Invalid %s", name)
	}
	return value, nil
}
