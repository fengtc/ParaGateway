package migrations

import (
	"os"
	"strings"
	"testing"
)

func TestDurationSampleCountMigrationIsIdempotent(t *testing.T) {
	data, err := os.ReadFile("237_ops_metrics_duration_sample_count.sql")
	if err != nil {
		t.Fatal(err)
	}
	sql := strings.ToLower(string(data))
	for _, table := range []string{"ops_metrics_hourly", "ops_metrics_daily"} {
		if !strings.Contains(sql, "alter table "+table) {
			t.Fatalf("migration does not alter %s", table)
		}
	}
	if strings.Count(sql, "duration_sample_count bigint not null default 0") != 2 {
		t.Fatalf("expected duration_sample_count on both pre-aggregation tables")
	}
	if !strings.Contains(sql, "add column if not exists") {
		t.Fatalf("migration must be idempotent")
	}
	for _, table := range []string{"ops_metrics_hourly", "ops_metrics_daily"} {
		if !strings.Contains(sql, "update "+table+"\nset duration_sample_count = success_count") {
			t.Fatalf("migration does not seed legacy duration samples for %s", table)
		}
	}
	if !strings.Contains(sql, "duration_sample_count = 0") || !strings.Contains(sql, "duration_p50_ms is not null") {
		t.Fatalf("legacy compatibility seed must only touch rows with duration metrics and no sample count")
	}
	for _, table := range []string{"ops_metrics_hourly", "ops_metrics_daily"} {
		if !strings.Contains(sql, "update "+table+"\nset ttft_sample_count = success_count") {
			t.Fatalf("migration does not repair legacy TTFT samples for %s", table)
		}
	}
	if !strings.Contains(sql, "ttft_sample_count = 0") || !strings.Contains(sql, "ttft_p50_ms is not null") {
		t.Fatalf("legacy TTFT compatibility seed must only touch rows with TTFT metrics and no sample count")
	}
}
