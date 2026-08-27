-- Structured work-distribution metadata, classifications, and review workflow.
-- These tables intentionally contain no prompt text, request body, source code, or credentials.

ALTER TABLE batch_image_jobs
    ADD COLUMN IF NOT EXISTS work_attribution JSONB;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'batch_image_jobs_work_attribution_check'
          AND conrelid = 'batch_image_jobs'::regclass
    ) THEN
        ALTER TABLE batch_image_jobs
            ADD CONSTRAINT batch_image_jobs_work_attribution_check CHECK (
                work_attribution IS NULL
                OR (
                    jsonb_typeof(work_attribution) = 'object'
                    AND work_attribution - ARRAY[
                        'project_ref', 'repository_ref', 'submission_type',
                        'work_related', 'category', 'confidence',
                        'classification_source', 'classifier_version'
                    ]::text[] = '{}'::jsonb
                    AND pg_column_size(work_attribution) <= 2048
                )
            );
    END IF;
END $$;

-- Reuse the existing user-attribute editor for a business-facing role
-- dimension. Snapshot persistence applies a fail-closed business-label
-- contract before this value reaches the historical metadata table.
INSERT INTO user_attribute_definitions (
    key,
    name,
    description,
    type,
    options,
    required,
    validation,
    placeholder,
    display_order,
    enabled,
    created_at,
    updated_at
)
SELECT
    'job_role',
    '岗位角色',
    '用户的业务岗位或工作角色',
    'text',
    '[]'::jsonb,
    false,
    '{}'::jsonb,
    '例如：研发、产品、销售',
    COALESCE((
        SELECT MAX(display_order) + 1
        FROM user_attribute_definitions
        WHERE deleted_at IS NULL
    ), 0),
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1
    FROM user_attribute_definitions
    WHERE key = 'job_role'
      AND deleted_at IS NULL
);

CREATE TABLE IF NOT EXISTS usage_work_metadata (
    usage_log_id BIGINT PRIMARY KEY REFERENCES usage_logs(id) ON DELETE CASCADE,
    project_ref VARCHAR(100),
    repository_ref VARCHAR(160),
    submission_type VARCHAR(32),
    department VARCHAR(100),
    role VARCHAR(50),
    source VARCHAR(32) NOT NULL DEFAULT 'explicit',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT usage_work_metadata_source_check CHECK (
        source IN ('explicit', 'client_metadata', 'import', 'manual')
    ),
    CONSTRAINT usage_work_metadata_submission_type_check CHECK (
        submission_type IS NULL OR submission_type IN (
            'coding', 'commit', 'pull_request', 'merge_request',
            'documentation', 'data_analysis', 'operations',
            'deployment', 'incident', 'communication', 'meeting',
            'learning', 'training', 'other', 'non_work'
        )
    ),
    CONSTRAINT usage_work_metadata_department_label_check CHECK (
        department IS NULL
        OR department = 'unknown'
        OR (
            department = BTRIM(department)
            AND CHAR_LENGTH(department) BETWEEN 1 AND 100
            AND department !~ '[[:cntrl:]]'
            AND department ~ '^[A-Za-z0-9一-龥 _.#·•（）()、/&+-]+$'
            AND department ~ '[A-Za-z0-9一-龥]'
            AND department !~* '^(please|help|write|create|explain|review|fix|translate|summarize|generate|tell|show|how|why|what|can|could|would|package|import|func|function|class|select|insert|update|delete)([^A-Za-z0-9_]|$)'
            AND department !~ '^(请|请问|帮我|帮忙|如何|怎么|为什么|能否|可否|给我|以下|这段)'
            AND POSITION('完整源代码' IN department) = 0
            AND department !~* '(bearer|basic)[[:space:]]+[A-Za-z0-9._~+/=-]{12,}'
            AND department !~* '(sk|rk|pk)-[A-Za-z0-9_-]{16,}'
            AND department !~* 'gh[pousr]_[A-Za-z0-9]{20,}'
            AND department !~* 'github_pat_[A-Za-z0-9_]{20,}'
            AND department !~ 'AKIA[0-9A-Z]{16}'
            AND department !~ 'eyJ[A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}'
            AND department !~ '[A-Za-z0-9_-]{24,}'
        )
    ),
    CONSTRAINT usage_work_metadata_role_label_check CHECK (
        role IS NULL
        OR role = 'unknown'
        OR (
            role = BTRIM(role)
            AND CHAR_LENGTH(role) BETWEEN 1 AND 50
            AND role !~ '[[:cntrl:]]'
            AND role ~ '^[A-Za-z0-9一-龥 _.#·•（）()、/&+-]+$'
            AND role ~ '[A-Za-z0-9一-龥]'
            AND role !~* '^(please|help|write|create|explain|review|fix|translate|summarize|generate|tell|show|how|why|what|can|could|would|package|import|func|function|class|select|insert|update|delete)([^A-Za-z0-9_]|$)'
            AND role !~ '^(请|请问|帮我|帮忙|如何|怎么|为什么|能否|可否|给我|以下|这段)'
            AND POSITION('完整源代码' IN role) = 0
            AND role !~* '(bearer|basic)[[:space:]]+[A-Za-z0-9._~+/=-]{12,}'
            AND role !~* '(sk|rk|pk)-[A-Za-z0-9_-]{16,}'
            AND role !~* 'gh[pousr]_[A-Za-z0-9]{20,}'
            AND role !~* 'github_pat_[A-Za-z0-9_]{20,}'
            AND role !~ 'AKIA[0-9A-Z]{16}'
            AND role !~ 'eyJ[A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}'
            AND role !~ '[A-Za-z0-9_-]{24,}'
        )
    )
);

CREATE INDEX IF NOT EXISTS idx_usage_work_metadata_department
    ON usage_work_metadata (department) WHERE department IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_usage_work_metadata_role
    ON usage_work_metadata (role) WHERE role IS NOT NULL;

CREATE TABLE IF NOT EXISTS usage_work_classifications (
    usage_log_id BIGINT PRIMARY KEY REFERENCES usage_logs(id) ON DELETE CASCADE,
    user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    work_related VARCHAR(16) NOT NULL,
    category VARCHAR(32) NOT NULL,
    weight BIGINT NOT NULL,
    confidence DECIMAL(5,4),
    classification_source VARCHAR(32) NOT NULL,
    classifier_version VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT usage_work_classifications_work_related_check CHECK (
        work_related IN ('work', 'non_work', 'uncertain')
    ),
    CONSTRAINT usage_work_classifications_category_check CHECK (
        category IN (
            'coding', 'documentation', 'data_analysis', 'operations',
            'communication', 'learning', 'other', 'unclassified', 'non_work'
        )
    ),
    CONSTRAINT usage_work_classifications_confidence_check CHECK (
        confidence IS NULL OR (confidence >= 0 AND confidence <= 1)
    ),
    CONSTRAINT usage_work_classifications_weight_check CHECK (weight >= 1),
    CONSTRAINT usage_work_classifications_source_check CHECK (
        classification_source IN (
            'explicit_metadata', 'local_rule', 'unclassified',
            'manual_review', 'import'
        )
    ),
    CONSTRAINT usage_work_classifications_combination_check CHECK (
        (work_related = 'work' AND category IN (
            'coding', 'documentation', 'data_analysis', 'operations',
            'communication', 'learning', 'other'
        ))
        OR (work_related = 'non_work' AND category = 'non_work')
        OR (work_related = 'uncertain' AND category = 'unclassified')
    )
);

CREATE INDEX IF NOT EXISTS idx_usage_work_classifications_user
    ON usage_work_classifications (user_id);
CREATE INDEX IF NOT EXISTS idx_usage_work_classifications_category
    ON usage_work_classifications (category);
CREATE INDEX IF NOT EXISTS idx_usage_work_classifications_work_related
    ON usage_work_classifications (work_related);

CREATE TABLE IF NOT EXISTS usage_work_reviews (
    id BIGSERIAL PRIMARY KEY,
    usage_log_id BIGINT NOT NULL REFERENCES usage_logs(id) ON DELETE CASCADE,
    previous_work_related VARCHAR(16),
    previous_category VARCHAR(32),
    proposed_work_related VARCHAR(16) NOT NULL,
    proposed_category VARCHAR(32) NOT NULL,
    reason_code VARCHAR(40) NOT NULL,
    status VARCHAR(16) NOT NULL DEFAULT 'pending',
    resolution_note VARCHAR(40),
    requested_by BIGINT REFERENCES users(id) ON DELETE SET NULL,
    resolved_by BIGINT REFERENCES users(id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at TIMESTAMPTZ,
    CONSTRAINT usage_work_reviews_previous_work_related_check CHECK (
        previous_work_related IS NULL OR previous_work_related IN ('work', 'non_work', 'uncertain')
    ),
    CONSTRAINT usage_work_reviews_proposed_work_related_check CHECK (
        proposed_work_related IN ('work', 'non_work', 'uncertain')
    ),
    CONSTRAINT usage_work_reviews_previous_category_check CHECK (
        previous_category IS NULL OR previous_category IN (
            'coding', 'documentation', 'data_analysis', 'operations',
            'communication', 'learning', 'other', 'unclassified', 'non_work'
        )
    ),
    CONSTRAINT usage_work_reviews_proposed_category_check CHECK (
        proposed_category IN (
            'coding', 'documentation', 'data_analysis', 'operations',
            'communication', 'learning', 'other', 'unclassified', 'non_work'
        )
    ),
    CONSTRAINT usage_work_reviews_previous_combination_check CHECK (
        (previous_work_related IS NULL AND previous_category IS NULL)
        OR (previous_work_related = 'work' AND previous_category IN (
            'coding', 'documentation', 'data_analysis', 'operations',
            'communication', 'learning', 'other'
        ))
        OR (previous_work_related = 'non_work' AND previous_category = 'non_work')
        OR (previous_work_related = 'uncertain' AND previous_category = 'unclassified')
    ),
    CONSTRAINT usage_work_reviews_proposed_combination_check CHECK (
        (proposed_work_related = 'work' AND proposed_category IN (
            'coding', 'documentation', 'data_analysis', 'operations',
            'communication', 'learning', 'other'
        ))
        OR (proposed_work_related = 'non_work' AND proposed_category = 'non_work')
        OR (proposed_work_related = 'uncertain' AND proposed_category = 'unclassified')
    ),
    CONSTRAINT usage_work_reviews_reason_check CHECK (
        reason_code IN (
            'incorrect_category', 'incorrect_work_relation',
            'missing_classification', 'other'
        )
    ),
    CONSTRAINT usage_work_reviews_status_check CHECK (
        status IN ('pending', 'approved', 'rejected')
    ),
    CONSTRAINT usage_work_reviews_resolution_note_check CHECK (
        resolution_note IS NULL OR resolution_note IN (
            'confirmed_correction', 'insufficient_evidence', 'duplicate',
            'invalid_request', 'other'
        )
    ),
    CONSTRAINT usage_work_reviews_resolution_check CHECK (
        (status = 'pending' AND resolved_by IS NULL AND resolved_at IS NULL AND resolution_note IS NULL)
        OR
        (status IN ('approved', 'rejected') AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL AND resolution_note IS NOT NULL)
    ),
    CONSTRAINT usage_work_reviews_decision_note_check CHECK (
        status = 'pending'
        OR (status = 'approved' AND resolution_note IN ('confirmed_correction', 'other'))
        OR (status = 'rejected' AND resolution_note IN (
            'insufficient_evidence', 'duplicate', 'invalid_request', 'other'
        ))
    )
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_usage_work_reviews_one_pending
    ON usage_work_reviews (usage_log_id) WHERE status = 'pending';
CREATE INDEX IF NOT EXISTS idx_usage_work_reviews_status_created
    ON usage_work_reviews (status, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_usage_work_reviews_usage_log
    ON usage_work_reviews (usage_log_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_usage_work_reviews_requested_by
    ON usage_work_reviews (requested_by) WHERE requested_by IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_usage_work_reviews_resolved_by
    ON usage_work_reviews (resolved_by) WHERE resolved_by IS NOT NULL;
