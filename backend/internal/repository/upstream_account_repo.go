package repository

import (
	"context"
	"database/sql"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/service"
)

type upstreamAccountRepository struct {
	db *sql.DB
}

func NewUpstreamAccountRepository(db *sql.DB) service.UpstreamAccountRepository {
	return &upstreamAccountRepository{db: db}
}

const upstreamAccountSelectSQL = `
	SELECT id, name, provider_type, base_url, auth_type,
	       credential_ciphertext, credential_hint,
	       oauth_profile, oauth_account_id, oauth_email, oauth_expires_at,
	       wif_subject_token_url, wif_client_id, wif_client_auth_method,
	       wif_audience, wif_scope, wif_identity_provider_id,
	       wif_service_account_id, wif_federation_rule_id,
	       wif_organization_id, wif_workspace_id,
	       is_active, priority, weight, max_concurrency, rpm_limit,
	       circuit_breaker_threshold, circuit_breaker_cooldown_seconds,
	       quota_status, quota_utilization, quota_resets_at, quota_checked_at,
	       quota_five_hour_utilization, quota_five_hour_resets_at,
	       quota_seven_day_utilization, quota_seven_day_resets_at,
	       quota_seven_day_sonnet_utilization, quota_seven_day_sonnet_resets_at,
	       cooldown_until, cooldown_reason, last_upstream_status,
	       last_success_at, last_failure_at, created_at, updated_at, deleted_at
	FROM upstream_accounts`

func (r *upstreamAccountRepository) Create(ctx context.Context, account *service.UpstreamAccount) error {
	_, err := r.db.ExecContext(ctx, `
		INSERT INTO upstream_accounts (
			id, name, provider_type, base_url, auth_type,
			credential_ciphertext, credential_hint,
			wif_subject_token_url, wif_client_id, wif_client_auth_method,
			wif_audience, wif_scope, wif_identity_provider_id,
			wif_service_account_id, wif_federation_rule_id,
			wif_organization_id, wif_workspace_id,
			is_active, priority, weight, max_concurrency, rpm_limit,
			circuit_breaker_threshold, circuit_breaker_cooldown_seconds,
			quota_status, created_at, updated_at
		) VALUES (
			$1, $2, $3, $4, $5,
			$6, $7,
			$8, $9, $10,
			$11, $12, $13,
			$14, $15,
			$16, $17,
			$18, $19, $20, $21, $22,
			$23, $24,
			$25, $26, $27
		)`,
		account.ID, account.Name, account.ProviderType, account.BaseURL, account.AuthType,
		account.CredentialCiphertext, account.CredentialHint,
		account.WIFSubjectTokenURL, account.WIFClientID, account.WIFClientAuthMethod,
		account.WIFAudience, account.WIFScope, account.WIFIdentityProviderID,
		account.WIFServiceAccountID, account.WIFFederationRuleID,
		account.WIFOrganizationID, account.WIFWorkspaceID,
		account.IsActive, account.Priority, account.Weight, account.MaxConcurrency, account.RPMLimit,
		account.CircuitBreakerThreshold, account.CircuitBreakerCooldownSeconds,
		account.QuotaStatus, account.CreatedAt, account.UpdatedAt,
	)
	return err
}

func (r *upstreamAccountRepository) GetByID(ctx context.Context, id string) (*service.UpstreamAccount, error) {
	row := r.db.QueryRowContext(ctx, upstreamAccountSelectSQL+` WHERE id = $1 AND deleted_at IS NULL`, id)
	account, err := scanUpstreamAccount(row)
	if err == sql.ErrNoRows {
		return nil, service.ErrUpstreamAccountNotFound
	}
	return account, err
}

func (r *upstreamAccountRepository) List(ctx context.Context) ([]service.UpstreamAccount, error) {
	rows, err := r.db.QueryContext(ctx, upstreamAccountSelectSQL+` WHERE deleted_at IS NULL ORDER BY created_at, id`)
	if err != nil {
		return nil, err
	}
	defer func() { _ = rows.Close() }()
	accounts := make([]service.UpstreamAccount, 0)
	for rows.Next() {
		account, scanErr := scanUpstreamAccount(rows)
		if scanErr != nil {
			return nil, scanErr
		}
		accounts = append(accounts, *account)
	}
	return accounts, rows.Err()
}

func (r *upstreamAccountRepository) Update(ctx context.Context, account *service.UpstreamAccount) error {
	result, err := r.db.ExecContext(ctx, `
		UPDATE upstream_accounts SET
			name = $2, provider_type = $3, base_url = $4, auth_type = $5,
			credential_ciphertext = $6, credential_hint = $7,
			wif_subject_token_url = $8, wif_client_id = $9, wif_client_auth_method = $10,
			wif_audience = $11, wif_scope = $12, wif_identity_provider_id = $13,
			wif_service_account_id = $14, wif_federation_rule_id = $15,
			wif_organization_id = $16, wif_workspace_id = $17,
			is_active = $18, priority = $19, weight = $20,
			max_concurrency = $21, rpm_limit = $22,
			circuit_breaker_threshold = $23,
			circuit_breaker_cooldown_seconds = $24,
			updated_at = $25
		WHERE id = $1 AND deleted_at IS NULL`,
		account.ID, account.Name, account.ProviderType, account.BaseURL, account.AuthType,
		account.CredentialCiphertext, account.CredentialHint,
		account.WIFSubjectTokenURL, account.WIFClientID, account.WIFClientAuthMethod,
		account.WIFAudience, account.WIFScope, account.WIFIdentityProviderID,
		account.WIFServiceAccountID, account.WIFFederationRuleID,
		account.WIFOrganizationID, account.WIFWorkspaceID,
		account.IsActive, account.Priority, account.Weight,
		account.MaxConcurrency, account.RPMLimit,
		account.CircuitBreakerThreshold, account.CircuitBreakerCooldownSeconds,
		account.UpdatedAt,
	)
	if err != nil {
		return err
	}
	return requireUpstreamAccountAffected(result)
}

func (r *upstreamAccountRepository) SoftDelete(ctx context.Context, id string, deletedAt time.Time) error {
	result, err := r.db.ExecContext(ctx, `
		UPDATE upstream_accounts
		SET is_active = false, deleted_at = $2, updated_at = $2
		WHERE id = $1 AND deleted_at IS NULL`, id, deletedAt)
	if err != nil {
		return err
	}
	return requireUpstreamAccountAffected(result)
}

type upstreamAccountScanner interface {
	Scan(dest ...any) error
}

func scanUpstreamAccount(scanner upstreamAccountScanner) (*service.UpstreamAccount, error) {
	account := &service.UpstreamAccount{}
	var oauthProfile, oauthAccountID, oauthEmail sql.NullString
	var oauthExpiresAt sql.NullTime
	var wifSubjectTokenURL, wifClientID, wifClientAuthMethod sql.NullString
	var wifAudience, wifScope, wifIdentityProviderID sql.NullString
	var wifServiceAccountID, wifFederationRuleID, wifOrganizationID, wifWorkspaceID sql.NullString
	var quotaUtilization, quotaFiveHour, quotaSevenDay, quotaSevenDaySonnet sql.NullFloat64
	var quotaResetsAt, quotaCheckedAt, quotaFiveHourResetsAt, quotaSevenDayResetsAt sql.NullTime
	var quotaSevenDaySonnetResetsAt, cooldownUntil, lastSuccessAt, lastFailureAt, deletedAt sql.NullTime
	var cooldownReason sql.NullString
	var lastUpstreamStatus sql.NullInt64
	err := scanner.Scan(
		&account.ID, &account.Name, &account.ProviderType, &account.BaseURL, &account.AuthType,
		&account.CredentialCiphertext, &account.CredentialHint,
		&oauthProfile, &oauthAccountID, &oauthEmail, &oauthExpiresAt,
		&wifSubjectTokenURL, &wifClientID, &wifClientAuthMethod,
		&wifAudience, &wifScope, &wifIdentityProviderID,
		&wifServiceAccountID, &wifFederationRuleID,
		&wifOrganizationID, &wifWorkspaceID,
		&account.IsActive, &account.Priority, &account.Weight, &account.MaxConcurrency, &account.RPMLimit,
		&account.CircuitBreakerThreshold, &account.CircuitBreakerCooldownSeconds,
		&account.QuotaStatus, &quotaUtilization, &quotaResetsAt, &quotaCheckedAt,
		&quotaFiveHour, &quotaFiveHourResetsAt,
		&quotaSevenDay, &quotaSevenDayResetsAt,
		&quotaSevenDaySonnet, &quotaSevenDaySonnetResetsAt,
		&cooldownUntil, &cooldownReason, &lastUpstreamStatus,
		&lastSuccessAt, &lastFailureAt, &account.CreatedAt, &account.UpdatedAt, &deletedAt,
	)
	if err != nil {
		return nil, err
	}
	account.OAuthProfile = nullableString(oauthProfile)
	account.OAuthAccountID = nullableString(oauthAccountID)
	account.OAuthEmail = nullableString(oauthEmail)
	account.OAuthExpiresAt = nullableTime(oauthExpiresAt)
	account.WIFSubjectTokenURL = nullableString(wifSubjectTokenURL)
	account.WIFClientID = nullableString(wifClientID)
	account.WIFClientAuthMethod = nullableString(wifClientAuthMethod)
	account.WIFAudience = nullableString(wifAudience)
	account.WIFScope = nullableString(wifScope)
	account.WIFIdentityProviderID = nullableString(wifIdentityProviderID)
	account.WIFServiceAccountID = nullableString(wifServiceAccountID)
	account.WIFFederationRuleID = nullableString(wifFederationRuleID)
	account.WIFOrganizationID = nullableString(wifOrganizationID)
	account.WIFWorkspaceID = nullableString(wifWorkspaceID)
	account.QuotaUtilization = nullableFloat(quotaUtilization)
	account.QuotaResetsAt = nullableTime(quotaResetsAt)
	account.QuotaCheckedAt = nullableTime(quotaCheckedAt)
	account.QuotaFiveHourUtilization = nullableFloat(quotaFiveHour)
	account.QuotaFiveHourResetsAt = nullableTime(quotaFiveHourResetsAt)
	account.QuotaSevenDayUtilization = nullableFloat(quotaSevenDay)
	account.QuotaSevenDayResetsAt = nullableTime(quotaSevenDayResetsAt)
	account.QuotaSevenDaySonnetUtilization = nullableFloat(quotaSevenDaySonnet)
	account.QuotaSevenDaySonnetResetsAt = nullableTime(quotaSevenDaySonnetResetsAt)
	account.CooldownUntil = nullableTime(cooldownUntil)
	account.CooldownReason = nullableString(cooldownReason)
	if lastUpstreamStatus.Valid {
		value := int(lastUpstreamStatus.Int64)
		account.LastUpstreamStatus = &value
	}
	account.LastSuccessAt = nullableTime(lastSuccessAt)
	account.LastFailureAt = nullableTime(lastFailureAt)
	account.DeletedAt = nullableTime(deletedAt)
	return account, nil
}

func requireUpstreamAccountAffected(result sql.Result) error {
	affected, err := result.RowsAffected()
	if err != nil {
		return err
	}
	if affected == 0 {
		return service.ErrUpstreamAccountNotFound
	}
	return nil
}

func nullableString(value sql.NullString) *string {
	if !value.Valid {
		return nil
	}
	return &value.String
}

func nullableTime(value sql.NullTime) *time.Time {
	if !value.Valid {
		return nil
	}
	return &value.Time
}

func nullableFloat(value sql.NullFloat64) *float64 {
	if !value.Valid {
		return nil
	}
	return &value.Float64
}
