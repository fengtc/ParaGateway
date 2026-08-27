package repository

import (
	"context"
	"errors"
	"regexp"
	"strings"
	"testing"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestPrepareUsageWorkDefaultsToUnclassifiedAndUsesTokenWeight(t *testing.T) {
	prepared := prepareUsageWork(&service.UsageLog{InputTokens: 10, OutputTokens: 5})
	require.False(t, prepared.recoveryOnly)
	require.Equal(t, service.WorkRelatedUncertain, prepared.workRelated)
	require.Equal(t, service.WorkCategoryUnclassified, prepared.category)
	require.Equal(t, int64(15), prepared.weight)
	require.Equal(t, "unclassified", prepared.classificationSource)
}

func TestPrepareUsageWorkKeepsOnlyStructuredAttribution(t *testing.T) {
	log := &service.UsageLog{
		InputTokens: 20,
		WorkAttribution: &service.UsageWorkAttribution{
			ProjectRef: "customer-platform", RepositoryRef: "team/backend",
			SubmissionType: "pull request", WorkRelated: service.WorkRelatedWork,
			Category: service.WorkCategoryCoding, Confidence: 0.82,
			ClassificationSource: "local_rule", ClassifierVersion: "rules-v1",
		},
	}
	prepared := prepareUsageWork(log)
	require.Equal(t, "pull_request", prepared.submissionType)
	require.Equal(t, service.WorkCategoryCoding, prepared.category)
	require.Equal(t, int64(20), prepared.weight)
	query, args := buildUsageWorkUpsertQuery([]usageWorkPrepared{prepared})
	require.Contains(t, query, "usage_work_classifications")
	require.NotContains(t, query, "prompt_text")
	require.NotContains(t, strings.Join(anyStrings(args), " "), "source code")
	require.Len(t, args, 13)
	require.Equal(t, false, args[3])
	require.Contains(t, query, "COALESCE(NULLIF(usage_work_metadata.project_ref, ''), EXCLUDED.project_ref)")
	require.Contains(t, query, "COALESCE(NULLIF(usage_work_metadata.repository_ref, ''), EXCLUDED.repository_ref)")
	require.Contains(t, query, "recovery_only")
	require.Contains(t, query, "ON NOT r.recovery_only AND snapshot_user.id = r.user_id")
	require.Contains(t, query, "ON NOT r.recovery_only\n   AND uad_department.key = 'department'")
	require.Contains(t, query, "uad_department.key = 'department'")
	require.Contains(t, query, "uad_job_role.key = 'job_role'")
	require.Contains(t, query, "COALESCE(NULLIF(BTRIM(uav_department.value), ''), 'unknown') AS department_candidate")
	require.Contains(t, query, "NULLIF(BTRIM(snapshot_user.role), '')")
	require.Contains(t, query, "AS role_candidate")
	require.Contains(t, query, "department, role, source")
	require.Contains(t, query, "COALESCE(NULLIF(usage_work_metadata.department, ''), EXCLUDED.department)")
	require.Contains(t, query, "COALESCE(NULLIF(usage_work_metadata.role, ''), EXCLUDED.role)")
	require.Equal(t, 1, strings.Count(query, "ON CONFLICT (usage_log_id) DO NOTHING"))
	require.NotContains(t, query, "classification_source <> 'manual_review'")
	require.NotContains(t, query, "user_id = EXCLUDED.user_id")
}

func TestPersistUsageWorkSavepointRecoversClassificationFailure(t *testing.T) {
	db, mock := newSQLMock(t)
	mock.ExpectExec(regexp.QuoteMeta("SAVEPOINT usage_work_classification")).
		WillReturnResult(sqlmock.NewResult(0, 0))
	mock.ExpectExec("(?s)WITH input.*dimension_snapshot.*INSERT INTO usage_work_metadata").
		WillReturnError(errors.New("classification table unavailable"))
	mock.ExpectExec(regexp.QuoteMeta("ROLLBACK TO SAVEPOINT usage_work_classification")).
		WillReturnResult(sqlmock.NewResult(0, 0))
	mock.ExpectExec(regexp.QuoteMeta("RELEASE SAVEPOINT usage_work_classification")).
		WillReturnResult(sqlmock.NewResult(0, 0))

	repo := &usageLogRepository{}
	repo.persistUsageWorkByID(context.Background(), db, &service.UsageLog{
		ID: 10, UserID: 20, APIKeyID: 30, RequestID: "request-1",
		WorkAttribution: &service.UsageWorkAttribution{
			ProjectRef: "paragateway", WorkRelated: service.WorkRelatedWork,
			Category: service.WorkCategoryCoding, Confidence: 0.8,
			ClassificationSource: "local_rule",
		},
	}, true, false)
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestPersistUsageWorkRowsUsesDimensionSnapshotForBatch(t *testing.T) {
	db, mock := newSQLMock(t)
	mock.ExpectExec("(?s)WITH input.*uad_department.*uad_job_role.*INSERT INTO usage_work_metadata.*department, role").
		WillReturnResult(sqlmock.NewResult(0, 2))

	err := persistUsageWorkRows(context.Background(), db, []usageWorkPrepared{
		{usageLogID: 10, workRelated: service.WorkRelatedWork, category: service.WorkCategoryCoding, weight: 1, classificationSource: "local_rule"},
		{usageLogID: 11, workRelated: service.WorkRelatedUncertain, category: service.WorkCategoryUnclassified, weight: 1, classificationSource: "unclassified"},
	})
	require.NoError(t, err)
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestFlushBestEffortBatchClassificationFailureDoesNotFailUsageLog(t *testing.T) {
	db, mock := newSQLMock(t)
	log := &service.UsageLog{
		UserID: 10, APIKeyID: 20, AccountID: 30, RequestID: "existing-request",
		InputTokens: 5, OutputTokens: 7,
		WorkAttribution: &service.UsageWorkAttribution{
			ProjectRef: "paragateway", WorkRelated: service.WorkRelatedWork,
			Category: service.WorkCategoryCoding, Confidence: 0.9,
			ClassificationSource: "local_rule", ClassifierVersion: "rules-v1",
		},
	}
	resultCh := make(chan error, 1)
	mock.ExpectQuery("(?s)WITH input.*INSERT INTO usage_logs").
		WillReturnRows(sqlmock.NewRows([]string{"request_id", "api_key_id"}))
	mock.ExpectExec("(?s)WITH input.*INSERT INTO usage_work_metadata.*INSERT INTO usage_work_classifications").
		WillReturnError(errors.New("classification table unavailable"))

	repo := newUsageLogRepositoryWithSQL(nil, db)
	repo.flushBestEffortBatch(db, []usageLogBestEffortRequest{{
		prepared: prepareUsageLogInsert(log),
		work:     prepareUsageWork(log),
		apiKeyID: log.APIKeyID,
		resultCh: resultCh,
	}})

	require.NoError(t, <-resultCh, "classification is best-effort and must not fail the usage log write")
	_, cached := repo.bestEffortRecent.Get(usageLogBatchKey(log.RequestID, log.APIKeyID))
	require.False(t, cached, "a failed classification write must remain retryable")
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestUsageWorkSnapshotBoundsLongUnicodeAttributesAtDatabaseBoundary(t *testing.T) {
	longDepartment := strings.Repeat("研发", 60)
	longRole := strings.Repeat("工程师", 30)
	require.Greater(t, len([]rune(longDepartment)), 100)
	require.Greater(t, len([]rune(longRole)), 50)

	query, _ := buildUsageWorkUpsertQuery([]usageWorkPrepared{{
		usageLogID: 10, workRelated: service.WorkRelatedWork,
		category: service.WorkCategoryCoding, weight: 1,
		classificationSource: "local_rule",
	}})
	require.Contains(t, query, "char_length(dc.department_candidate) > 100")
	require.Contains(t, query, "char_length(dc.role_candidate) > 50")
	require.Contains(t, query, "dc.department_candidate ~ $work_label$[[:cntrl:]]$work_label$")
	require.Equal(t, 2, strings.Count(query, usageWorkLabelAllowedPattern))
	require.Equal(t, 2, strings.Count(query, usageWorkLabelContentPattern))
	require.Equal(t, 2, strings.Count(query, usageWorkLabelEnglishFreeTextPattern))
	require.Equal(t, 2, strings.Count(query, usageWorkLabelChineseFreeTextPattern))
	require.Contains(t, query, "github_pat_")
	require.Contains(t, query, "AKIA[0-9A-Z]{16}")
	require.Contains(t, query, "eyJ[A-Za-z0-9_-]{10,}")
	require.Equal(t, 2, strings.Count(query, usageWorkLabelLongTokenPattern))
	require.Equal(t, 2, strings.Count(query, usageWorkLabelCompleteSourceCodeMarker))
	require.Contains(t, query, "THEN 'unknown'")
	require.NotContains(t, query, "LEFT(COALESCE", "invalid dimensions must be rejected as a whole, never truncated and retained")
	require.NotContains(t, query, "OCTET_LENGTH", "snapshot bounds must use PostgreSQL character semantics, not UTF-8 byte length")
}

func TestUsageWorkRowsForBatchIncludesExistingUsageLogsForSelfHeal(t *testing.T) {
	newLog := &service.UsageLog{WorkAttribution: &service.UsageWorkAttribution{
		WorkRelated: service.WorkRelatedWork, Category: service.WorkCategoryCoding,
		ClassificationSource: "local_rule",
	}}
	duplicateLog := &service.UsageLog{WorkAttribution: &service.UsageWorkAttribution{
		WorkRelated: service.WorkRelatedWork, Category: service.WorkCategoryDocumentation,
		ClassificationSource: "explicit_metadata",
	}}
	rows := usageWorkRowsForBatch(
		[]string{"new", "duplicate"},
		map[string][]usageLogCreateRequest{
			"new":       {{log: newLog}},
			"duplicate": {{log: duplicateLog}},
		},
		map[string]usageLogBatchState{
			"new":       {ID: 10},
			"duplicate": {ID: 11},
		},
		map[string]bool{"new": true, "duplicate": false},
	)
	require.Len(t, rows, 2)
	require.Equal(t, int64(10), rows[0].usageLogID)
	require.Equal(t, service.WorkCategoryCoding, rows[0].category)
	require.False(t, rows[0].recoveryOnly)
	require.Equal(t, int64(11), rows[1].usageLogID)
	require.Equal(t, service.WorkCategoryDocumentation, rows[1].category)
	require.True(t, rows[1].recoveryOnly)
}

func anyStrings(values []any) []string {
	result := make([]string, 0, len(values))
	for _, value := range values {
		if text, ok := value.(string); ok {
			result = append(result, text)
		}
	}
	return result
}
