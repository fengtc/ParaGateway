package repository

import (
	"context"
	"math"
	"strconv"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/pkg/logger"
	"github.com/Wei-Shaw/sub2api/internal/service"
)

type usageWorkPrepared struct {
	usageLogID int64
	requestID string
	apiKeyID int64
	workRelated string
	category string
	weight int64
	confidence float64
	classificationSource string
	classifierVersion string
}

func prepareUsageWork(log *service.UsageLog) usageWorkPrepared {
	row := usageWorkPrepared{requestID: strings.TrimSpace(log.RequestID), apiKeyID: log.APIKeyID,
		workRelated: service.WorkRelatedUncertain, category: service.WorkCategoryUnclassified,
		weight: int64(log.TotalTokens()), confidence: 0.30,
		classificationSource: "unclassified", classifierVersion: "work-content-rules-v1"}
	if row.weight < 1 { row.weight = 1 }
	if log.WorkAttribution == nil { return row }
	value := service.NormalizeUsageWorkAttribution(*log.WorkAttribution)
	row.workRelated, row.category = value.WorkRelated, value.Category
	row.classificationSource, row.classifierVersion = value.ClassificationSource, value.ClassifierVersion
	row.confidence = math.Max(0, math.Min(1, value.Confidence))
	return row
}

func (r *usageLogRepository) persistUsageWorkByID(ctx context.Context, sqlq sqlExecutor, log *service.UsageLog, useSavepoint bool, _ bool) {
	if log == nil || log.ID <= 0 { return }
	row := prepareUsageWork(log); row.usageLogID = log.ID
	if useSavepoint { if _, err := sqlq.ExecContext(ctx, "SAVEPOINT usage_work_classification"); err != nil { return } }
	err := persistUsageWorkRows(ctx, sqlq, []usageWorkPrepared{row})
	if err != nil && useSavepoint { _, _ = sqlq.ExecContext(ctx, "ROLLBACK TO SAVEPOINT usage_work_classification") }
	if useSavepoint { _, _ = sqlq.ExecContext(ctx, "RELEASE SAVEPOINT usage_work_classification") }
	if err != nil { logger.LegacyPrintf("repository.usage_log", "persist work classification failed: %v", err) }
}

func persistUsageWorkRows(ctx context.Context, sqlq sqlExecutor, rows []usageWorkPrepared) error {
	if sqlq == nil || len(rows) == 0 { return nil }
	query, args := buildUsageWorkUpsertQuery(rows)
	_, err := sqlq.ExecContext(ctx, query, args...)
	return err
}

func buildUsageWorkUpsertQuery(rows []usageWorkPrepared) (string, []any) {
	var query strings.Builder
	query.WriteString(`WITH input (usage_log_id, request_id, api_key_id, work_related, category, weight, confidence, classification_source, classifier_version) AS (VALUES `)
	args := make([]any, 0, len(rows)*9)
	casts := []string{"bigint", "text", "bigint", "text", "text", "bigint", "numeric", "text", "text"}
	position := 1
	for rowIndex, row := range rows {
		if rowIndex > 0 { query.WriteByte(',') }
		query.WriteByte('(')
		values := []any{row.usageLogID, row.requestID, row.apiKeyID, row.workRelated, row.category, row.weight, row.confidence, row.classificationSource, row.classifierVersion}
		for index, value := range values {
			if index > 0 { query.WriteByte(',') }
			query.WriteString("$"); query.WriteString(strconv.Itoa(position)); query.WriteString("::"); query.WriteString(casts[index])
			args = append(args, value); position++
		}
		query.WriteByte(')')
	}
	query.WriteString(`), resolved AS (
 SELECT COALESCE(NULLIF(i.usage_log_id, 0), matched.id) AS usage_log_id,
        i.work_related, i.category, i.weight, i.confidence, i.classification_source, i.classifier_version
 FROM input i
 LEFT JOIN usage_logs matched ON i.usage_log_id = 0 AND i.request_id <> '' AND matched.request_id = i.request_id AND matched.api_key_id = i.api_key_id
)
INSERT INTO usage_work_classifications (usage_log_id, user_id, work_related, category, weight, confidence, classification_source, classifier_version, created_at)
SELECT r.usage_log_id, ul.user_id, r.work_related, r.category, GREATEST(r.weight, 1), r.confidence, r.classification_source, r.classifier_version, NOW()
FROM resolved r JOIN usage_logs ul ON ul.id = r.usage_log_id
ON CONFLICT (usage_log_id) DO NOTHING`)
	return query.String(), args
}

func usageWorkRowsForBatch(keys []string, requestsByKey map[string][]usageLogCreateRequest, stateMap map[string]usageLogBatchState, _ map[string]bool) []usageWorkPrepared {
	rows := make([]usageWorkPrepared, 0, len(keys))
	for _, key := range keys {
		state, ok := stateMap[key]; requests := requestsByKey[key]
		if !ok || state.ID <= 0 || len(requests) == 0 || requests[0].log == nil { continue }
		row := prepareUsageWork(requests[0].log); row.usageLogID = state.ID; rows = append(rows, row)
	}
	return rows
}
