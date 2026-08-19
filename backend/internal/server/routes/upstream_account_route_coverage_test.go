package routes

import (
	"os"
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestIndependentUpstreamAccountRoutesDoNotAliasOfficialAccounts(t *testing.T) {
	source, err := os.ReadFile("admin.go")
	require.NoError(t, err)
	text := string(source)

	require.Contains(t, text, `admin.Group("/accounts")`)
	require.Contains(t, text, `admin.Group("/upstream-accounts")`)
	require.Contains(t, text, "h.Admin.Account.List")
	require.Contains(t, text, "h.Admin.UpstreamAccount.List")
	require.Contains(t, text, "h.Admin.UpstreamAccount.TestDraft")
	require.Contains(t, text, "h.Admin.UpstreamAccount.TestSaved")
	require.Equal(t, 1, strings.Count(text, `admin.Group("/upstream-accounts")`))
}
