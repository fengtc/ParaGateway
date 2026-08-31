-- Add the number of successful requests that contributed a complete response
-- duration to the ops pre-aggregation tables.
--
-- success_count includes successful rows whose duration_ms is NULL (for
-- example, an interrupted or otherwise incomplete response).  Keeping this
-- count separate prevents E2E latency aggregates from being weighted by rows
-- that did not contribute a duration sample.
--
-- Existing aggregate rows predate the sample counter.  Seed those rows with
-- success_count only when they already contain a duration metric.  This keeps
-- historical E2E charts available without an expensive migration-time scan of
-- usage_logs; every subsequent hourly aggregation replaces the compatibility
-- value with the exact COUNT(duration_ms).
--
-- Migration 145 introduced ttft_sample_count with the same zero default and
-- left older buckets untouched.  Repair those legacy rows here as well so the
-- newly separated TTFT chart does not disappear for historical windows.  The
-- success_count fallback is an approximation only for pre-existing rows; new
-- hourly aggregates continue to store the exact COUNT(first_token_ms).

SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '10min';

ALTER TABLE ops_metrics_hourly
    ADD COLUMN IF NOT EXISTS duration_sample_count BIGINT NOT NULL DEFAULT 0;

ALTER TABLE ops_metrics_daily
    ADD COLUMN IF NOT EXISTS duration_sample_count BIGINT NOT NULL DEFAULT 0;

UPDATE ops_metrics_hourly
SET duration_sample_count = success_count
WHERE duration_sample_count = 0
  AND success_count > 0
  AND (
      duration_p50_ms IS NOT NULL
      OR duration_p90_ms IS NOT NULL
      OR duration_p95_ms IS NOT NULL
      OR duration_p99_ms IS NOT NULL
      OR duration_avg_ms IS NOT NULL
      OR duration_max_ms IS NOT NULL
  );

UPDATE ops_metrics_daily
SET duration_sample_count = success_count
WHERE duration_sample_count = 0
  AND success_count > 0
  AND (
      duration_p50_ms IS NOT NULL
      OR duration_p90_ms IS NOT NULL
      OR duration_p95_ms IS NOT NULL
      OR duration_p99_ms IS NOT NULL
      OR duration_avg_ms IS NOT NULL
      OR duration_max_ms IS NOT NULL
  );

UPDATE ops_metrics_hourly
SET ttft_sample_count = success_count
WHERE ttft_sample_count = 0
  AND success_count > 0
  AND (
      ttft_p50_ms IS NOT NULL
      OR ttft_p90_ms IS NOT NULL
      OR ttft_p95_ms IS NOT NULL
      OR ttft_p99_ms IS NOT NULL
      OR ttft_avg_ms IS NOT NULL
      OR ttft_max_ms IS NOT NULL
  );

UPDATE ops_metrics_daily
SET ttft_sample_count = success_count
WHERE ttft_sample_count = 0
  AND success_count > 0
  AND (
      ttft_p50_ms IS NOT NULL
      OR ttft_p90_ms IS NOT NULL
      OR ttft_p95_ms IS NOT NULL
      OR ttft_p99_ms IS NOT NULL
      OR ttft_avg_ms IS NOT NULL
      OR ttft_max_ms IS NOT NULL
  );
