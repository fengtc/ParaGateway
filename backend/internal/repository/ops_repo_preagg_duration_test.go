package repository

import (
	"context"
	"testing"
	"time"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/stretchr/testify/require"
)

func TestUpsertHourlyMetricsPersistsDurationSampleCount(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &opsRepository{db: db}
	start := time.Date(2026, 8, 31, 0, 0, 0, 0, time.UTC)
	end := start.Add(time.Hour)

	mock.ExpectExec(`(?s)COUNT\(\*\) FILTER \(WHERE duration_ms IS NOT NULL\) AS duration_sample_count.*INSERT INTO ops_metrics_hourly.*duration_sample_count.*duration_sample_count = EXCLUDED\.duration_sample_count`).
		WithArgs(start, end).
		WillReturnResult(sqlmock.NewResult(0, 1))

	require.NoError(t, repo.UpsertHourlyMetrics(context.Background(), start, end))
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestUpsertDailyMetricsWeightsDurationByDurationSamples(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &opsRepository{db: db}
	start := time.Date(2026, 8, 31, 0, 0, 0, 0, time.UTC)
	end := start.Add(24 * time.Hour)

	mock.ExpectExec(`(?s)SUM\(duration_p50_ms::double precision \* duration_sample_count\).*MAX\(duration_p95_ms\) FILTER \(WHERE duration_sample_count > 0\).*SUM\(duration_avg_ms \* duration_sample_count\).*MAX\(ttft_p95_ms\) FILTER \(WHERE ttft_sample_count > 0\).*duration_sample_count = EXCLUDED\.duration_sample_count`).
		WithArgs(start, end).
		WillReturnResult(sqlmock.NewResult(0, 1))

	require.NoError(t, repo.UpsertDailyMetrics(context.Background(), start, end))
	require.NoError(t, mock.ExpectationsWereMet())
}
