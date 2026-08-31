package repository

import (
	"context"
	"fmt"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/service"
)

func (r *opsRepository) GetLatencyHistogram(ctx context.Context, filter *service.OpsDashboardFilter) (*service.OpsLatencyHistogramResponse, error) {
	if r == nil || r.db == nil {
		return nil, fmt.Errorf("nil ops repository")
	}
	if filter == nil {
		return nil, fmt.Errorf("nil filter")
	}
	if filter.StartTime.IsZero() || filter.EndTime.IsZero() {
		return nil, fmt.Errorf("start_time/end_time required")
	}

	start := filter.StartTime.UTC()
	end := filter.EndTime.UTC()

	join, where, args, _ := buildUsageWhere(filter, start, end, 1)
	// Build both distributions from one filtered scan.  This keeps E2E and
	// TTFT counts on the same population and avoids running two potentially
	// expensive full-window queries.
	durationRangeExpr := latencyHistogramRangeCaseExpr("duration_ms")
	durationOrderExpr := latencyHistogramRangeOrderCaseExpr("duration_ms")
	ttftRangeExpr := latencyHistogramRangeCaseExpr("first_token_ms")
	ttftOrderExpr := latencyHistogramRangeOrderCaseExpr("first_token_ms")

	q := `
	WITH filtered AS (
	  SELECT ul.duration_ms, ul.first_token_ms
	  FROM usage_logs ul
	  ` + join + `
  ` + where + `
	), histogram AS (
	  SELECT 'duration'::text AS metric,
	         ` + durationRangeExpr + ` AS range,
	         ` + durationOrderExpr + ` AS ord
	  FROM filtered
	  WHERE duration_ms IS NOT NULL
	  UNION ALL
	  SELECT 'ttft'::text AS metric,
	         ` + ttftRangeExpr + ` AS range,
	         ` + ttftOrderExpr + ` AS ord
	  FROM filtered
	  WHERE first_token_ms IS NOT NULL
	)
	SELECT metric, range, COUNT(*) AS count, ord
	FROM histogram
	GROUP BY metric, range, ord
	ORDER BY metric ASC, ord ASC`

	rows, err := r.db.QueryContext(ctx, q, args...)
	if err != nil {
		return nil, err
	}
	defer func() { _ = rows.Close() }()

	durationCounts := make(map[string]int64, len(latencyHistogramOrderedRanges))
	ttftCounts := make(map[string]int64, len(latencyHistogramOrderedRanges))
	var durationTotal, ttftTotal int64
	for rows.Next() {
		var metric string
		var label string
		var count int64
		var _ord int
		if err := rows.Scan(&metric, &label, &count, &_ord); err != nil {
			return nil, err
		}
		switch metric {
		case "duration":
			durationCounts[label] = count
			durationTotal += count
		case "ttft":
			ttftCounts[label] = count
			ttftTotal += count
		}
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}

	buildBuckets := func(counts map[string]int64) []*service.OpsLatencyHistogramBucket {
		buckets := make([]*service.OpsLatencyHistogramBucket, 0, len(latencyHistogramOrderedRanges))
		for _, label := range latencyHistogramOrderedRanges {
			buckets = append(buckets, &service.OpsLatencyHistogramBucket{
				Range: label,
				Count: counts[label],
			})
		}
		return buckets
	}
	durationBuckets := buildBuckets(durationCounts)
	ttftBuckets := buildBuckets(ttftCounts)

	model := strings.TrimSpace(filter.Model)
	response := &service.OpsLatencyHistogramResponse{
		StartTime:             start,
		EndTime:               end,
		Platform:              strings.TrimSpace(filter.Platform),
		GroupID:               filter.GroupID,
		Model:                 model,
		AccountID:             filter.AccountID,
		TotalRequests:         durationTotal,
		Buckets:               durationBuckets,
		DurationTotalRequests: durationTotal,
		DurationBuckets:       durationBuckets,
		TTFTTotalRequests:     ttftTotal,
		TTFTBuckets:           ttftBuckets,
	}
	return response, nil
}
