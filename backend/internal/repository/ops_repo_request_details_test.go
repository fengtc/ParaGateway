package repository

import (
	"context"
	"testing"
	"time"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestListRequestDetails_SortsAndReturnsFirstTokenLatency(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &opsRepository{db: db}
	start := time.Date(2026, 8, 31, 0, 0, 0, 0, time.UTC)
	end := start.Add(time.Hour)

	mock.ExpectQuery(`SELECT COUNT\(1\) FROM combined`).
		WithArgs(start, end).
		WillReturnRows(sqlmock.NewRows([]string{"count"}).AddRow(int64(1)))
	mock.ExpectQuery(`ORDER BY first_token_ms DESC NULLS LAST, created_at DESC\s+LIMIT \$3 OFFSET \$4`).
		WithArgs(start, end, 20, 0).
		WillReturnRows(sqlmock.NewRows([]string{
			"kind", "created_at", "request_id", "platform", "model", "duration_ms", "first_token_ms",
			"status_code", "error_id", "phase", "severity", "message", "user_id", "api_key_id",
			"account_id", "group_id", "stream",
		}).AddRow(
			"success", start, "req-1", "openai", "gpt-5.6-sol", int64(3200), int64(1450),
			nil, nil, nil, nil, nil, int64(3), int64(4), int64(5), int64(6), true,
		))

	items, total, err := repo.ListRequestDetails(context.Background(), &service.OpsRequestDetailFilter{
		StartTime: &start,
		EndTime:   &end,
		Page:      1,
		PageSize:  20,
		Sort:      "first_token_desc",
	})

	require.NoError(t, err)
	require.Equal(t, int64(1), total)
	require.Len(t, items, 1)
	require.NotNil(t, items[0].FirstTokenMs)
	require.Equal(t, 1450, *items[0].FirstTokenMs)
	require.NoError(t, mock.ExpectationsWereMet())
}
