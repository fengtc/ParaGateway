package migrations

import (
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestWorkReviewPreviousPairMigrationCleansAndConstrainsHalfNullSnapshots(t *testing.T) {
	content, err := FS.ReadFile("238_work_review_previous_pair.sql")
	require.NoError(t, err)
	sql := string(content)

	for _, required := range []string{
		"UPDATE usage_work_reviews",
		"SET previous_work_related = CASE previous_category",
		"WHEN 'non_work' THEN 'non_work'",
		"WHEN 'unclassified' THEN 'uncertain'",
		"ELSE 'work'",
		"SET previous_category = CASE previous_work_related",
		"WHEN 'uncertain' THEN 'unclassified'",
		"WHERE previous_work_related IN ('non_work', 'uncertain')",
		"SET previous_work_related = NULL",
		"previous_category = NULL",
		"WHERE previous_work_related = 'work'",
		"usage_work_reviews_previous_pair_presence_check",
		"(previous_work_related IS NULL) = (previous_category IS NULL)",
		"NOT VALID",
		"VALIDATE CONSTRAINT usage_work_reviews_previous_pair_presence_check",
	} {
		require.Contains(t, sql, required)
	}

	require.NotContains(t, strings.ToUpper(sql), "DROP CONSTRAINT")
	require.NotContains(t, strings.ToUpper(sql), "BEGIN;")
	require.NotContains(t, strings.ToUpper(sql), "COMMIT;")
	require.Equal(t, 1, strings.Count(sql, "ADD CONSTRAINT"))
	require.Equal(t, 3, strings.Count(sql, "UPDATE usage_work_reviews"))
	require.Less(t, strings.Index(sql, "ADD CONSTRAINT"), strings.Index(sql, "UPDATE usage_work_reviews"))
	require.Less(t, strings.Index(sql, "UPDATE usage_work_reviews"), strings.Index(sql, "VALIDATE CONSTRAINT"))
}
