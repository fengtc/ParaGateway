package repository

import (
	"context"
	"testing"
	"time"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestLatencyHistogramBuckets_AreConsistent(t *testing.T) {
	require.Equal(t, len(latencyHistogramBuckets), len(latencyHistogramOrderedRanges))
	for i, b := range latencyHistogramBuckets {
		require.Equal(t, b.label, latencyHistogramOrderedRanges[i])
	}
}

func TestLatencyHistogramBuckets_UseNewBoundaries(t *testing.T) {
	require.Equal(t, []int{1000, 3000, 10000, 30000, 0}, []int{
		latencyHistogramBuckets[0].upperMs,
		latencyHistogramBuckets[1].upperMs,
		latencyHistogramBuckets[2].upperMs,
		latencyHistogramBuckets[3].upperMs,
		latencyHistogramBuckets[4].upperMs,
	})
	require.Equal(t, []string{
		"0-1000ms",
		"1000-3000ms",
		"3000-10000ms",
		"10000-30000ms",
		"30000ms+",
	}, latencyHistogramOrderedRanges)
}

func TestLatencyHistogramRangeExpressions_UseExclusiveUpperBounds(t *testing.T) {
	rangeExpr := latencyHistogramRangeCaseExpr("value_ms")
	orderExpr := latencyHistogramRangeOrderCaseExpr("value_ms")
	for _, boundary := range []string{"< 1000", "< 3000", "< 10000", "< 30000"} {
		require.Contains(t, rangeExpr, boundary)
		require.Contains(t, orderExpr, boundary)
	}
	require.Contains(t, rangeExpr, "ELSE '30000ms+'")
}

func TestGetLatencyHistogram_ReturnsSeparateDurationAndTTFTDistributions(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &opsRepository{db: db}
	start := time.Date(2026, 8, 31, 0, 0, 0, 0, time.UTC)
	end := start.Add(time.Hour)

	mock.ExpectQuery(`WITH filtered`).
		WithArgs(start, end).
		WillReturnRows(sqlmock.NewRows([]string{"metric", "range", "count", "ord"}).
			AddRow("duration", "0-1000ms", int64(2), 1).
			AddRow("duration", "30000ms+", int64(1), 5).
			AddRow("ttft", "0-1000ms", int64(3), 1).
			AddRow("ttft", "1000-3000ms", int64(1), 2))

	resp, err := repo.GetLatencyHistogram(context.Background(), &service.OpsDashboardFilter{
		StartTime: start,
		EndTime:   end,
	})
	require.NoError(t, err)
	require.Equal(t, int64(3), resp.DurationTotalRequests)
	require.Equal(t, int64(4), resp.TTFTTotalRequests)
	require.Equal(t, resp.DurationTotalRequests, resp.TotalRequests)
	require.Equal(t, resp.DurationBuckets, resp.Buckets)
	require.Len(t, resp.DurationBuckets, len(latencyHistogramOrderedRanges))
	require.Len(t, resp.TTFTBuckets, len(latencyHistogramOrderedRanges))
	require.Equal(t, int64(2), resp.DurationBuckets[0].Count)
	require.Equal(t, int64(1), resp.DurationBuckets[4].Count)
	require.Equal(t, int64(3), resp.TTFTBuckets[0].Count)
	require.Equal(t, int64(1), resp.TTFTBuckets[1].Count)
	require.NoError(t, mock.ExpectationsWereMet())
}
