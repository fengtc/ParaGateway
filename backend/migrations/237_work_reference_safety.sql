-- Harden structured project/repository references without changing migration 236.
-- Existing unsafe values are discarded in full; prompt text, source code, and
-- credential-like values must never remain in attribution metadata.

CREATE OR REPLACE FUNCTION public.paragateway_is_safe_work_reference(
    reference_value TEXT,
    max_length INTEGER,
    allow_slash BOOLEAN
)
RETURNS BOOLEAN
LANGUAGE SQL
IMMUTABLE
PARALLEL SAFE
SET search_path = pg_catalog
AS $function$
    SELECT
        reference_value = BTRIM(reference_value)
        AND CHAR_LENGTH(reference_value) BETWEEN 1 AND max_length
        AND reference_value !~ '[[:cntrl:]]'
        AND CASE
            WHEN allow_slash THEN
                reference_value ~ '^[A-Za-z0-9一-龥_./-]+$'
                AND CHAR_LENGTH(reference_value)
                    - CHAR_LENGTH(REPLACE(reference_value, '/', '')) <= 1
                AND reference_value !~ '(^/|/$|//)'
                AND reference_value !~ '(^|/)[.]{1,2}(/|$)'
            ELSE
                reference_value ~ '^[A-Za-z0-9一-龥_.-]+$'
        END
        AND reference_value ~ '[A-Za-z0-9一-龥]'
        AND reference_value !~* '^(please|help|write|create|explain|review|fix|translate|summarize|generate|tell|show|how|why|what|can|could|would|package|import|func|function|class|select|insert|update|delete)([^A-Za-z0-9_]|$)'
        AND reference_value !~ '^(请|请问|帮我|帮忙|如何|怎么|为什么|能否|可否|给我|以下|这段)'
        AND POSITION('完整源代码' IN reference_value) = 0
        AND reference_value !~* '(bearer|basic)[[:space:]]+[A-Za-z0-9._~+/=-]{12,}'
        AND reference_value !~* '(sk|rk|pk)-[A-Za-z0-9_-]{16,}'
        AND reference_value !~* 'gh[pousr]_[A-Za-z0-9]{20,}'
        AND reference_value !~* 'github_pat_[A-Za-z0-9_]{20,}'
        AND reference_value !~ 'AKIA[0-9A-Z]{16}'
        AND reference_value !~ 'eyJ[A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}'
        AND reference_value !~ 'AIza[0-9A-Za-z_-]{30,}'
        AND reference_value !~* 'xox[baprs]-[a-z0-9-]{16,}'
        AND reference_value !~ '[A-Za-z0-9]{32,}';
$function$;

CREATE OR REPLACE FUNCTION public.paragateway_is_safe_work_attribution(
    attribution JSONB
)
RETURNS BOOLEAN
LANGUAGE SQL
IMMUTABLE
PARALLEL SAFE
SET search_path = pg_catalog
AS $function$
    SELECT
        jsonb_typeof(attribution) = 'object'
        AND attribution - ARRAY[
            'project_ref', 'repository_ref', 'submission_type',
            'work_related', 'category', 'confidence',
            'classification_source', 'classifier_version'
        ]::text[] = '{}'::jsonb
        AND pg_column_size(attribution) <= 2048
        AND attribution ?& ARRAY[
            'work_related', 'category', 'confidence', 'classification_source'
        ]::text[]
        AND (
            NOT (attribution ? 'project_ref')
            OR (
                jsonb_typeof(attribution -> 'project_ref') = 'string'
                AND public.paragateway_is_safe_work_reference(
                    attribution ->> 'project_ref', 100, false
                )
            )
        )
        AND (
            NOT (attribution ? 'repository_ref')
            OR (
                jsonb_typeof(attribution -> 'repository_ref') = 'string'
                AND public.paragateway_is_safe_work_reference(
                    attribution ->> 'repository_ref', 160, true
                )
            )
        )
        AND (
            NOT (attribution ? 'submission_type')
            OR (
                jsonb_typeof(attribution -> 'submission_type') = 'string'
                AND attribution ->> 'submission_type' IN (
                    'coding', 'commit', 'pull_request', 'merge_request',
                    'documentation', 'data_analysis', 'operations',
                    'deployment', 'incident', 'communication', 'meeting',
                    'learning', 'training', 'other', 'non_work'
                )
            )
        )
        AND jsonb_typeof(attribution -> 'work_related') = 'string'
        AND attribution ->> 'work_related' IN ('work', 'non_work', 'uncertain')
        AND jsonb_typeof(attribution -> 'category') = 'string'
        AND attribution ->> 'category' IN (
            'coding', 'documentation', 'data_analysis', 'operations',
            'communication', 'learning', 'other', 'unclassified', 'non_work'
        )
        AND (
            (
                attribution ->> 'work_related' = 'work'
                AND attribution ->> 'category' IN (
                    'coding', 'documentation', 'data_analysis', 'operations',
                    'communication', 'learning', 'other'
                )
            )
            OR (
                attribution ->> 'work_related' = 'non_work'
                AND attribution ->> 'category' = 'non_work'
            )
            OR (
                attribution ->> 'work_related' = 'uncertain'
                AND attribution ->> 'category' = 'unclassified'
            )
        )
        AND jsonb_typeof(attribution -> 'confidence') = 'number'
        AND attribution -> 'confidence' >= '0'::jsonb
        AND attribution -> 'confidence' <= '1'::jsonb
        AND jsonb_typeof(attribution -> 'classification_source') = 'string'
        AND attribution ->> 'classification_source' IN (
            'explicit_metadata', 'local_rule', 'unclassified',
            'manual_review', 'import'
        )
        AND (
            NOT (attribution ? 'classifier_version')
            OR (
                jsonb_typeof(attribution -> 'classifier_version') = 'string'
                AND public.paragateway_is_safe_work_reference(
                    attribution ->> 'classifier_version', 64, false
                )
            )
        );
$function$;

UPDATE usage_work_metadata
SET project_ref = NULL,
    updated_at = NOW()
WHERE project_ref IS NOT NULL
  AND NOT public.paragateway_is_safe_work_reference(project_ref, 100, false);

UPDATE usage_work_metadata
SET repository_ref = NULL,
    updated_at = NOW()
WHERE repository_ref IS NOT NULL
  AND NOT public.paragateway_is_safe_work_reference(repository_ref, 160, true);

UPDATE batch_image_jobs
SET work_attribution = work_attribution - 'project_ref'
WHERE work_attribution ? 'project_ref'
  AND (
      jsonb_typeof(work_attribution -> 'project_ref') IS DISTINCT FROM 'string'
      OR NOT COALESCE(
          public.paragateway_is_safe_work_reference(work_attribution ->> 'project_ref', 100, false),
          false
      )
  );

UPDATE batch_image_jobs
SET work_attribution = work_attribution - 'repository_ref'
WHERE work_attribution ? 'repository_ref'
  AND (
      jsonb_typeof(work_attribution -> 'repository_ref') IS DISTINCT FROM 'string'
      OR NOT COALESCE(
          public.paragateway_is_safe_work_reference(work_attribution ->> 'repository_ref', 160, true),
          false
      )
  );

UPDATE batch_image_jobs
SET work_attribution = NULL
WHERE work_attribution IS NOT NULL
  AND NOT COALESCE(
      public.paragateway_is_safe_work_attribution(work_attribution),
      false
  );

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'usage_work_metadata_project_ref_check'
          AND conrelid = 'usage_work_metadata'::regclass
    ) THEN
        ALTER TABLE usage_work_metadata
            ADD CONSTRAINT usage_work_metadata_project_ref_check CHECK (
                project_ref IS NULL
                OR public.paragateway_is_safe_work_reference(project_ref, 100, false)
            );
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'usage_work_metadata_repository_ref_check'
          AND conrelid = 'usage_work_metadata'::regclass
    ) THEN
        ALTER TABLE usage_work_metadata
            ADD CONSTRAINT usage_work_metadata_repository_ref_check CHECK (
                repository_ref IS NULL
                OR public.paragateway_is_safe_work_reference(repository_ref, 160, true)
            );
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'batch_image_jobs_work_reference_safety_check'
          AND conrelid = 'batch_image_jobs'::regclass
    ) THEN
        ALTER TABLE batch_image_jobs
            ADD CONSTRAINT batch_image_jobs_work_reference_safety_check CHECK (
                work_attribution IS NULL
                OR public.paragateway_is_safe_work_attribution(work_attribution)
            );
    END IF;
END $$;
