package repository

import (
	"context"
	"testing"
	"time"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/Wei-Shaw/sub2api/internal/pkg/pagination"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestUsageLogRepositoryListExternalUsageLogsFiltersTotalsAndMapping(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &usageLogRepository{sql: db}
	start := time.Date(2026, 8, 28, 0, 0, 0, 0, time.UTC)
	end := start.Add(24 * time.Hour)
	filterArgs := []any{int64(1), int64(2), int64(3), int64(4), "alias", start, end}

	wherePattern := "WHERE ul.user_id = \\$1 AND ul.api_key_id = \\$2 AND ul.account_id = \\$3 AND ul.group_id = \\$4 AND \\(ul.model = \\$5 OR ul.requested_model = \\$5 OR ul.upstream_model = \\$5\\) AND ul.created_at >= \\$6 AND ul.created_at < \\$7"
	mock.ExpectQuery("SELECT COUNT\\(\\*\\) FROM usage_logs ul " + wherePattern).
		WithArgs(anySliceToDriverValues(filterArgs)...).
		WillReturnRows(sqlmock.NewRows([]string{"count"}).AddRow(int64(26)))
	mock.ExpectQuery("SELECT COUNT\\(\\*\\) AS requests, .* FROM usage_logs ul " + wherePattern).
		WithArgs(anySliceToDriverValues(filterArgs)...).
		WillReturnRows(sqlmock.NewRows([]string{
			"requests", "input_tokens", "output_tokens", "cache_creation_tokens", "cache_read_tokens",
			"image_output_tokens", "total_tokens", "total_cost", "actual_cost",
		}).AddRow(int64(26), int64(100), int64(200), int64(30), int64(40), int64(50), int64(420), 4.2, 8.4))

	createdAt := start.Add(time.Hour)
	mock.ExpectQuery("SELECT ul.id, .* LEFT JOIN groups g ON g.id = ul.group_id " + wherePattern + " ORDER BY ul.created_at ASC, ul.id ASC LIMIT \\$8 OFFSET \\$9").
		WithArgs(anySliceToDriverValues(append(filterArgs, 25, 25))...).
		WillReturnRows(sqlmock.NewRows([]string{
			"id", "created_at", "user_id", "email", "username", "api_key_id", "api_key_name",
			"account_id", "account_name", "platform", "group_id", "group_name", "request_id", "model",
			"requested_model", "upstream_model", "model_mapping_chain", "input_tokens", "output_tokens",
			"cache_creation_tokens", "cache_read_tokens", "image_output_tokens", "total_tokens", "input_cost",
			"output_cost", "cache_creation_cost", "cache_read_cost", "image_output_cost", "total_cost", "actual_cost",
			"request_type", "stream", "duration_ms", "first_token_ms", "inbound_endpoint", "upstream_endpoint",
		}).AddRow(
			int64(99), createdAt, int64(1), "user@example.com", "user", int64(2), "external",
			int64(3), "account", "openai", int64(4), "Engineering", "req-1", "alias",
			"alias", "gpt-5.2", "alias->gpt-5.2", 10, 20, 3, 4, 5, 42,
			0.1, 0.2, 0.03, 0.04, 0.05, 0.42, 0.84, int16(service.RequestTypeStream), true,
			321, 87, "/v1/chat/completions", "/v1/responses",
		))

	items, pageResult, totals, err := repo.ListExternalUsageLogs(context.Background(), pagination.PaginationParams{
		Page: 2, PageSize: 25, SortOrder: pagination.SortOrderAsc,
	}, service.ExternalUsageLogFilters{
		UserID: 1, APIKeyID: 2, AccountID: 3, GroupID: 4, Model: " alias ", StartTime: &start, EndTime: &end,
	})

	require.NoError(t, err)
	require.Equal(t, &pagination.PaginationResult{Total: 26, Page: 2, PageSize: 25, Pages: 2}, pageResult)
	require.Equal(t, &service.ExternalUsageLogTotals{
		Requests: 26, InputTokens: 100, OutputTokens: 200, CacheCreationTokens: 30,
		CacheReadTokens: 40, ImageOutputTokens: 50, TotalTokens: 420, TotalCost: 4.2, ActualCost: 8.4,
	}, totals)
	require.Len(t, items, 1)
	item := items[0]
	require.Equal(t, int64(99), item.ID)
	require.Equal(t, "stream", item.RequestType)
	require.Equal(t, int64(4), *item.GroupID)
	require.Equal(t, "Engineering", *item.GroupName)
	require.Equal(t, "gpt-5.2", *item.UpstreamModel)
	require.Equal(t, "alias->gpt-5.2", *item.ModelMappingChain)
	require.Equal(t, 321, *item.DurationMs)
	require.Equal(t, 87, *item.FirstTokenMs)
	require.Equal(t, "/v1/chat/completions", *item.InboundEndpoint)
	require.Equal(t, "/v1/responses", *item.UpstreamEndpoint)
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestUsageLogRepositoryListExternalUsageLogsEmptyResultKeepsOnePage(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &usageLogRepository{sql: db}

	mock.ExpectQuery("SELECT COUNT\\(\\*\\) FROM usage_logs ul").
		WillReturnRows(sqlmock.NewRows([]string{"count"}).AddRow(int64(0)))
	mock.ExpectQuery("SELECT COUNT\\(\\*\\) AS requests, .* FROM usage_logs ul").
		WillReturnRows(sqlmock.NewRows([]string{
			"requests", "input_tokens", "output_tokens", "cache_creation_tokens", "cache_read_tokens",
			"image_output_tokens", "total_tokens", "total_cost", "actual_cost",
		}).AddRow(int64(0), int64(0), int64(0), int64(0), int64(0), int64(0), int64(0), 0.0, 0.0))
	mock.ExpectQuery("SELECT ul.id, .* ORDER BY ul.created_at DESC, ul.id DESC LIMIT \\$1 OFFSET \\$2").
		WithArgs(20, 0).
		WillReturnRows(sqlmock.NewRows([]string{
			"id", "created_at", "user_id", "email", "username", "api_key_id", "api_key_name",
			"account_id", "account_name", "platform", "group_id", "group_name", "request_id", "model",
			"requested_model", "upstream_model", "model_mapping_chain", "input_tokens", "output_tokens",
			"cache_creation_tokens", "cache_read_tokens", "image_output_tokens", "total_tokens", "input_cost",
			"output_cost", "cache_creation_cost", "cache_read_cost", "image_output_cost", "total_cost", "actual_cost",
			"request_type", "stream", "duration_ms", "first_token_ms", "inbound_endpoint", "upstream_endpoint",
		}))

	items, pageResult, totals, err := repo.ListExternalUsageLogs(context.Background(), pagination.PaginationParams{}, service.ExternalUsageLogFilters{})
	require.NoError(t, err)
	require.Empty(t, items)
	require.Equal(t, &pagination.PaginationResult{Total: 0, Page: 1, PageSize: 20, Pages: 1}, pageResult)
	require.Equal(t, &service.ExternalUsageLogTotals{}, totals)
	require.NoError(t, mock.ExpectationsWereMet())
}
