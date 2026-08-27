package repository

import (
	"context"
	"errors"
	"os"
	"regexp"
	"strings"
	"testing"
	"time"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestWorkDistributionRecordsQueryEnforcesSampleAndCohortPrivacy(t *testing.T) {
	start := time.Date(2026, 8, 1, 0, 0, 0, 0, time.UTC)
	query, args := buildWorkRecordsQuery(service.WorkDistributionRecordFilter{
		WorkDistributionFilter: service.WorkDistributionFilter{StartTime: start, EndTime: start.Add(24 * time.Hour), UserID: 7},
		MinSampleSize:          5,
		MinCohortSize:          5,
		Page:                   1,
		PageSize:               20,
	}, false)
	normalized := strings.Join(strings.Fields(query), " ")
	require.Contains(t, normalized, "COUNT(DISTINCT user_id)::bigint AS cohort_size")
	require.Contains(t, normalized, "HAVING COUNT(*) >= $3")
	require.Contains(t, normalized, "MIN(dc.cohort_size) >= $4")
	require.Contains(t, normalized, "b.user_id = $5")
	require.Equal(t, int64(5), args[2])
	require.Equal(t, int64(5), args[3])
	require.Equal(t, int64(7), args[4])
}

func TestWorkDistributionRecordsQueryAppliesDimensionFiltersBeforePrivacyCohorts(t *testing.T) {
	start := time.Date(2026, 8, 1, 0, 0, 0, 0, time.UTC)
	query, args := buildWorkRecordsQuery(service.WorkDistributionRecordFilter{
		WorkDistributionFilter: service.WorkDistributionFilter{StartTime: start, EndTime: start.Add(24 * time.Hour), UserID: 7},
		Category:               service.WorkCategoryCoding, MinSampleSize: 5, MinCohortSize: 5, Page: 1, PageSize: 20,
	}, false)
	normalized := strings.Join(strings.Fields(query), " ")
	filterIndex := strings.Index(normalized, "filtered_scope AS")
	cohortIndex := strings.Index(normalized, "department_cohorts AS")
	require.Greater(t, filterIndex, 0)
	require.Greater(t, cohortIndex, filterIndex)
	require.Contains(t, normalized[filterIndex:cohortIndex], "b.category = $3")
	require.Contains(t, normalized, "HAVING COUNT(*) >= $4")
	require.Contains(t, normalized, "MIN(dc.cohort_size) >= $5")
	require.Contains(t, normalized, "b.user_id = $6")
	require.Equal(t, service.WorkCategoryCoding, args[2])
}

func TestCreateWorkReviewDoesNotExposeAnotherUsersUsage(t *testing.T) {
	db, mock := newSQLMock(t)
	mock.ExpectBegin()
	mock.ExpectQuery(regexp.QuoteMeta("WHERE ul.id = $1 AND ($2::bigint = 0 OR ul.user_id = $2)")).
		WithArgs(int64(42), int64(7)).
		WillReturnRows(sqlmock.NewRows([]string{"user_id", "email", "work_related", "category"}))
	mock.ExpectRollback()

	repo := &workDistributionRepository{db: db}
	_, err := repo.CreateReview(context.Background(), service.CreateWorkReviewInput{
		UsageLogID: 42, OwnerUserID: 7, RequestedBy: 7,
		WorkRelated: service.WorkRelatedWork, Category: service.WorkCategoryCoding,
		ReasonCode: "incorrect_category",
	})
	require.ErrorIs(t, err, service.ErrWorkUsageNotFound)
	require.True(t, errors.Is(err, service.ErrWorkUsageNotFound))
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestWorkDistributionAggregateCoverageExcludesUnclassifiedRows(t *testing.T) {
	content := readCurrentFileForTest(t, "work_distribution_repo.go")
	require.Contains(t, content, "wc.category <> 'unclassified'")
	require.Contains(t, content, "wc.classification_source <> 'unclassified'")
}

func TestWorkDistributionDimensionsUseImmutableSnapshotsOnly(t *testing.T) {
	content := readCurrentFileForTest(t, "work_distribution_repo.go")
	require.Contains(t, content, "LEFT JOIN usage_work_metadata wm ON wm.usage_log_id = ul.id")
	require.Contains(t, content, "COALESCE(NULLIF(BTRIM(wm.department), ''), 'unknown')")
	require.Contains(t, content, "COALESCE(NULLIF(BTRIM(wm.role), ''), 'unknown')")
	require.NotContains(t, content, "user_attribute_values uav")
	require.NotContains(t, content, "NULLIF(BTRIM(u.role), '')")
}

func readCurrentFileForTest(t *testing.T, name string) string {
	t.Helper()
	data, err := os.ReadFile(name)
	require.NoError(t, err)
	return string(data)
}
