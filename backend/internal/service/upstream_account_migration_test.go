package service

import (
	"os"
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestUpstreamAccountMigrationCreatesIndependentTableWithoutMutatingAccounts(t *testing.T) {
	source, err := os.ReadFile("../../migrations/223_upstream_accounts.sql")
	require.NoError(t, err)
	text := strings.ToLower(string(source))

	require.Contains(t, text, "create table if not exists upstream_accounts")
	require.Contains(t, text, "credential_ciphertext")
	require.Contains(t, text, "circuit_breaker_threshold")
	require.NotContains(t, text, "alter table accounts")
	require.NotContains(t, text, "references accounts")
	require.NotContains(t, text, "account_id bigint")
}
