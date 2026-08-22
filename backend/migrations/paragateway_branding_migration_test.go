package migrations

import (
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestParaGatewayBrandingMigration(t *testing.T) {
	content, err := FS.ReadFile("228_normalize_paragateway_branding.sql")
	require.NoError(t, err)

	sql := strings.Join(strings.Fields(string(content)), " ")
	require.Contains(t, sql, "UPDATE settings")
	require.Contains(t, sql, "regexp_replace")
	require.Contains(t, sql, "'Sub2API', 'ParaGateway', 'gi'")
	require.Contains(t, sql, "'Para[[:space:]]+AI[[:space:]]+Coding[[:space:]]+Gateway'")
	for _, key := range []string{
		"site_name",
		"site_subtitle",
		"contact_info",
		"home_content",
		"login_agreement_documents",
		"smtp_from_name",
	} {
		require.Contains(t, sql, "'"+key+"'")
	}
}
