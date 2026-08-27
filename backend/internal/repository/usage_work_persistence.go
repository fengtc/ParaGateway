package repository

import (
	"context"
	"fmt"
	"math"
	"strconv"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/pkg/logger"
	"github.com/Wei-Shaw/sub2api/internal/service"
)

const (
	usageWorkLabelAllowedPattern           = `^[A-Za-z0-9一-龥 _.#·•（）()、/&+-]+$`
	usageWorkLabelContentPattern           = `[A-Za-z0-9一-龥]`
	usageWorkLabelEnglishFreeTextPattern   = `^(please|help|write|create|explain|review|fix|translate|summarize|generate|tell|show|how|why|what|can|could|would|package|import|func|function|class|select|insert|update|delete)([^A-Za-z0-9_]|$)`
	usageWorkLabelChineseFreeTextPattern   = `^(请|请问|帮我|帮忙|如何|怎么|为什么|能否|可否|给我|以下|这段)`
	usageWorkLabelCredentialFoldPattern    = `(bearer|basic)[[:space:]]+[A-Za-z0-9._~+/=-]{12,}|(sk|rk|pk)-[A-Za-z0-9_-]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}`
	usageWorkLabelCredentialExactPattern   = `AKIA[0-9A-Z]{16}|eyJ[A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}`
	usageWorkLabelLongTokenPattern         = `[A-Za-z0-9_-]{24,}`
	usageWorkLabelCompleteSourceCodeMarker = `完整源代码`
)

type usageWorkPrepared struct {
	usageLogID           int64
	requestID            string
	apiKeyID             int64
	recoveryOnly         bool
	projectRef           string
	repositoryRef        string
	submissionType       string
	workRelated          string
	category             string
	weight               int64
	confidence           float64
	classificationSource string
	classifierVersion    string
}

func usageWorkLabelGuardSQL(candidate string, maxRunes int) string {
	return fmt.Sprintf(`CASE
      WHEN %s IS NULL OR %s = 'unknown' THEN 'unknown'
      WHEN char_length(%s) < 1
        OR char_length(%s) > %d
        OR %s ~ $work_label$[[:cntrl:]]$work_label$
        OR %s !~ $work_label$%s$work_label$
        OR %s !~ $work_label$%s$work_label$
        OR lower(%s) ~ $work_label$%s$work_label$
        OR %s ~ $work_label$%s$work_label$
        OR strpos(%s, '%s') > 0
        OR %s ~* $work_label$%s$work_label$
        OR %s ~ $work_label$%s$work_label$
        OR %s ~ $work_label$%s$work_label$
      THEN 'unknown'
      ELSE %s
    END`,
		candidate, candidate,
		candidate, candidate, maxRunes,
		candidate,
		candidate, usageWorkLabelAllowedPattern,
		candidate, usageWorkLabelContentPattern,
		candidate, usageWorkLabelEnglishFreeTextPattern,
		candidate, usageWorkLabelChineseFreeTextPattern,
		candidate, usageWorkLabelCompleteSourceCodeMarker,
		candidate, usageWorkLabelCredentialFoldPattern,
		candidate, usageWorkLabelCredentialExactPattern,
		candidate, usageWorkLabelLongTokenPattern,
		candidate,
	)
}

func prepareUsageWork(log *service.UsageLog) usageWorkPrepared {
	prepared := usageWorkPrepared{
		requestID:            strings.TrimSpace(log.RequestID),
		apiKeyID:             log.APIKeyID,
		workRelated:          service.WorkRelatedUncertain,
		category:             service.WorkCategoryUnclassified,
		weight:               int64(log.TotalTokens()),
		confidence:           0.30,
		classificationSource: "unclassified",
		classifierVersion:    "work-content-rules-v1",
	}
	if prepared.weight < 1 {
		prepared.weight = 1
	}
	if log.WorkAttribution == nil {
		return prepared
	}

	attribution := service.NormalizeUsageWorkAttribution(*log.WorkAttribution)
	prepared.projectRef = attribution.ProjectRef
	prepared.repositoryRef = attribution.RepositoryRef
	prepared.submissionType = attribution.SubmissionType
	prepared.workRelated = attribution.WorkRelated
	prepared.category = attribution.Category
	prepared.classificationSource = attribution.ClassificationSource
	prepared.classifierVersion = attribution.ClassifierVersion
	prepared.confidence = math.Max(0, math.Min(1, attribution.Confidence))
	return prepared
}

func (r *usageLogRepository) persistUsageWorkByID(ctx context.Context, sqlq sqlExecutor, log *service.UsageLog, useSavepoint bool, recoveryOnly bool) {
	if log == nil || log.ID <= 0 {
		return
	}
	prepared := prepareUsageWork(log)
	prepared.usageLogID = log.ID
	prepared.recoveryOnly = recoveryOnly
	if useSavepoint {
		if _, err := sqlq.ExecContext(ctx, "SAVEPOINT usage_work_classification"); err != nil {
			logger.LegacyPrintf("repository.usage_log", "create structured work classification savepoint failed: %v", err)
			return
		}
	}
	err := persistUsageWorkRows(ctx, sqlq, []usageWorkPrepared{prepared})
	if err != nil && useSavepoint {
		if _, rollbackErr := sqlq.ExecContext(ctx, "ROLLBACK TO SAVEPOINT usage_work_classification"); rollbackErr != nil {
			logger.LegacyPrintf("repository.usage_log", "rollback structured work classification savepoint failed: %v", rollbackErr)
		}
	}
	if useSavepoint {
		if _, releaseErr := sqlq.ExecContext(ctx, "RELEASE SAVEPOINT usage_work_classification"); releaseErr != nil {
			logger.LegacyPrintf("repository.usage_log", "release structured work classification savepoint failed: %v", releaseErr)
		}
	}
	if err != nil {
		logger.LegacyPrintf("repository.usage_log", "persist structured work classification failed: %v", err)
	}
}

func persistUsageWorkRows(ctx context.Context, sqlq sqlExecutor, rows []usageWorkPrepared) error {
	if sqlq == nil || len(rows) == 0 {
		return nil
	}
	query, args := buildUsageWorkUpsertQuery(rows)
	_, err := sqlq.ExecContext(ctx, query, args...)
	return err
}

func buildUsageWorkUpsertQuery(rows []usageWorkPrepared) (string, []any) {
	var query strings.Builder
	query.WriteString(`
WITH input (
  usage_log_id, request_id, api_key_id, recovery_only, project_ref, repository_ref,
  submission_type, work_related, category, weight, confidence,
  classification_source, classifier_version
) AS (VALUES `)
	args := make([]any, 0, len(rows)*13)
	argPos := 1
	casts := []string{"bigint", "text", "bigint", "boolean", "text", "text", "text", "text", "text", "bigint", "numeric", "text", "text"}
	for rowIndex, row := range rows {
		if rowIndex > 0 {
			query.WriteByte(',')
		}
		query.WriteByte('(')
		values := []any{
			row.usageLogID, row.requestID, row.apiKeyID, row.recoveryOnly, row.projectRef, row.repositoryRef,
			row.submissionType, row.workRelated, row.category, row.weight, row.confidence,
			row.classificationSource, row.classifierVersion,
		}
		for i, value := range values {
			if i > 0 {
				query.WriteByte(',')
			}
			query.WriteByte('$')
			query.WriteString(strconv.Itoa(argPos))
			query.WriteString("::")
			query.WriteString(casts[i])
			args = append(args, value)
			argPos++
		}
		query.WriteByte(')')
	}
	query.WriteString(`),
resolved_key AS (
  SELECT
    COALESCE(NULLIF(i.usage_log_id, 0), matched.id) AS usage_log_id,
    i.recovery_only, i.project_ref, i.repository_ref, i.submission_type,
    i.work_related, i.category, i.weight, i.confidence,
    i.classification_source, i.classifier_version
  FROM input i
  LEFT JOIN usage_logs matched
    ON i.usage_log_id = 0
   AND i.request_id <> ''
   AND matched.request_id = i.request_id
   AND matched.api_key_id = i.api_key_id
),
resolved AS (
  SELECT rk.*, ul.user_id
  FROM resolved_key rk
  JOIN usage_logs ul ON ul.id = rk.usage_log_id
),
dimension_candidates AS (
  SELECT
    r.*,
    COALESCE(NULLIF(BTRIM(uav_department.value), ''), 'unknown') AS department_candidate,
    COALESCE(
      NULLIF(BTRIM(uav_job_role.value), ''),
      NULLIF(BTRIM(snapshot_user.role), ''),
      'unknown'
    ) AS role_candidate
  FROM resolved r
  LEFT JOIN users snapshot_user
    ON NOT r.recovery_only AND snapshot_user.id = r.user_id
  LEFT JOIN user_attribute_definitions uad_department
    ON NOT r.recovery_only
   AND uad_department.key = 'department' AND uad_department.deleted_at IS NULL
  LEFT JOIN user_attribute_values uav_department
    ON uav_department.user_id = r.user_id
   AND uav_department.attribute_id = uad_department.id
  LEFT JOIN user_attribute_definitions uad_job_role
    ON NOT r.recovery_only
   AND uad_job_role.key = 'job_role' AND uad_job_role.deleted_at IS NULL
  LEFT JOIN user_attribute_values uav_job_role
    ON uav_job_role.user_id = r.user_id
   AND uav_job_role.attribute_id = uad_job_role.id
),
dimension_snapshot AS (
  SELECT
    dc.*,
    `)
	query.WriteString(usageWorkLabelGuardSQL("dc.department_candidate", 100))
	query.WriteString(` AS department,
    `)
	query.WriteString(usageWorkLabelGuardSQL("dc.role_candidate", 50))
	query.WriteString(` AS role
  FROM dimension_candidates dc
),
metadata_upsert AS (
  INSERT INTO usage_work_metadata (
    usage_log_id, project_ref, repository_ref, submission_type,
    department, role, source, created_at, updated_at
  )
  SELECT usage_log_id, NULLIF(project_ref, ''), NULLIF(repository_ref, ''),
    NULLIF(submission_type, ''), NULLIF(department, ''), NULLIF(role, ''),
    'client_metadata', NOW(), NOW()
  FROM dimension_snapshot
  ON CONFLICT (usage_log_id) DO UPDATE SET
    project_ref = COALESCE(NULLIF(usage_work_metadata.project_ref, ''), EXCLUDED.project_ref),
    repository_ref = COALESCE(NULLIF(usage_work_metadata.repository_ref, ''), EXCLUDED.repository_ref),
    submission_type = COALESCE(NULLIF(usage_work_metadata.submission_type, ''), EXCLUDED.submission_type),
    department = COALESCE(NULLIF(usage_work_metadata.department, ''), EXCLUDED.department),
    role = COALESCE(NULLIF(usage_work_metadata.role, ''), EXCLUDED.role),
    source = COALESCE(NULLIF(usage_work_metadata.source, ''), EXCLUDED.source),
    updated_at = NOW()
  RETURNING usage_log_id
)
INSERT INTO usage_work_classifications (
  usage_log_id, user_id, work_related, category, weight, confidence,
  classification_source, classifier_version, created_at, updated_at
)
SELECT usage_log_id, user_id, work_related, category, GREATEST(weight, 1), confidence,
  classification_source, classifier_version, NOW(), NOW()
FROM dimension_snapshot
ON CONFLICT (usage_log_id) DO NOTHING`)
	return query.String(), args
}

func usageWorkRowsForBatch(keys []string, requestsByKey map[string][]usageLogCreateRequest, stateMap map[string]usageLogBatchState, insertedMap map[string]bool) []usageWorkPrepared {
	rows := make([]usageWorkPrepared, 0, len(keys))
	for _, key := range keys {
		state, ok := stateMap[key]
		requests := requestsByKey[key]
		if !ok || state.ID <= 0 || len(requests) == 0 || requests[0].log == nil {
			continue
		}
		prepared := prepareUsageWork(requests[0].log)
		prepared.usageLogID = state.ID
		prepared.recoveryOnly = !insertedMap[key]
		rows = append(rows, prepared)
	}
	return rows
}

func (p usageWorkPrepared) String() string {
	return fmt.Sprintf("usageWorkPrepared{id=%d,source=%s,category=%s}", p.usageLogID, p.classificationSource, p.category)
}
