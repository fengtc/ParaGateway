package repository

import (
	"fmt"
	"strings"
)

type latencyHistogramBucket struct {
	upperMs int
	label   string
}

var latencyHistogramBuckets = []latencyHistogramBucket{
	// The previous 100/200/500/1000/2000ms buckets made normal model
	// generation look like one giant "2000ms+" tail.  These wider buckets are
	// useful for both E2E duration and TTFT, while preserving deterministic
	// ordering for the dashboard.
	{upperMs: 1000, label: "0-1000ms"},
	{upperMs: 3000, label: "1000-3000ms"},
	{upperMs: 10000, label: "3000-10000ms"},
	{upperMs: 30000, label: "10000-30000ms"},
	{upperMs: 0, label: "30000ms+"}, // default bucket
}

var latencyHistogramOrderedRanges = func() []string {
	out := make([]string, 0, len(latencyHistogramBuckets))
	for _, b := range latencyHistogramBuckets {
		out = append(out, b.label)
	}
	return out
}()

func latencyHistogramRangeCaseExpr(column string) string {
	var sb strings.Builder
	_, _ = sb.WriteString("CASE\n")

	for _, b := range latencyHistogramBuckets {
		if b.upperMs <= 0 {
			continue
		}
		fmt.Fprintf(&sb, "\tWHEN %s < %d THEN '%s'\n", column, b.upperMs, b.label)
	}

	// Default bucket.
	last := latencyHistogramBuckets[len(latencyHistogramBuckets)-1]
	fmt.Fprintf(&sb, "\tELSE '%s'\n", last.label)
	_, _ = sb.WriteString("END")
	return sb.String()
}

func latencyHistogramRangeOrderCaseExpr(column string) string {
	var sb strings.Builder
	_, _ = sb.WriteString("CASE\n")

	order := 1
	for _, b := range latencyHistogramBuckets {
		if b.upperMs <= 0 {
			continue
		}
		fmt.Fprintf(&sb, "\tWHEN %s < %d THEN %d\n", column, b.upperMs, order)
		order++
	}

	fmt.Fprintf(&sb, "\tELSE %d\n", order)
	_, _ = sb.WriteString("END")
	return sb.String()
}
