package repository

import (
	"context"
	"testing"
	"time"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestListRequestDetailsUsesEffectiveRequestedModelForBothSources(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &opsRepository{db: db}
	start := time.Date(2026, 8, 31, 0, 0, 0, 0, time.UTC)
	end := start.Add(time.Hour)
	accountID := int64(34)
	filter := &service.OpsRequestDetailFilter{
		StartTime: &start,
		EndTime:   &end,
		AccountID: &accountID,
		Model:     "gpt-5.6-sol",
		Page:      1,
		PageSize:  20,
	}

	modelExpressions := `(?s)COALESCE\(NULLIF\(TRIM\(ul\.requested_model\), ''\), ul\.model\) AS model.*COALESCE\(NULLIF\(TRIM\(o\.requested_model\), ''\), o\.model, ''\) AS model`
	mock.ExpectQuery(modelExpressions+`.*SELECT COUNT\(1\) FROM combined WHERE account_id = \$3 AND model = \$4`).
		WithArgs(start, end, accountID, "gpt-5.6-sol").
		WillReturnRows(sqlmock.NewRows([]string{"count"}).AddRow(int64(0)))
	mock.ExpectQuery(modelExpressions+`.*FROM combined.*WHERE account_id = \$3 AND model = \$4.*LIMIT \$5 OFFSET \$6`).
		WithArgs(start, end, accountID, "gpt-5.6-sol", 20, 0).
		WillReturnRows(sqlmock.NewRows([]string{
			"kind", "created_at", "request_id", "platform", "model", "duration_ms",
			"first_token_ms", "status_code", "error_id", "phase", "severity", "message",
			"user_id", "api_key_id", "account_id", "group_id", "stream",
		}))

	items, total, err := repo.ListRequestDetails(context.Background(), filter)
	require.NoError(t, err)
	require.Empty(t, items)
	require.Zero(t, total)
	require.NoError(t, mock.ExpectationsWereMet())
}
