package repository

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"strings"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/service"
)

type workDistributionRepository struct {
	db *sql.DB
}

func NewWorkDistributionRepository(db *sql.DB) service.WorkDistributionRepository {
	return &workDistributionRepository{db: db}
}

const workDepartmentJoin = `
LEFT JOIN usage_work_metadata wm ON wm.usage_log_id = ul.id`

const effectiveDepartmentSQL = `COALESCE(NULLIF(BTRIM(wm.department), ''), 'unknown')`
const effectiveRoleSQL = `COALESCE(NULLIF(BTRIM(wm.role), ''), 'unknown')`
const totalTokensSQL = `(ul.input_tokens + ul.output_tokens + ul.cache_creation_tokens + ul.cache_read_tokens)`

func buildWorkScopeWhere(filter service.WorkDistributionFilter) (string, []any) {
	args := []any{filter.StartTime.UTC(), filter.EndTime.UTC()}
	clauses := []string{"ul.created_at >= $1", "ul.created_at < $2"}
	if filter.UserID > 0 {
		args = append(args, filter.UserID)
		clauses = append(clauses, fmt.Sprintf("ul.user_id = $%d", len(args)))
	}
	if value := strings.TrimSpace(filter.Department); value != "" {
		args = append(args, value)
		clauses = append(clauses, fmt.Sprintf("LOWER(%s) = LOWER($%d)", effectiveDepartmentSQL, len(args)))
	}
	if value := strings.TrimSpace(filter.Role); value != "" {
		args = append(args, value)
		clauses = append(clauses, fmt.Sprintf("LOWER(%s) = LOWER($%d)", effectiveRoleSQL, len(args)))
	}
	return "WHERE " + strings.Join(clauses, " AND "), args
}

func (r *workDistributionRepository) GetAggregates(ctx context.Context, filter service.WorkDistributionFilter) ([]service.WorkDistributionAggregate, error) {
	scopeFilter := filter
	scopeFilter.UserID = 0
	where, args := buildWorkScopeWhere(scopeFilter)
	outerWhere := ""
	if filter.UserID > 0 {
		args = append(args, filter.UserID)
		outerWhere = fmt.Sprintf("WHERE b.user_id = $%d", len(args))
	}
	query := `
WITH work_scope AS (
 SELECT
  ul.user_id,
  COALESCE(u.email, '') AS email,
  ` + effectiveDepartmentSQL + ` AS department,
  ` + effectiveRoleSQL + ` AS role,
  CASE WHEN wc.usage_log_id IS NULL THEN 'uncertain' ELSE wc.work_related END AS work_related,
  CASE WHEN wc.usage_log_id IS NULL THEN 'unclassified' ELSE wc.category END AS category,
  (wc.usage_log_id IS NOT NULL AND wc.category <> 'unclassified' AND wc.classification_source <> 'unclassified') AS classified,
  COALESCE(wc.weight, ` + totalTokensSQL + `::bigint) AS workload,
  CASE
    WHEN wc.category <> 'unclassified' AND wc.classification_source <> 'unclassified' THEN wc.confidence
    ELSE NULL
  END AS confidence
 FROM usage_logs ul
 JOIN users u ON u.id = ul.user_id
 ` + workDepartmentJoin + `
 LEFT JOIN usage_work_classifications wc ON wc.usage_log_id = ul.id
 ` + where + `
), department_cohorts AS (
 SELECT department, COUNT(DISTINCT user_id)::bigint AS cohort_size
 FROM work_scope
 GROUP BY department
), scope_cohort AS (
 SELECT COUNT(DISTINCT user_id)::bigint AS cohort_size
 FROM work_scope
)
SELECT
  b.user_id, b.email, b.department, b.role, b.work_related, b.category, b.classified,
  COUNT(*) AS requests,
  COALESCE(SUM(b.workload), 0)::bigint AS total_tokens,
  COALESCE(SUM(b.confidence) FILTER (WHERE b.confidence IS NOT NULL), 0)::float8 AS confidence_sum,
  COUNT(b.confidence)::bigint AS confidence_sample_count,
  dc.cohort_size,
  sc.cohort_size
FROM work_scope b
JOIN department_cohorts dc ON dc.department = b.department
CROSS JOIN scope_cohort sc
` + outerWhere + `
GROUP BY b.user_id, b.email, b.department, b.role, b.work_related, b.category, b.classified, dc.cohort_size, sc.cohort_size`

	rows, err := r.db.QueryContext(ctx, query, args...)
	if err != nil {
		return nil, fmt.Errorf("query work distribution aggregates: %w", err)
	}
	defer func() { _ = rows.Close() }()
	result := make([]service.WorkDistributionAggregate, 0)
	for rows.Next() {
		var item service.WorkDistributionAggregate
		if err := rows.Scan(&item.UserID, &item.Email, &item.Department, &item.Role, &item.WorkRelated, &item.Category, &item.Classified, &item.Requests, &item.TotalTokens, &item.ConfidenceSum, &item.ConfidenceSampleCount, &item.DepartmentCohortSize, &item.ScopeCohortSize); err != nil {
			return nil, fmt.Errorf("scan work distribution aggregate: %w", err)
		}
		result = append(result, item)
	}
	if err := rows.Err(); err != nil {
		return nil, fmt.Errorf("iterate work distribution aggregates: %w", err)
	}
	return result, nil
}

func buildWorkRecordsQuery(filter service.WorkDistributionRecordFilter, count bool) (string, []any) {
	scopeFilter := filter.WorkDistributionFilter
	scopeFilter.UserID = 0
	scopeWhere, args := buildWorkScopeWhere(scopeFilter)
	base := `
WITH work_base AS (
  SELECT
    ul.id AS usage_log_id,
    ul.user_id,
    COALESCE(u.email, '') AS email,
    ` + effectiveDepartmentSQL + ` AS department,
    ` + effectiveRoleSQL + ` AS role,
    ` + totalTokensSQL + `::bigint AS total_tokens,
    ul.created_at,
    (wm.usage_log_id IS NOT NULL) AS metadata_exists,
    COALESCE(wm.project_ref, '') AS project_ref,
    COALESCE(wm.repository_ref, '') AS repository_ref,
    COALESCE(wm.submission_type, '') AS submission_type,
    COALESCE(wm.department, '') AS metadata_department,
    COALESCE(wm.role, '') AS metadata_role,
    COALESCE(wm.source, '') AS metadata_source,
    (wc.usage_log_id IS NOT NULL) AS classification_exists,
    CASE WHEN wc.usage_log_id IS NULL THEN 'uncertain' ELSE wc.work_related END AS work_related,
    CASE WHEN wc.usage_log_id IS NULL THEN 'unclassified' ELSE wc.category END AS category,
    COALESCE(wc.weight, 0)::bigint AS weight,
    wc.confidence,
    COALESCE(wc.classification_source, '') AS classification_source,
    COALESCE(wc.classifier_version, '') AS classifier_version,
    wc.updated_at AS classification_updated_at,
    COALESCE(latest_review.status, '') AS review_status
  FROM usage_logs ul
  JOIN users u ON u.id = ul.user_id
  ` + workDepartmentJoin + `
  LEFT JOIN usage_work_classifications wc ON wc.usage_log_id = ul.id
  LEFT JOIN LATERAL (
    SELECT wr.status
    FROM usage_work_reviews wr
    WHERE wr.usage_log_id = ul.id
    ORDER BY wr.created_at DESC, wr.id DESC
    LIMIT 1
  ) latest_review ON TRUE
  ` + scopeWhere + `
), filtered_scope AS (
  SELECT b.*
  FROM work_base b
  WHERE `
	privacyClauses := []string{"1=1"}
	if filter.Category != "" {
		args = append(args, filter.Category)
		privacyClauses = append(privacyClauses, fmt.Sprintf("b.category = $%d", len(args)))
	}
	if filter.WorkRelated != "" {
		args = append(args, filter.WorkRelated)
		privacyClauses = append(privacyClauses, fmt.Sprintf("b.work_related = $%d", len(args)))
	}
	if filter.ReviewStatus != "" {
		args = append(args, filter.ReviewStatus)
		privacyClauses = append(privacyClauses, fmt.Sprintf("b.review_status = $%d", len(args)))
	}
	base += strings.Join(privacyClauses, " AND ") + `
), department_cohorts AS (
  SELECT department, COUNT(DISTINCT user_id)::bigint AS cohort_size
  FROM filtered_scope
  GROUP BY department
), eligible_users AS (
  SELECT b.user_id
  FROM filtered_scope b
  JOIN department_cohorts dc ON dc.department = b.department
  GROUP BY b.user_id
  HAVING COUNT(*) >= $` + fmt.Sprint(len(args)+1) + `
     AND MIN(dc.cohort_size) >= $` + fmt.Sprint(len(args)+2) + `
)
`
	args = append(args, filter.MinSampleSize, filter.MinCohortSize)
	clauses := []string{"1=1"}
	if filter.UserID > 0 {
		args = append(args, filter.UserID)
		clauses = append(clauses, fmt.Sprintf("b.user_id = $%d", len(args)))
	}
	from := ` FROM filtered_scope b JOIN eligible_users eu ON eu.user_id = b.user_id WHERE ` + strings.Join(clauses, " AND ")
	if count {
		return base + `SELECT COUNT(*)` + from, args
	}
	selectSQL := `SELECT
  b.usage_log_id, b.user_id, b.email, b.department, b.role, b.total_tokens, b.created_at,
  b.metadata_exists, b.project_ref, b.repository_ref, b.submission_type,
  b.metadata_department, b.metadata_role, b.metadata_source,
  b.classification_exists, b.work_related, b.category, b.weight, b.confidence,
  b.classification_source, b.classifier_version, b.classification_updated_at, b.review_status`
	args = append(args, filter.PageSize, (filter.Page-1)*filter.PageSize)
	return base + selectSQL + from + fmt.Sprintf(" ORDER BY b.created_at DESC, b.usage_log_id DESC LIMIT $%d OFFSET $%d", len(args)-1, len(args)), args
}

func (r *workDistributionRepository) ListRecords(ctx context.Context, filter service.WorkDistributionRecordFilter) ([]service.WorkDistributionRecord, int64, error) {
	countQuery, countArgs := buildWorkRecordsQuery(filter, true)
	var total int64
	if err := r.db.QueryRowContext(ctx, countQuery, countArgs...).Scan(&total); err != nil {
		return nil, 0, fmt.Errorf("count work distribution records: %w", err)
	}
	query, args := buildWorkRecordsQuery(filter, false)
	rows, err := r.db.QueryContext(ctx, query, args...)
	if err != nil {
		return nil, 0, fmt.Errorf("list work distribution records: %w", err)
	}
	defer func() { _ = rows.Close() }()
	items := make([]service.WorkDistributionRecord, 0)
	for rows.Next() {
		item, err := scanWorkDistributionRecord(rows.Scan)
		if err != nil {
			return nil, 0, fmt.Errorf("scan work distribution record: %w", err)
		}
		items = append(items, *item)
	}
	if err := rows.Err(); err != nil {
		return nil, 0, fmt.Errorf("iterate work distribution records: %w", err)
	}
	return items, total, nil
}

func (r *workDistributionRepository) ListUserClassifications(ctx context.Context, userID int64, page, pageSize int) ([]service.WorkDistributionRecord, int64, error) {
	var total int64
	if err := r.db.QueryRowContext(ctx, `SELECT COUNT(*) FROM usage_logs WHERE user_id = $1`, userID).Scan(&total); err != nil {
		return nil, 0, fmt.Errorf("count user work classifications: %w", err)
	}
	rows, err := r.db.QueryContext(ctx, `
SELECT
  ul.id, ul.user_id, COALESCE(u.email, ''),
  `+effectiveDepartmentSQL+` AS department, `+effectiveRoleSQL+` AS role,
  `+totalTokensSQL+`::bigint AS total_tokens, ul.created_at,
  (wm.usage_log_id IS NOT NULL), COALESCE(wm.project_ref, ''), COALESCE(wm.repository_ref, ''),
  COALESCE(wm.submission_type, ''), COALESCE(wm.department, ''), COALESCE(wm.role, ''), COALESCE(wm.source, ''),
  (wc.usage_log_id IS NOT NULL),
  CASE WHEN wc.usage_log_id IS NULL THEN 'uncertain' ELSE wc.work_related END,
  CASE WHEN wc.usage_log_id IS NULL THEN 'unclassified' ELSE wc.category END,
  COALESCE(wc.weight, 0)::bigint, wc.confidence, COALESCE(wc.classification_source, ''),
  COALESCE(wc.classifier_version, ''), wc.updated_at,
  COALESCE(latest_review.status, '')
FROM usage_logs ul
JOIN users u ON u.id = ul.user_id
`+workDepartmentJoin+`
LEFT JOIN usage_work_classifications wc ON wc.usage_log_id = ul.id
LEFT JOIN LATERAL (
  SELECT wr.status FROM usage_work_reviews wr
  WHERE wr.usage_log_id = ul.id
  ORDER BY wr.created_at DESC, wr.id DESC LIMIT 1
) latest_review ON TRUE
WHERE ul.user_id = $1
ORDER BY ul.created_at DESC, ul.id DESC
LIMIT $2 OFFSET $3`, userID, pageSize, (page-1)*pageSize)
	if err != nil {
		return nil, 0, fmt.Errorf("list user work classifications: %w", err)
	}
	defer func() { _ = rows.Close() }()
	items := make([]service.WorkDistributionRecord, 0)
	for rows.Next() {
		item, err := scanWorkDistributionRecord(rows.Scan)
		if err != nil {
			return nil, 0, fmt.Errorf("scan user work classification: %w", err)
		}
		items = append(items, *item)
	}
	if err := rows.Err(); err != nil {
		return nil, 0, fmt.Errorf("iterate user work classifications: %w", err)
	}
	return items, total, nil
}

func scanWorkDistributionRecord(scan func(dest ...any) error) (*service.WorkDistributionRecord, error) {
	item := &service.WorkDistributionRecord{}
	var metadataExists, classificationExists bool
	var metadata service.WorkDistributionMetadata
	var classification service.WorkDistributionClassification
	var confidence sql.NullFloat64
	var classificationUpdatedAt sql.NullTime
	if err := scan(
		&item.UsageLogID, &item.UserID, &item.Email, &item.Department, &item.Role, &item.TotalTokens, &item.CreatedAt,
		&metadataExists, &metadata.ProjectRef, &metadata.RepositoryRef, &metadata.SubmissionType,
		&metadata.Department, &metadata.Role, &metadata.Source,
		&classificationExists, &classification.WorkRelated, &classification.Category, &classification.Weight, &confidence,
		&classification.ClassificationSource, &classification.ClassifierVersion, &classificationUpdatedAt, &item.ReviewStatus,
	); err != nil {
		return nil, err
	}
	if metadataExists {
		item.Metadata = &metadata
	}
	if classificationExists {
		if confidence.Valid {
			value := confidence.Float64
			classification.Confidence = &value
		}
		if classificationUpdatedAt.Valid {
			classification.UpdatedAt = classificationUpdatedAt.Time
		}
		item.Classification = &classification
	}
	return item, nil
}

func (r *workDistributionRepository) CreateReview(ctx context.Context, input service.CreateWorkReviewInput) (*service.WorkDistributionReview, error) {
	tx, err := r.db.BeginTx(ctx, nil)
	if err != nil {
		return nil, fmt.Errorf("begin work review: %w", err)
	}
	defer func() { _ = tx.Rollback() }()

	var userID int64
	var email string
	var previousRelated, previousCategory sql.NullString
	err = tx.QueryRowContext(ctx, `
SELECT ul.user_id, COALESCE(u.email, ''), wc.work_related, wc.category
FROM usage_logs ul
JOIN users u ON u.id = ul.user_id
LEFT JOIN usage_work_classifications wc ON wc.usage_log_id = ul.id
WHERE ul.id = $1 AND ($2::bigint = 0 OR ul.user_id = $2)`, input.UsageLogID, input.OwnerUserID).Scan(&userID, &email, &previousRelated, &previousCategory)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, service.ErrWorkUsageNotFound
	}
	if err != nil {
		return nil, fmt.Errorf("load usage for work review: %w", err)
	}

	var pending bool
	if err := tx.QueryRowContext(ctx, `SELECT EXISTS(SELECT 1 FROM usage_work_reviews WHERE usage_log_id = $1 AND status = 'pending')`, input.UsageLogID).Scan(&pending); err != nil {
		return nil, fmt.Errorf("check pending work review: %w", err)
	}
	if pending {
		return nil, service.ErrWorkReviewConflict
	}

	item := &service.WorkDistributionReview{UsageLogID: input.UsageLogID, UserID: userID, Email: email, ProposedWorkRelated: input.WorkRelated, ProposedCategory: input.Category, ReasonCode: input.ReasonCode, Status: service.WorkReviewPending}
	if previousRelated.Valid {
		value := previousRelated.String
		item.PreviousWorkRelated = &value
	}
	if previousCategory.Valid {
		value := previousCategory.String
		item.PreviousCategory = &value
	}
	requestedBy := input.RequestedBy
	item.RequestedBy = &requestedBy
	err = tx.QueryRowContext(ctx, `
INSERT INTO usage_work_reviews (
  usage_log_id, previous_work_related, previous_category,
  proposed_work_related, proposed_category, reason_code, status, requested_by
) VALUES ($1, $2, $3, $4, $5, $6, 'pending', $7)
RETURNING id, created_at`, input.UsageLogID, nullableWorkStringPtr(item.PreviousWorkRelated), nullableWorkStringPtr(item.PreviousCategory), input.WorkRelated, input.Category, input.ReasonCode, input.RequestedBy).Scan(&item.ID, &item.CreatedAt)
	if err != nil {
		if strings.Contains(err.Error(), "idx_usage_work_reviews_one_pending") {
			return nil, service.ErrWorkReviewConflict
		}
		return nil, fmt.Errorf("insert work review: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return nil, fmt.Errorf("commit work review: %w", err)
	}
	return item, nil
}

func (r *workDistributionRepository) ListReviews(ctx context.Context, filter service.WorkDistributionReviewFilter) ([]service.WorkDistributionReview, int64, error) {
	args := make([]any, 0, 4)
	clauses := []string{"1=1"}
	if filter.Status != "" {
		args = append(args, filter.Status)
		clauses = append(clauses, fmt.Sprintf("r.status = $%d", len(args)))
	}
	if filter.UserID > 0 {
		args = append(args, filter.UserID)
		clauses = append(clauses, fmt.Sprintf("ul.user_id = $%d", len(args)))
	}
	where := "WHERE " + strings.Join(clauses, " AND ")
	var total int64
	if err := r.db.QueryRowContext(ctx, `SELECT COUNT(*) FROM usage_work_reviews r JOIN usage_logs ul ON ul.id = r.usage_log_id `+where, args...).Scan(&total); err != nil {
		return nil, 0, fmt.Errorf("count work reviews: %w", err)
	}
	args = append(args, filter.PageSize, (filter.Page-1)*filter.PageSize)
	rows, err := r.db.QueryContext(ctx, `
SELECT r.id, r.usage_log_id, ul.user_id, COALESCE(u.email, ''),
  r.previous_work_related, r.previous_category, r.proposed_work_related, r.proposed_category,
  r.reason_code, r.status, COALESCE(r.resolution_note, ''), r.requested_by, r.resolved_by,
  r.created_at, r.resolved_at
FROM usage_work_reviews r
JOIN usage_logs ul ON ul.id = r.usage_log_id
JOIN users u ON u.id = ul.user_id
`+where+fmt.Sprintf(" ORDER BY r.created_at DESC, r.id DESC LIMIT $%d OFFSET $%d", len(args)-1, len(args)), args...)
	if err != nil {
		return nil, 0, fmt.Errorf("list work reviews: %w", err)
	}
	defer func() { _ = rows.Close() }()
	items := make([]service.WorkDistributionReview, 0)
	for rows.Next() {
		item, err := scanWorkReview(rows.Scan)
		if err != nil {
			return nil, 0, fmt.Errorf("scan work review: %w", err)
		}
		items = append(items, *item)
	}
	if err := rows.Err(); err != nil {
		return nil, 0, fmt.Errorf("iterate work reviews: %w", err)
	}
	return items, total, nil
}

func (r *workDistributionRepository) ResolveReview(ctx context.Context, input service.ResolveWorkReviewInput) (*service.WorkDistributionReview, error) {
	tx, err := r.db.BeginTx(ctx, nil)
	if err != nil {
		return nil, fmt.Errorf("begin resolve work review: %w", err)
	}
	defer func() { _ = tx.Rollback() }()
	item, err := scanWorkReview(tx.QueryRowContext(ctx, `
SELECT r.id, r.usage_log_id, ul.user_id, COALESCE(u.email, ''),
  r.previous_work_related, r.previous_category, r.proposed_work_related, r.proposed_category,
  r.reason_code, r.status, COALESCE(r.resolution_note, ''), r.requested_by, r.resolved_by,
  r.created_at, r.resolved_at
FROM usage_work_reviews r
JOIN usage_logs ul ON ul.id = r.usage_log_id
JOIN users u ON u.id = ul.user_id
WHERE r.id = $1
FOR UPDATE OF r`, input.ReviewID).Scan)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, service.ErrWorkReviewNotFound
	}
	if err != nil {
		return nil, fmt.Errorf("load work review: %w", err)
	}
	if item.Status != service.WorkReviewPending {
		return nil, service.ErrWorkReviewResolved
	}

	if input.Decision == service.WorkReviewApproved {
		_, err = tx.ExecContext(ctx, `
INSERT INTO usage_work_classifications (
  usage_log_id, user_id, work_related, category, weight, confidence,
  classification_source, classifier_version, created_at, updated_at
)
SELECT ul.id, ul.user_id, $2, $3,
  GREATEST((ul.input_tokens + ul.output_tokens + ul.cache_creation_tokens + ul.cache_read_tokens)::bigint, 1),
  1, 'manual_review', 'manual-review-v1', NOW(), NOW()
FROM usage_logs ul WHERE ul.id = $1
ON CONFLICT (usage_log_id) DO UPDATE SET
  user_id = EXCLUDED.user_id,
  work_related = EXCLUDED.work_related,
  category = EXCLUDED.category,
  weight = EXCLUDED.weight,
  confidence = EXCLUDED.confidence,
  classification_source = EXCLUDED.classification_source,
  classifier_version = EXCLUDED.classifier_version,
  updated_at = NOW()`, item.UsageLogID, item.ProposedWorkRelated, item.ProposedCategory)
		if err != nil {
			return nil, fmt.Errorf("apply approved work classification: %w", err)
		}
	}
	resolvedAt := time.Now().UTC()
	result, err := tx.ExecContext(ctx, `
UPDATE usage_work_reviews
SET status = $2, resolution_note = $3, resolved_by = $4, resolved_at = $5
WHERE id = $1 AND status = 'pending'`, input.ReviewID, input.Decision, input.ResolutionNote, input.ResolvedBy, resolvedAt)
	if err != nil {
		return nil, fmt.Errorf("resolve work review: %w", err)
	}
	if affected, _ := result.RowsAffected(); affected != 1 {
		return nil, service.ErrWorkReviewResolved
	}
	item.Status = input.Decision
	item.ResolutionNote = input.ResolutionNote
	resolvedBy := input.ResolvedBy
	item.ResolvedBy = &resolvedBy
	item.ResolvedAt = &resolvedAt
	if err := tx.Commit(); err != nil {
		return nil, fmt.Errorf("commit resolved work review: %w", err)
	}
	return item, nil
}

func scanWorkReview(scan func(dest ...any) error) (*service.WorkDistributionReview, error) {
	item := &service.WorkDistributionReview{}
	var previousRelated, previousCategory sql.NullString
	var requestedBy, resolvedBy sql.NullInt64
	var resolvedAt sql.NullTime
	if err := scan(
		&item.ID, &item.UsageLogID, &item.UserID, &item.Email,
		&previousRelated, &previousCategory, &item.ProposedWorkRelated, &item.ProposedCategory,
		&item.ReasonCode, &item.Status, &item.ResolutionNote, &requestedBy, &resolvedBy,
		&item.CreatedAt, &resolvedAt,
	); err != nil {
		return nil, err
	}
	if previousRelated.Valid {
		value := previousRelated.String
		item.PreviousWorkRelated = &value
	}
	if previousCategory.Valid {
		value := previousCategory.String
		item.PreviousCategory = &value
	}
	if requestedBy.Valid {
		value := requestedBy.Int64
		item.RequestedBy = &value
	}
	if resolvedBy.Valid {
		value := resolvedBy.Int64
		item.ResolvedBy = &value
	}
	if resolvedAt.Valid {
		value := resolvedAt.Time
		item.ResolvedAt = &value
	}
	return item, nil
}

func nullableWorkStringPtr(value *string) any {
	if value == nil {
		return nil
	}
	return *value
}
