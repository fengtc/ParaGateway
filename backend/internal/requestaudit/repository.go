package requestaudit

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"strings"
	"time"
)

type Repository struct{ db *sql.DB }

func NewRepository(db *sql.DB) *Repository { return &Repository{db: db} }

func (r *Repository) LoadPolicy(ctx context.Context) (*Policy, error) {
	return scanPolicy(r.db.QueryRowContext(ctx, `
		SELECT id, enabled, capture_mode, sample_rate, retention_days,
		       capture_request_body, capture_response_body, store_encrypted_content,
		       redaction_level, max_body_bytes, version, updated_by, updated_at
		FROM request_audit_policies WHERE id=1`))
}

func (r *Repository) UpdatePolicy(ctx context.Context, req UpdatePolicyRequest, adminID int64) (*Policy, error) {
	return scanPolicy(r.db.QueryRowContext(ctx, `
		UPDATE request_audit_policies SET
			enabled=$1, capture_mode=$2, sample_rate=$3, retention_days=$4,
			capture_request_body=$5, capture_response_body=$6,
			store_encrypted_content=$7, redaction_level=$8, max_body_bytes=$9,
			version=version+1, updated_by=$10, updated_at=NOW()
		WHERE id=1 AND version=$11
		RETURNING id, enabled, capture_mode, sample_rate, retention_days,
		          capture_request_body, capture_response_body, store_encrypted_content,
		          redaction_level, max_body_bytes, version, updated_by, updated_at`,
		req.Enabled, req.CaptureMode, req.SampleRate, req.RetentionDays,
		req.CaptureRequestBody, req.CaptureResponseBody, req.StoreEncryptedContent,
		req.RedactionLevel, req.MaxBodyBytes, nullablePositiveID(adminID), req.ExpectedVersion))
}

func scanPolicy(row interface{ Scan(...any) error }) (*Policy, error) {
	var item Policy
	var updatedBy sql.NullInt64
	err := row.Scan(&item.ID, &item.Enabled, &item.CaptureMode, &item.SampleRate,
		&item.RetentionDays, &item.CaptureRequestBody, &item.CaptureResponseBody,
		&item.StoreEncryptedContent, &item.RedactionLevel, &item.MaxBodyBytes,
		&item.Version, &updatedBy, &item.UpdatedAt)
	if err != nil {
		return nil, err
	}
	if updatedBy.Valid {
		item.UpdatedBy = &updatedBy.Int64
	}
	return &item, nil
}

func (r *Repository) Insert(ctx context.Context, item *Record) error {
	if item == nil {
		return errors.New("request audit record is nil")
	}
	return r.db.QueryRowContext(ctx, `
		INSERT INTO request_audit_records (
			request_id, user_id, username_snapshot, user_email_snapshot,
			api_key_id, api_key_name_snapshot, group_id, group_name_snapshot,
			method, endpoint, model, client_ip, status_code, latency_ms,
			is_stream, capture_reason, policy_version,
			request_content_type, response_content_type, request_preview, response_preview,
			request_body_ciphertext, response_body_ciphertext, encryption_version,
			request_bytes, response_bytes, request_truncated, response_truncated,
			request_body_omitted, response_body_omitted, content_error, expires_at, created_at
		) VALUES (
			$1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,
			$18,$19,$20,$21,$22,$23,$24,$25,$26,$27,$28,$29,$30,$31,$32,$33
		) RETURNING id`,
		item.RequestID, item.UserID, item.UsernameSnapshot, item.UserEmailSnapshot,
		item.APIKeyID, item.APIKeyNameSnapshot, item.GroupID, item.GroupNameSnapshot,
		item.Method, item.Endpoint, item.Model, item.ClientIP, item.StatusCode, item.LatencyMS,
		item.IsStream, item.CaptureReason, item.PolicyVersion,
		item.RequestContentType, item.ResponseContentType, item.RequestPreview, item.ResponsePreview,
		item.RequestBodyCiphertext, item.ResponseBodyCiphertext, item.EncryptionVersion,
		item.RequestBytes, item.ResponseBytes, item.RequestTruncated, item.ResponseTruncated,
		item.RequestBodyOmitted, item.ResponseBodyOmitted, item.ContentError, item.ExpiresAt, item.CreatedAt,
	).Scan(&item.ID)
}

func (r *Repository) List(ctx context.Context, filter Filter, page, pageSize int) (*Page, error) {
	if page < 1 {
		page = 1
	}
	if pageSize < 1 {
		pageSize = 20
	}
	if pageSize > 100 {
		pageSize = 100
	}
	where, args := buildWhere(filter)
	var total int64
	if err := r.db.QueryRowContext(ctx, `SELECT COUNT(*) FROM request_audit_records r`+where, args...).Scan(&total); err != nil {
		return nil, err
	}
	queryArgs := append([]any(nil), args...)
	limitPos := len(queryArgs) + 1
	queryArgs = append(queryArgs, pageSize, (page-1)*pageSize)
	rows, err := r.db.QueryContext(ctx, `SELECT `+recordListColumns+`
		FROM request_audit_records r`+where+
		fmt.Sprintf(` ORDER BY r.created_at DESC, r.id DESC LIMIT $%d OFFSET $%d`, limitPos, limitPos+1), queryArgs...)
	if err != nil {
		return nil, err
	}
	defer func() { _ = rows.Close() }()
	items := make([]*Record, 0, pageSize)
	for rows.Next() {
		item, scanErr := scanRecordList(rows)
		if scanErr != nil {
			return nil, scanErr
		}
		items = append(items, item)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	pages := 0
	if total > 0 {
		pages = int((total + int64(pageSize) - 1) / int64(pageSize))
	}
	return &Page{Items: items, Total: total, Page: page, PageSize: pageSize, Pages: pages}, nil
}

func (r *Repository) Get(ctx context.Context, id int64) (*Record, error) {
	item, err := scanRecordDetail(r.db.QueryRowContext(ctx, `SELECT `+recordDetailColumns+`
		FROM request_audit_records r WHERE r.id=$1`, id))
	if errors.Is(err, sql.ErrNoRows) {
		return nil, sql.ErrNoRows
	}
	return item, err
}

func (r *Repository) DeleteExpired(ctx context.Context, now time.Time, limit int) (int64, error) {
	if limit <= 0 || limit > 10000 {
		limit = 1000
	}
	result, err := r.db.ExecContext(ctx, `
		WITH selected AS (
			SELECT id FROM request_audit_records
			WHERE expires_at <= $1 ORDER BY expires_at, id LIMIT $2
		)
		DELETE FROM request_audit_records r USING selected s WHERE r.id=s.id`, now, limit)
	if err != nil {
		return 0, err
	}
	return result.RowsAffected()
}

func buildWhere(filter Filter) (string, []any) {
	clauses := make([]string, 0, 9)
	args := make([]any, 0, 9)
	add := func(expression string, value any) {
		args = append(args, value)
		clauses = append(clauses, fmt.Sprintf(expression, len(args)))
	}
	if filter.UserID != nil {
		add("r.user_id=$%d", *filter.UserID)
	}
	if filter.APIKeyID != nil {
		add("r.api_key_id=$%d", *filter.APIKeyID)
	}
	if filter.GroupID != nil {
		add("r.group_id=$%d", *filter.GroupID)
	}
	if filter.StatusCode != nil {
		add("r.status_code=$%d", *filter.StatusCode)
	}
	if filter.RequestID != "" {
		add("r.request_id=$%d", filter.RequestID)
	}
	if filter.Model != "" {
		add("r.model ILIKE $%d", "%"+filter.Model+"%")
	}
	if filter.StartAt != nil {
		add("r.created_at >= $%d", *filter.StartAt)
	}
	if filter.EndAt != nil {
		add("r.created_at <= $%d", *filter.EndAt)
	}
	if filter.Query != "" {
		args = append(args, "%"+filter.Query+"%")
		pos := len(args)
		clauses = append(clauses, fmt.Sprintf(`(
			r.username_snapshot ILIKE $%d OR r.user_email_snapshot ILIKE $%d OR
			r.api_key_name_snapshot ILIKE $%d OR r.group_name_snapshot ILIKE $%d OR
			r.endpoint ILIKE $%d OR r.model ILIKE $%d OR r.request_preview ILIKE $%d OR
			r.response_preview ILIKE $%d)`, pos, pos, pos, pos, pos, pos, pos, pos))
	}
	if len(clauses) == 0 {
		return "", args
	}
	return " WHERE " + strings.Join(clauses, " AND "), args
}

const recordListColumns = `
	r.id, r.request_id, r.user_id, r.username_snapshot, r.user_email_snapshot,
	r.api_key_id, r.api_key_name_snapshot, r.group_id, r.group_name_snapshot,
	r.method, r.endpoint, r.model, r.client_ip, r.status_code, r.latency_ms,
	r.is_stream, r.capture_reason, r.policy_version,
	r.request_content_type, r.response_content_type, r.request_preview, r.response_preview,
	r.encryption_version, r.request_bytes, r.response_bytes,
	r.request_truncated, r.response_truncated, r.request_body_omitted, r.response_body_omitted,
	r.content_error, r.expires_at, r.created_at,
	(r.request_body_ciphertext <> '' OR r.response_body_ciphertext <> '')`

const recordDetailColumns = recordListColumns + `,
	r.request_body_ciphertext, r.response_body_ciphertext`

type scanner interface{ Scan(...any) error }

func scanRecordList(row scanner) (*Record, error) {
	return scanRecord(row, false)
}

func scanRecordDetail(row scanner) (*Record, error) {
	return scanRecord(row, true)
}

func scanRecord(row scanner, detail bool) (*Record, error) {
	var item Record
	var userID, apiKeyID, groupID sql.NullInt64
	values := []any{
		&item.ID, &item.RequestID, &userID, &item.UsernameSnapshot, &item.UserEmailSnapshot,
		&apiKeyID, &item.APIKeyNameSnapshot, &groupID, &item.GroupNameSnapshot,
		&item.Method, &item.Endpoint, &item.Model, &item.ClientIP, &item.StatusCode, &item.LatencyMS,
		&item.IsStream, &item.CaptureReason, &item.PolicyVersion,
		&item.RequestContentType, &item.ResponseContentType, &item.RequestPreview, &item.ResponsePreview,
		&item.EncryptionVersion, &item.RequestBytes, &item.ResponseBytes,
		&item.RequestTruncated, &item.ResponseTruncated, &item.RequestBodyOmitted, &item.ResponseBodyOmitted,
		&item.ContentError, &item.ExpiresAt, &item.CreatedAt, &item.RawContentAvailable,
	}
	if detail {
		values = append(values, &item.RequestBodyCiphertext, &item.ResponseBodyCiphertext)
	}
	if err := row.Scan(values...); err != nil {
		return nil, err
	}
	if userID.Valid {
		item.UserID = &userID.Int64
	}
	if apiKeyID.Valid {
		item.APIKeyID = &apiKeyID.Int64
	}
	if groupID.Valid {
		item.GroupID = &groupID.Int64
	}
	return &item, nil
}

func nullablePositiveID(id int64) any {
	if id <= 0 {
		return nil
	}
	return id
}
