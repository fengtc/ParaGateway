-- A review either has a complete previous classification snapshot or none.
-- Migration 236's combination CHECK can evaluate to UNKNOWN for a half-null
-- pair, and PostgreSQL accepts UNKNOWN CHECK results.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'usage_work_reviews_previous_pair_presence_check'
          AND conrelid = 'public.usage_work_reviews'::regclass
    ) THEN
        ALTER TABLE usage_work_reviews
            ADD CONSTRAINT usage_work_reviews_previous_pair_presence_check
            CHECK (
                (previous_work_related IS NULL) = (previous_category IS NULL)
            ) NOT VALID;
    END IF;
END $$;

UPDATE usage_work_reviews
SET previous_work_related = CASE previous_category
    WHEN 'non_work' THEN 'non_work'
    WHEN 'unclassified' THEN 'uncertain'
    ELSE 'work'
END
WHERE previous_work_related IS NULL
  AND previous_category IS NOT NULL;

UPDATE usage_work_reviews
SET previous_category = CASE previous_work_related
    WHEN 'non_work' THEN 'non_work'
    WHEN 'uncertain' THEN 'unclassified'
END
WHERE previous_work_related IN ('non_work', 'uncertain')
  AND previous_category IS NULL;

UPDATE usage_work_reviews
SET previous_work_related = NULL,
    previous_category = NULL
WHERE previous_work_related = 'work'
  AND previous_category IS NULL;

ALTER TABLE usage_work_reviews
    VALIDATE CONSTRAINT usage_work_reviews_previous_pair_presence_check;
