package migrations

import (
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestLegacyCopilotAccountMigration(t *testing.T) {
	content, err := FS.ReadFile("233_migrate_legacy_copilot_accounts.sql")
	require.NoError(t, err)

	sql := strings.Join(strings.Fields(string(content)), " ")
	require.Contains(t, sql, "UPDATE accounts")
	require.Contains(t, sql, "platform = 'openai'")
	require.Contains(t, sql, "type = 'oauth'")
	require.Contains(t, sql, "'oauth_profile', 'github_copilot'")
	require.Contains(t, sql, "'github_access_token', credentials -> 'github_token'")
	require.Contains(t, sql, "credentials - 'github_token'")
	require.Contains(t, sql, "WHERE platform = 'copilot'")
	require.Contains(t, sql, "AND type = 'apikey'")
	require.Contains(t, sql, "AND NOT (credentials ? 'github_access_token')")

	for _, preservedKey := range []string{"billing_pat", "billing_username", "base_url", "model_mapping"} {
		require.NotContains(t, sql, "credentials - '"+preservedKey+"'")
	}
}
