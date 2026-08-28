package admin

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"net/url"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/pkg/pagination"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

type externalUsageRepoCapture struct {
	service.UsageLogRepository
	params  pagination.PaginationParams
	filters service.ExternalUsageLogFilters
	items   []service.ExternalUsageLog
	page    *pagination.PaginationResult
	totals  *service.ExternalUsageLogTotals
}

func (r *externalUsageRepoCapture) ListExternalUsageLogs(_ context.Context, params pagination.PaginationParams, filters service.ExternalUsageLogFilters) ([]service.ExternalUsageLog, *pagination.PaginationResult, *service.ExternalUsageLogTotals, error) {
	r.params = params
	r.filters = filters
	return r.items, r.page, r.totals, nil
}

func newExternalUsageTestRouter(repo *externalUsageRepoCapture) *gin.Engine {
	gin.SetMode(gin.TestMode)
	usageService := service.NewUsageService(repo, nil, nil, nil)
	handler := NewUsageHandler(usageService, nil, nil, nil)
	router := gin.New()
	router.GET("/api/v1/admin/usage/external-logs", handler.ExternalLogs)
	return router
}

func TestExternalUsageLogsPreservesLegacyRequestAndResponseContract(t *testing.T) {
	groupID := int64(44)
	groupName := "Engineering"
	upstreamModel := "gpt-5.2"
	mappingChain := "alias->gpt-5.2"
	durationMs := 321
	firstTokenMs := 87
	inboundEndpoint := "/v1/chat/completions"
	upstreamEndpoint := "/v1/responses"
	createdAt := time.Date(2026, 8, 28, 10, 20, 30, 0, time.UTC)

	repo := &externalUsageRepoCapture{
		items: []service.ExternalUsageLog{{
			ID: 99, CreatedAt: createdAt,
			UserID: 1, Email: "user@example.com", Username: "user",
			APIKeyID: 2, APIKeyName: "external",
			AccountID: 3, AccountName: "account", Platform: "openai",
			GroupID: &groupID, GroupName: &groupName,
			RequestID: "req-1", Model: "alias", RequestedModel: "alias",
			UpstreamModel: &upstreamModel, ModelMappingChain: &mappingChain,
			InputTokens: 10, OutputTokens: 20, CacheCreationTokens: 3,
			CacheReadTokens: 4, ImageOutputTokens: 5, TotalTokens: 42,
			InputCost: 0.1, OutputCost: 0.2, CacheCreationCost: 0.03,
			CacheReadCost: 0.04, ImageOutputCost: 0.05, TotalCost: 0.42, ActualCost: 0.84,
			RequestType: "stream", Stream: true, DurationMs: &durationMs,
			FirstTokenMs: &firstTokenMs, InboundEndpoint: &inboundEndpoint, UpstreamEndpoint: &upstreamEndpoint,
		}},
		page: &pagination.PaginationResult{Total: 501, Page: 2, PageSize: 500, Pages: 2},
		totals: &service.ExternalUsageLogTotals{
			Requests: 501, InputTokens: 1000, OutputTokens: 2000,
			CacheCreationTokens: 300, CacheReadTokens: 400, ImageOutputTokens: 500,
			TotalTokens: 4200, TotalCost: 42, ActualCost: 84,
		},
	}
	router := newExternalUsageTestRouter(repo)

	query := url.Values{
		"start_time": {"2026-08-28T00:00:00+08:00"},
		"end_time":   {"2026-08-29T00:00:00+08:00"},
		"user_id":    {"1"}, "api_key_id": {"2"}, "account_id": {"3"}, "group_id": {"44"},
		"model": {" alias "}, "page": {"2"}, "page_size": {"800"}, "sort_order": {"asc"},
	}
	recorder := httptest.NewRecorder()
	router.ServeHTTP(recorder, httptest.NewRequest(http.MethodGet, "/api/v1/admin/usage/external-logs?"+query.Encode(), nil))

	require.Equal(t, http.StatusOK, recorder.Code)
	require.Equal(t, 2, repo.params.Page)
	require.Equal(t, 500, repo.params.PageSize)
	require.Equal(t, "created_at", repo.params.SortBy)
	require.Equal(t, "asc", repo.params.SortOrder)
	require.Equal(t, int64(1), repo.filters.UserID)
	require.Equal(t, int64(2), repo.filters.APIKeyID)
	require.Equal(t, int64(3), repo.filters.AccountID)
	require.Equal(t, int64(44), repo.filters.GroupID)
	require.Equal(t, "alias", repo.filters.Model)
	require.Equal(t, time.Date(2026, 8, 27, 16, 0, 0, 0, time.UTC), repo.filters.StartTime.UTC())
	require.Equal(t, time.Date(2026, 8, 28, 16, 0, 0, 0, time.UTC), repo.filters.EndTime.UTC())

	var envelope map[string]any
	require.NoError(t, json.Unmarshal(recorder.Body.Bytes(), &envelope))
	require.ElementsMatch(t, []string{"code", "message", "data"}, mapKeys(envelope))
	require.Equal(t, float64(0), envelope["code"])
	require.Equal(t, "success", envelope["message"])

	data := envelope["data"].(map[string]any)
	require.ElementsMatch(t, []string{"items", "totals", "pagination"}, mapKeys(data))
	items := data["items"].([]any)
	require.Len(t, items, 1)
	require.ElementsMatch(t, []string{
		"id", "created_at", "user_id", "email", "username", "api_key_id", "api_key_name",
		"account_id", "account_name", "platform", "group_id", "group_name", "request_id", "model",
		"requested_model", "upstream_model", "model_mapping_chain", "input_tokens", "output_tokens",
		"cache_creation_tokens", "cache_read_tokens", "image_output_tokens", "total_tokens", "input_cost",
		"output_cost", "cache_creation_cost", "cache_read_cost", "image_output_cost", "total_cost", "actual_cost",
		"request_type", "stream", "duration_ms", "first_token_ms", "inbound_endpoint", "upstream_endpoint",
	}, mapKeys(items[0].(map[string]any)))
	require.ElementsMatch(t, []string{
		"requests", "input_tokens", "output_tokens", "cache_creation_tokens", "cache_read_tokens",
		"image_output_tokens", "total_tokens", "total_cost", "actual_cost",
	}, mapKeys(data["totals"].(map[string]any)))
	require.ElementsMatch(t, []string{"total", "page", "page_size", "pages"}, mapKeys(data["pagination"].(map[string]any)))
}

func TestExternalUsageLogsSupportsLegacyDateRangeAndEmptyPage(t *testing.T) {
	repo := &externalUsageRepoCapture{
		items:  []service.ExternalUsageLog{},
		page:   &pagination.PaginationResult{Total: 0, Page: 1, PageSize: 20, Pages: 1},
		totals: &service.ExternalUsageLogTotals{},
	}
	router := newExternalUsageTestRouter(repo)
	recorder := httptest.NewRecorder()
	router.ServeHTTP(recorder, httptest.NewRequest(http.MethodGet, "/api/v1/admin/usage/external-logs?start_date=2026-08-28&end_date=2026-08-28&timezone=Asia%2FShanghai", nil))

	require.Equal(t, http.StatusOK, recorder.Code)
	require.Equal(t, time.Date(2026, 8, 27, 16, 0, 0, 0, time.UTC), repo.filters.StartTime.UTC())
	require.Equal(t, time.Date(2026, 8, 28, 16, 0, 0, 0, time.UTC), repo.filters.EndTime.UTC())
	var envelope map[string]any
	require.NoError(t, json.Unmarshal(recorder.Body.Bytes(), &envelope))
	data := envelope["data"].(map[string]any)
	require.Empty(t, data["items"].([]any))
	require.Equal(t, float64(1), data["pagination"].(map[string]any)["pages"])
}

func TestParseExternalUsageTimePreservesLegacyFormatsAndPrecedence(t *testing.T) {
	parsed, err := parseExternalUsageTime("2026-08-28 12:34:56", "bad-date", "Asia/Shanghai", false)
	require.NoError(t, err)
	require.Equal(t, time.Date(2026, 8, 28, 12, 34, 56, 0, time.UTC), *parsed)

	parsed, err = parseExternalUsageTime("2026-08-28T12:34:56+08:00", "", "", false)
	require.NoError(t, err)
	require.Equal(t, time.Date(2026, 8, 28, 4, 34, 56, 0, time.UTC), parsed.UTC())
}

func TestExternalUsageLogsRejectsInvalidLegacyParameters(t *testing.T) {
	testCases := []struct {
		name  string
		query string
	}{
		{name: "missing end", query: "start_time=2026-08-28T00%3A00%3A00Z"},
		{name: "invalid time", query: "start_time=bad&end_time=2026-08-29T00%3A00%3A00Z"},
		{name: "empty range", query: "start_time=2026-08-28T00%3A00%3A00Z&end_time=2026-08-28T00%3A00%3A00Z"},
		{name: "over 366 days", query: "start_time=2025-08-27T00%3A00%3A00Z&end_time=2026-08-29T00%3A00%3A00Z"},
		{name: "non-positive id", query: "start_time=2026-08-28T00%3A00%3A00Z&end_time=2026-08-29T00%3A00%3A00Z&user_id=0"},
		{name: "invalid id", query: "start_time=2026-08-28T00%3A00%3A00Z&end_time=2026-08-29T00%3A00%3A00Z&account_id=nope"},
	}
	for _, testCase := range testCases {
		t.Run(testCase.name, func(t *testing.T) {
			repo := &externalUsageRepoCapture{}
			recorder := httptest.NewRecorder()
			newExternalUsageTestRouter(repo).ServeHTTP(recorder, httptest.NewRequest(http.MethodGet, "/api/v1/admin/usage/external-logs?"+testCase.query, nil))
			require.Equal(t, http.StatusBadRequest, recorder.Code)
		})
	}
}

func mapKeys(values map[string]any) []string {
	keys := make([]string, 0, len(values))
	for key := range values {
		keys = append(keys, key)
	}
	return keys
}
