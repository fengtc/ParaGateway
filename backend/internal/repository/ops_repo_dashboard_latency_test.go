package repository

import (
	"database/sql"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestAggregateHourlyRowsWeightsDurationAndTTFTByTheirOwnSamples(t *testing.T) {
	rows := []opsHourlyMetricsRow{
		{
			successCount:        100,
			durationSampleCount: 1,
			ttftSampleCount:     9,
			durationP50:         sql.NullInt64{Int64: 100, Valid: true},
			durationP90:         sql.NullInt64{Int64: 200, Valid: true},
			durationAvg:         sql.NullFloat64{Float64: 150, Valid: true},
			ttftP50:             sql.NullInt64{Int64: 1000, Valid: true},
			ttftP90:             sql.NullInt64{Int64: 1200, Valid: true},
			ttftAvg:             sql.NullFloat64{Float64: 1100, Valid: true},
		},
		{
			successCount:        1,
			durationSampleCount: 9,
			ttftSampleCount:     1,
			durationP50:         sql.NullInt64{Int64: 1000, Valid: true},
			durationP90:         sql.NullInt64{Int64: 2000, Valid: true},
			durationAvg:         sql.NullFloat64{Float64: 1500, Valid: true},
			ttftP50:             sql.NullInt64{Int64: 100, Valid: true},
			ttftP90:             sql.NullInt64{Int64: 200, Valid: true},
			ttftAvg:             sql.NullFloat64{Float64: 150, Valid: true},
		},
	}

	got := aggregateHourlyRows(rows)
	require.Equal(t, int64(101), got.successCount)
	require.Equal(t, int64(10), got.durationSampleCount)
	require.Equal(t, int64(10), got.ttftSampleCount)
	require.NotNil(t, got.duration.P50)
	require.Equal(t, 910, *got.duration.P50)
	require.NotNil(t, got.duration.P90)
	require.Equal(t, 1820, *got.duration.P90)
	require.NotNil(t, got.duration.Avg)
	require.Equal(t, 1365, *got.duration.Avg)
	require.NotNil(t, got.ttft.P50)
	require.Equal(t, 910, *got.ttft.P50)
	require.NotNil(t, got.ttft.P90)
	require.Equal(t, 1100, *got.ttft.P90)
	require.NotNil(t, got.ttft.Avg)
	require.Equal(t, 1005, *got.ttft.Avg)
}

func TestAggregateHourlyRowsIgnoresDurationMetricsWithoutSamples(t *testing.T) {
	rows := []opsHourlyMetricsRow{{
		successCount:        5,
		durationSampleCount: 0,
		ttftSampleCount:     0,
		durationP50:         sql.NullInt64{Int64: 999, Valid: true},
		durationP95:         sql.NullInt64{Int64: 999, Valid: true},
		durationMax:         sql.NullInt64{Int64: 999, Valid: true},
		ttftP50:             sql.NullInt64{Int64: 999, Valid: true},
		ttftP95:             sql.NullInt64{Int64: 999, Valid: true},
		ttftMax:             sql.NullInt64{Int64: 999, Valid: true},
	}}

	got := aggregateHourlyRows(rows)
	require.Nil(t, got.duration.P50)
	require.Nil(t, got.duration.P95)
	require.Nil(t, got.duration.Max)
	require.Nil(t, got.ttft.P50)
	require.Nil(t, got.ttft.P95)
	require.Nil(t, got.ttft.Max)
}

func TestCombineApproxPercentilesIgnoresSegmentsWithoutSamples(t *testing.T) {
	stale := 999
	valid := 100
	got := combineApproxPercentiles([]opsPercentileSegment{
		{weight: 0, p: percentilesWithTail(stale)},
		{weight: 1, p: percentilesWithTail(valid)},
	})
	require.Equal(t, valid, *got.P95)
	require.Equal(t, valid, *got.P99)
	require.Equal(t, valid, *got.Max)
}

func percentilesWithTail(value int) service.OpsPercentiles {
	return service.OpsPercentiles{P95: &value, P99: &value, Max: &value}
}
