//go:build integration

package repository

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	dbmigrations "github.com/Wei-Shaw/sub2api/migrations"
	"github.com/stretchr/testify/require"
)

func readLegacyCopilotMigrationSQL(t *testing.T) (string, string) {
	t.Helper()
	migrationSQL, err := dbmigrations.FS.ReadFile("235_normalize_legacy_copilot_groups.sql")
	require.NoError(t, err)
	rollbackPath := filepath.Join("..", "..", "migrations", "rollback", "235_restore_legacy_copilot_groups.sql")
	rollbackSQL, err := os.ReadFile(rollbackPath)
	require.NoError(t, err)
	return string(migrationSQL), string(rollbackSQL)
}

func rollback235BodyForTestTransaction(t *testing.T, rollbackSQL string) string {
	t.Helper()
	trimmed := strings.TrimSpace(rollbackSQL)
	require.True(t, strings.HasPrefix(trimmed, "-- Explicit operator rollback"))
	begin := strings.Index(trimmed, "BEGIN;")
	require.NotEqual(t, -1, begin)
	trimmed = strings.TrimSpace(trimmed[begin+len("BEGIN;"):])
	require.True(t, strings.HasSuffix(trimmed, "COMMIT;"))
	return strings.TrimSpace(strings.TrimSuffix(trimmed, "COMMIT;"))
}

func TestMigration235PreservesBindingsIsIdempotentAndRollsBack(t *testing.T) {
	tx := testTx(t)
	ctx := context.Background()
	migrationSQL, rollbackSQL := readLegacyCopilotMigrationSQL(t)

	originalGroupUpdatedAt := time.Date(2025, 1, 2, 3, 4, 5, 0, time.UTC)
	originalAccountUpdatedAt := time.Date(2025, 2, 3, 4, 5, 6, 0, time.UTC)
	var groupID int64
	require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO groups (
    name,
    platform,
    allow_messages_dispatch,
    allow_live,
    require_oauth_only,
    updated_at
)
VALUES ('migration-235-copilot', 'copilot', FALSE, TRUE, FALSE, $1)
RETURNING id
`, originalGroupUpdatedAt).Scan(&groupID))

	legacyCredentials := `{
  "github_token":"  github-long-lived  ",
  "github_access_token":"github-long-lived",
  "billing_pat":"billing-secret",
  "billing_username":"octocat",
  "custom_setting":{"preserve":true}
}`
	var legacyAccountID int64
	require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO accounts (name, platform, type, credentials, updated_at)
VALUES ('migration-235-legacy-account', 'copilot', 'apikey', $1::jsonb, $2)
RETURNING id
`, legacyCredentials, originalAccountUpdatedAt).Scan(&legacyAccountID))

	var canonicalAccountID int64
	require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO accounts (name, platform, type, credentials)
VALUES (
    'migration-235-canonical-account',
    'openai',
    'oauth',
    '{"oauth_profile":"github_copilot","github_access_token":"canonical-token"}'::jsonb
)
RETURNING id
`).Scan(&canonicalAccountID))

	_, err := tx.ExecContext(ctx, `
INSERT INTO account_groups (account_id, group_id, priority)
VALUES ($1, $3, 7), ($2, $3, 11)
`, legacyAccountID, canonicalAccountID, groupID)
	require.NoError(t, err)

	var userID int64
	require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO users (email, password_hash)
VALUES ('migration-235@example.test', 'not-a-real-hash')
RETURNING id
`).Scan(&userID))
	var apiKeyID int64
	require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO api_keys (user_id, key, name, group_id)
VALUES ($1, 'sk-migration-235', 'migration-235-key', $2)
RETURNING id
`, userID, groupID).Scan(&apiKeyID))

	_, err = tx.ExecContext(ctx, migrationSQL)
	require.NoError(t, err)

	var groupPlatform string
	var allowMessages bool
	var allowLive bool
	var requireOAuthOnly bool
	var githubCopilotOnly bool
	var migratedGroupUpdatedAt time.Time
	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT platform, allow_messages_dispatch, allow_live, require_oauth_only, github_copilot_only, updated_at
FROM groups
WHERE id = $1
`, groupID).Scan(&groupPlatform, &allowMessages, &allowLive, &requireOAuthOnly, &githubCopilotOnly, &migratedGroupUpdatedAt))
	require.Equal(t, "openai", groupPlatform)
	require.True(t, allowMessages)
	require.False(t, allowLive)
	require.True(t, requireOAuthOnly)
	require.True(t, githubCopilotOnly)

	var accountPlatform, accountType, migratedCredentials string
	var migratedAccountUpdatedAt time.Time
	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT platform, type, credentials::text, updated_at
FROM accounts
WHERE id = $1
`, legacyAccountID).Scan(&accountPlatform, &accountType, &migratedCredentials, &migratedAccountUpdatedAt))
	require.Equal(t, "openai", accountPlatform)
	require.Equal(t, "oauth", accountType)
	require.JSONEq(t, `{
  "oauth_profile":"github_copilot",
  "github_access_token":"github-long-lived",
  "billing_pat":"billing-secret",
  "billing_username":"octocat",
  "custom_setting":{"preserve":true}
}`, migratedCredentials)

	var legacyPriority, canonicalPriority int
	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT priority FROM account_groups WHERE account_id = $1 AND group_id = $2
`, legacyAccountID, groupID).Scan(&legacyPriority))
	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT priority FROM account_groups WHERE account_id = $1 AND group_id = $2
`, canonicalAccountID, groupID).Scan(&canonicalPriority))
	require.Equal(t, 7, legacyPriority)
	require.Equal(t, 11, canonicalPriority)

	var apiKeyGroupID int64
	require.NoError(t, tx.QueryRowContext(ctx, `SELECT group_id FROM api_keys WHERE id = $1`, apiKeyID).Scan(&apiKeyGroupID))
	require.Equal(t, groupID, apiKeyGroupID)

	var groupBackupCount, accountBackupCount int
	require.NoError(t, tx.QueryRowContext(ctx, `SELECT COUNT(*) FROM legacy_copilot_groups_backup_235 WHERE id = $1`, groupID).Scan(&groupBackupCount))
	require.NoError(t, tx.QueryRowContext(ctx, `SELECT COUNT(*) FROM legacy_copilot_accounts_backup_235 WHERE id = $1`, legacyAccountID).Scan(&accountBackupCount))
	require.Equal(t, 1, groupBackupCount)
	require.Equal(t, 1, accountBackupCount)

	_, err = tx.ExecContext(ctx, migrationSQL)
	require.NoError(t, err)

	var secondGroupUpdatedAt, secondAccountUpdatedAt time.Time
	require.NoError(t, tx.QueryRowContext(ctx, `SELECT updated_at FROM groups WHERE id = $1`, groupID).Scan(&secondGroupUpdatedAt))
	require.NoError(t, tx.QueryRowContext(ctx, `SELECT updated_at FROM accounts WHERE id = $1`, legacyAccountID).Scan(&secondAccountUpdatedAt))
	require.Equal(t, migratedGroupUpdatedAt, secondGroupUpdatedAt)
	require.Equal(t, migratedAccountUpdatedAt, secondAccountUpdatedAt)

	_, err = tx.ExecContext(ctx, rollback235BodyForTestTransaction(t, rollbackSQL))
	require.NoError(t, err)

	var restoredCredentials string
	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT platform, type, credentials::text, updated_at
FROM accounts
WHERE id = $1
`, legacyAccountID).Scan(&accountPlatform, &accountType, &restoredCredentials, &migratedAccountUpdatedAt))
	require.Equal(t, "copilot", accountPlatform)
	require.Equal(t, "apikey", accountType)
	require.JSONEq(t, legacyCredentials, restoredCredentials)
	require.Equal(t, originalAccountUpdatedAt, migratedAccountUpdatedAt)

	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT platform, allow_messages_dispatch, allow_live, require_oauth_only, updated_at
FROM groups
WHERE id = $1
`, groupID).Scan(&groupPlatform, &allowMessages, &allowLive, &requireOAuthOnly, &migratedGroupUpdatedAt))
	require.Equal(t, "copilot", groupPlatform)
	require.False(t, allowMessages)
	require.True(t, allowLive)
	require.False(t, requireOAuthOnly)
	require.Equal(t, originalGroupUpdatedAt, migratedGroupUpdatedAt)

	require.NoError(t, tx.QueryRowContext(ctx, `SELECT priority FROM account_groups WHERE account_id = $1 AND group_id = $2`, legacyAccountID, groupID).Scan(&legacyPriority))
	require.Equal(t, 7, legacyPriority)
	require.NoError(t, tx.QueryRowContext(ctx, `SELECT group_id FROM api_keys WHERE id = $1`, apiKeyID).Scan(&apiKeyGroupID))
	require.Equal(t, groupID, apiKeyGroupID)

	var migrationRecordCount int
	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT COUNT(*) FROM schema_migrations
WHERE filename = '235_normalize_legacy_copilot_groups.sql'
`).Scan(&migrationRecordCount))
	require.Zero(t, migrationRecordCount)

	// A rollback followed by another upgrade must refresh the backup instead of
	// restoring the stale state captured before the first upgrade.
	reappliedGroupUpdatedAt := time.Date(2026, 3, 4, 5, 6, 7, 0, time.UTC)
	reappliedAccountUpdatedAt := time.Date(2026, 4, 5, 6, 7, 8, 0, time.UTC)
	_, err = tx.ExecContext(ctx, `
UPDATE groups
SET
    allow_messages_dispatch = TRUE,
    allow_live = TRUE,
    require_oauth_only = TRUE,
    updated_at = $2
WHERE id = $1
`, groupID, reappliedGroupUpdatedAt)
	require.NoError(t, err)
	_, err = tx.ExecContext(ctx, `
UPDATE accounts
SET credentials = '{"github_token":"second-token"}'::jsonb, updated_at = $2
WHERE id = $1
`, legacyAccountID, reappliedAccountUpdatedAt)
	require.NoError(t, err)

	_, err = tx.ExecContext(ctx, migrationSQL)
	require.NoError(t, err)
	_, err = tx.ExecContext(ctx, rollback235BodyForTestTransaction(t, rollbackSQL))
	require.NoError(t, err)

	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT allow_messages_dispatch, allow_live, require_oauth_only, updated_at
FROM groups
WHERE id = $1
`, groupID).Scan(&allowMessages, &allowLive, &requireOAuthOnly, &migratedGroupUpdatedAt))
	require.True(t, allowMessages)
	require.True(t, allowLive)
	require.True(t, requireOAuthOnly)
	require.Equal(t, reappliedGroupUpdatedAt, migratedGroupUpdatedAt)
	require.NoError(t, tx.QueryRowContext(ctx, `
SELECT credentials::text, updated_at FROM accounts WHERE id = $1
`, legacyAccountID).Scan(&restoredCredentials, &migratedAccountUpdatedAt))
	require.JSONEq(t, `{"github_token":"second-token"}`, restoredCredentials)
	require.Equal(t, reappliedAccountUpdatedAt, migratedAccountUpdatedAt)
}

func TestMigration235RollbackRejectsUnbackedCopilotOnlyGroup(t *testing.T) {
	tx := testTx(t)
	ctx := context.Background()
	migrationSQL, rollbackSQL := readLegacyCopilotMigrationSQL(t)

	_, err := tx.ExecContext(ctx, migrationSQL)
	require.NoError(t, err)
	_, err = tx.ExecContext(ctx, `
INSERT INTO groups (
    name,
    platform,
    github_copilot_only,
    allow_messages_dispatch,
    require_oauth_only
)
VALUES ('migration-235-unbacked-copilot', 'openai', TRUE, TRUE, TRUE)
`)
	require.NoError(t, err)

	_, err = tx.ExecContext(ctx, rollback235BodyForTestTransaction(t, rollbackSQL))
	require.ErrorContains(t, err, "rollback 235 blocked: Copilot-only group IDs without legacy backup=")
}

func TestMigration235PreflightRejectsUnsafeData(t *testing.T) {
	migrationSQL, _ := readLegacyCopilotMigrationSQL(t)

	t.Run("missing token", func(t *testing.T) {
		tx := testTx(t)
		ctx := context.Background()
		_, err := tx.ExecContext(ctx, `
INSERT INTO accounts (name, platform, type, credentials)
VALUES ('migration-235-missing-token', 'copilot', 'apikey', '{}'::jsonb)
`)
		require.NoError(t, err)
		_, err = tx.ExecContext(ctx, migrationSQL)
		require.ErrorContains(t, err, "invalid legacy Copilot account IDs=")
	})

	t.Run("conflicting tokens", func(t *testing.T) {
		tx := testTx(t)
		ctx := context.Background()
		_, err := tx.ExecContext(ctx, `
INSERT INTO accounts (name, platform, type, credentials)
VALUES (
    'migration-235-conflicting-token',
    'copilot',
    'apikey',
    '{"github_token":"token-a","github_access_token":"token-b"}'::jsonb
)
`)
		require.NoError(t, err)
		_, err = tx.ExecContext(ctx, migrationSQL)
		require.ErrorContains(t, err, "invalid legacy Copilot account IDs=")
		require.NotContains(t, err.Error(), "token-a")
		require.NotContains(t, err.Error(), "token-b")
	})

	t.Run("ordinary account in legacy group", func(t *testing.T) {
		tx := testTx(t)
		ctx := context.Background()
		var groupID, accountID int64
		require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO groups (name, platform)
VALUES ('migration-235-mixed-group', 'copilot')
RETURNING id
`).Scan(&groupID))
		require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO accounts (name, platform, type, credentials)
VALUES ('migration-235-ordinary-account', 'openai', 'apikey', '{"api_key":"ordinary"}'::jsonb)
RETURNING id
`).Scan(&accountID))
		_, err := tx.ExecContext(ctx, `INSERT INTO account_groups (account_id, group_id) VALUES ($1, $2)`, accountID, groupID)
		require.NoError(t, err)
		_, err = tx.ExecContext(ctx, migrationSQL)
		require.ErrorContains(t, err, "legacy Copilot groups contain non-Copilot members")
		require.Contains(t, err.Error(), fmt.Sprintf("group=%d/account=%d", groupID, accountID))
	})

	t.Run("Copilot account in incompatible group", func(t *testing.T) {
		tx := testTx(t)
		ctx := context.Background()
		var groupID, accountID int64
		require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO groups (name, platform)
VALUES ('migration-235-gemini-group', 'gemini')
RETURNING id
`).Scan(&groupID))
		require.NoError(t, tx.QueryRowContext(ctx, `
INSERT INTO accounts (name, platform, type, credentials)
VALUES ('migration-235-incompatible-account', 'copilot', 'apikey', '{"github_token":"legacy-token"}'::jsonb)
RETURNING id
`).Scan(&accountID))
		_, err := tx.ExecContext(ctx, `INSERT INTO account_groups (account_id, group_id) VALUES ($1, $2)`, accountID, groupID)
		require.NoError(t, err)
		_, err = tx.ExecContext(ctx, migrationSQL)
		require.ErrorContains(t, err, "Copilot accounts have incompatible group bindings")
		require.Contains(t, err.Error(), fmt.Sprintf("group=%d/account=%d", groupID, accountID))
	})
}
