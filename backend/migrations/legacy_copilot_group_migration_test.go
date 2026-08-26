package migrations

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestLegacyCopilotGroupMigrationPreservesBindingsAndCanonicalIdentity(t *testing.T) {
	content, err := FS.ReadFile("235_normalize_legacy_copilot_groups.sql")
	require.NoError(t, err)

	sql := strings.Join(strings.Fields(string(content)), " ")
	require.Contains(t, sql, "migration 235 blocked: invalid legacy Copilot account IDs=")
	require.Contains(t, sql, "legacy Copilot groups contain non-Copilot members")
	require.Contains(t, sql, "Copilot accounts have incompatible group bindings")
	require.Contains(t, sql, "CREATE TABLE IF NOT EXISTS legacy_copilot_groups_backup_235 (")
	require.Contains(t, sql, "CREATE TABLE IF NOT EXISTS legacy_copilot_accounts_backup_235 (")
	require.Contains(t, sql, "ALTER TABLE groups ADD COLUMN IF NOT EXISTS github_copilot_only BOOLEAN NOT NULL DEFAULT FALSE")
	require.Contains(t, sql, "ON CONFLICT (id) DO UPDATE")
	require.Contains(t, sql, "backed_up_at = NOW()")
	require.Contains(t, sql, "UPDATE accounts SET platform = 'openai', type = 'oauth'")
	require.Contains(t, sql, "'oauth_profile', 'github_copilot'")
	require.Contains(t, sql, "SELECT id, name, platform, allow_messages_dispatch, allow_live, require_oauth_only, updated_at FROM groups WHERE platform = 'copilot' ON CONFLICT (id) DO UPDATE")
	require.Contains(t, sql, "UPDATE groups SET platform = 'openai', allow_messages_dispatch = TRUE, github_copilot_only = TRUE, allow_live = FALSE, require_oauth_only = TRUE, updated_at = NOW() WHERE platform = 'copilot'")
	require.NotContains(t, strings.ToUpper(sql), "DELETE FROM ACCOUNT_GROUPS")
	require.NotContains(t, strings.ToUpper(sql), "UPDATE ACCOUNT_GROUPS")
}

func TestLegacyCopilotRollbackIsNotAutomaticallyEmbedded(t *testing.T) {
	entries, err := FS.ReadDir(".")
	require.NoError(t, err)
	for _, entry := range entries {
		require.NotEqual(t, "235_restore_legacy_copilot_groups.sql", entry.Name())
	}
}

func TestLegacyCopilotRollbackRejectsUnbackedMarkedGroups(t *testing.T) {
	content, err := os.ReadFile(filepath.Join("rollback", "235_restore_legacy_copilot_groups.sql"))
	require.NoError(t, err)

	sql := strings.Join(strings.Fields(string(content)), " ")
	require.True(t, strings.HasPrefix(strings.TrimSpace(string(content)), "-- Explicit operator rollback"))
	require.Contains(t, sql, "BEGIN;")
	require.Contains(t, sql, "WHERE g.github_copilot_only = TRUE")
	require.Contains(t, sql, "legacy_copilot_groups_backup_235")
	require.Contains(t, sql, "allow_live = backup.allow_live")
	require.Contains(t, sql, "require_oauth_only = backup.require_oauth_only")
	require.Contains(t, sql, "rollback 235 blocked: Copilot-only group IDs without legacy backup=")
	require.Contains(t, sql, "ALTER TABLE groups DROP COLUMN IF EXISTS github_copilot_only")
	require.Contains(t, sql, "DELETE FROM schema_migrations WHERE filename = '235_normalize_legacy_copilot_groups.sql'")
	require.True(t, strings.HasSuffix(strings.TrimSpace(string(content)), "COMMIT;"))
}
