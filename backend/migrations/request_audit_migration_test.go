package migrations

import (
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestRequestAuditMigrationIsDisabledAndPreviewOnlyByDefault(t *testing.T) {
	content, err := FS.ReadFile("234_request_audit.sql")
	require.NoError(t, err)
	normalized := strings.Join(strings.Fields(string(content)), " ")

	for _, required := range []string{
		"CREATE TABLE IF NOT EXISTS request_audit_policies",
		"CREATE TABLE IF NOT EXISTS request_audit_records",
		"enabled BOOLEAN NOT NULL DEFAULT FALSE",
		"store_encrypted_content BOOLEAN NOT NULL DEFAULT FALSE",
		"capture_mode IN ('all', 'errors', 'sample')",
		"retention_days BETWEEN 1 AND 3650",
		"max_body_bytes BETWEEN 4096 AND 4194304",
		"request_body_ciphertext TEXT NOT NULL DEFAULT ''",
		"response_body_ciphertext TEXT NOT NULL DEFAULT ''",
		"idx_request_audit_records_expires",
	} {
		require.Contains(t, normalized, required)
	}
	require.NotContains(t, normalized, "request_body_plaintext")
	require.NotContains(t, normalized, "response_body_plaintext")
}
