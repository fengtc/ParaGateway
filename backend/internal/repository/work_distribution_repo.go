package repository

import (
	"context"
	"database/sql"
	"fmt"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/service"
)

type workDistributionRepository struct{ db *sql.DB }

func NewWorkDistributionRepository(db *sql.DB) service.WorkDistributionRepository {
	return &workDistributionRepository{db: db}
}

const workTotalTokensSQL = `(ul.input_tokens + ul.output_tokens + ul.cache_creation_tokens + ul.cache_read_tokens)`
const workDepartmentSQL = `COALESCE(NULLIF(BTRIM(department_value.value), ''), 'unknown')`
const workRoleSQL = `COALESCE(NULLIF(BTRIM(job_role_value.value), ''), NULLIF(BTRIM(u.role), ''), 'unknown')`

func buildWorkScopeWhere(filter service.WorkDistributionFilter) (string, []any) {
	args := []any{filter.StartTime.UTC(), filter.EndTime.UTC()}
	clauses := []string{"ul.created_at >= $1", "ul.created_at < $2"}
	if filter.UserID > 0 { args = append(args, filter.UserID); clauses = append(clauses, fmt.Sprintf("ul.user_id = $%d", len(args))) }
	if value := strings.TrimSpace(filter.Department); value != "" { args = append(args, value); clauses = append(clauses, fmt.Sprintf("LOWER(%s) = LOWER($%d)", workDepartmentSQL, len(args))) }
	if value := strings.TrimSpace(filter.Role); value != "" { args = append(args, value); clauses = append(clauses, fmt.Sprintf("LOWER(%s) = LOWER($%d)", workRoleSQL, len(args))) }
	return "WHERE " + strings.Join(clauses, " AND "), args
}

func (r *workDistributionRepository) GetAggregates(ctx context.Context, filter service.WorkDistributionFilter) ([]service.WorkDistributionAggregate, error) {
	where, args := buildWorkScopeWhere(filter)
	query := `SELECT ul.user_id, COALESCE(u.email, ''), ` + workDepartmentSQL + `, ` + workRoleSQL + `,
 COALESCE(wc.work_related, 'uncertain'), COALESCE(wc.category, 'unclassified'),
 COUNT(*)::bigint, COALESCE(SUM(COALESCE(wc.weight, ` + workTotalTokensSQL + `)), 0)::bigint,
 COALESCE(SUM(wc.confidence) FILTER (WHERE wc.confidence IS NOT NULL), 0)::float8,
 COUNT(wc.confidence)::bigint
FROM usage_logs ul
JOIN users u ON u.id = ul.user_id
LEFT JOIN user_attribute_definitions department_def ON department_def.key = 'department' AND department_def.deleted_at IS NULL
LEFT JOIN user_attribute_values department_value ON department_value.user_id = u.id AND department_value.attribute_id = department_def.id
LEFT JOIN user_attribute_definitions job_role_def ON job_role_def.key = 'job_role' AND job_role_def.deleted_at IS NULL
LEFT JOIN user_attribute_values job_role_value ON job_role_value.user_id = u.id AND job_role_value.attribute_id = job_role_def.id
LEFT JOIN usage_work_classifications wc ON wc.usage_log_id = ul.id
` + where + `
GROUP BY ul.user_id, u.email, ` + workDepartmentSQL + `, ` + workRoleSQL + `, wc.work_related, wc.category`
	rows, err := r.db.QueryContext(ctx, query, args...)
	if err != nil { return nil, fmt.Errorf("query work distribution aggregates: %w", err) }
	defer func(){ _ = rows.Close() }()
	items := make([]service.WorkDistributionAggregate, 0)
	for rows.Next() {
		var item service.WorkDistributionAggregate
		if err := rows.Scan(&item.UserID, &item.Email, &item.Department, &item.Role, &item.WorkRelated, &item.Category, &item.Requests, &item.TotalTokens, &item.ConfidenceSum, &item.ConfidenceSampleCount); err != nil { return nil, fmt.Errorf("scan work distribution aggregate: %w", err) }
		items = append(items, item)
	}
	if err := rows.Err(); err != nil { return nil, fmt.Errorf("iterate work distribution aggregates: %w", err) }
	return items, nil
}
