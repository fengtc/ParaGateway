-- One classification result per usage log.
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
    CONSTRAINT usage_work_classifications_work_related_check CHECK (work_related IN ('work', 'non_work', 'uncertain')),
    CONSTRAINT usage_work_classifications_category_check CHECK (category IN ('coding', 'documentation', 'data_analysis', 'operations', 'communication', 'learning', 'other', 'unclassified', 'non_work')),
    CONSTRAINT usage_work_classifications_confidence_check CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1)),
    CONSTRAINT usage_work_classifications_weight_check CHECK (weight >= 1),
    CONSTRAINT usage_work_classifications_source_check CHECK (classification_source IN ('local_rule', 'unclassified', 'import')),
    CONSTRAINT usage_work_classifications_combination_check CHECK (
        (work_related = 'work' AND category IN ('coding', 'documentation', 'data_analysis', 'operations', 'communication', 'learning', 'other'))
        OR (work_related = 'non_work' AND category = 'non_work')
        OR (work_related = 'uncertain' AND category = 'unclassified')
    )
);

CREATE INDEX IF NOT EXISTS idx_usage_work_classifications_user ON usage_work_classifications (user_id);
CREATE INDEX IF NOT EXISTS idx_usage_work_classifications_category ON usage_work_classifications (category);
