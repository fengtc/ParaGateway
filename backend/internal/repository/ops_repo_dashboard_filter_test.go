package repository

import (
	"strings"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestBuildUsageWhere_ModelAndAccountFiltersPreserveArgumentOrder(t *testing.T) {
	start := time.Date(2026, 8, 31, 0, 0, 0, 0, time.UTC)
	end := start.Add(time.Hour)
	groupID := int64(12)
	accountID := int64(34)
	filter := &service.OpsDashboardFilter{
		Platform:  " OpenAI ",
		GroupID:   &groupID,
		Model:     " gpt-5.6-sol ",
		AccountID: &accountID,
	}

	join, where, args, next := buildUsageWhere(filter, start, end, 1)
	require.Contains(t, join, "LEFT JOIN groups g")
	require.Contains(t, join, "LEFT JOIN accounts a")
	require.Contains(t, where, "ul.group_id = $3")
	require.Contains(t, where, "COALESCE(NULLIF(g.platform,''), a.platform) = $4")
	require.Contains(t, where, "COALESCE(NULLIF(TRIM(ul.requested_model), ''), ul.model) = $5")
	require.Contains(t, where, "ul.account_id = $6")
	require.Equal(t, []any{start, end, groupID, "openai", "gpt-5.6-sol", accountID}, args)
	require.Equal(t, 7, next)
}

func TestBuildErrorWhere_ModelAndAccountFiltersUseRequestedModelFallback(t *testing.T) {
	start := time.Date(2026, 8, 31, 0, 0, 0, 0, time.UTC)
	end := start.Add(time.Hour)
	groupID := int64(12)
	accountID := int64(34)
	filter := &service.OpsDashboardFilter{
		Platform:  " OpenAI ",
		GroupID:   &groupID,
		Model:     "gpt-5.6-sol",
		AccountID: &accountID,
	}

	where, args, next := buildErrorWhere(filter, start, end, 1)
	require.Contains(t, where, "group_id = $3")
	require.Contains(t, where, "platform = $4")
	require.Contains(t, where, "COALESCE(NULLIF(TRIM(requested_model), ''), model, '') = $5")
	require.Contains(t, where, "account_id = $6")
	require.Equal(t, []any{start, end, groupID, "openai", "gpt-5.6-sol", accountID}, args)
	require.Equal(t, 7, next)
}

func TestOpsEntityFilterSQL_UsesSharedPlaceholdersForBreakdowns(t *testing.T) {
	accountID := int64(34)
	filter := &service.OpsDashboardFilter{Model: "gpt-5.6-sol", AccountID: &accountID}
	usageSQL, errorSQL, args, next := opsEntityFilterSQL(filter, "ul.", "", 3)
	require.Contains(t, usageSQL, "COALESCE(NULLIF(TRIM(ul.requested_model), ''), ul.model) = $3")
	require.Contains(t, usageSQL, "ul.account_id = $4")
	require.Contains(t, errorSQL, "COALESCE(NULLIF(TRIM(requested_model), ''), model, '') = $3")
	require.Contains(t, errorSQL, "account_id = $4")
	require.Equal(t, []any{"gpt-5.6-sol", accountID}, args)
	require.Equal(t, 5, next)
}

func TestOpsDashboardFilterRequiresRawForModelOrAccount(t *testing.T) {
	require.False(t, (&service.OpsDashboardFilter{}).RequiresRaw())
	require.True(t, (&service.OpsDashboardFilter{Model: " gpt-5 "}).RequiresRaw())
	zero := int64(0)
	require.False(t, (&service.OpsDashboardFilter{AccountID: &zero}).RequiresRaw())
	id := int64(1)
	require.True(t, (&service.OpsDashboardFilter{AccountID: &id}).RequiresRaw())
}

func TestBuildUsageWhere_EmptyModelAndNonPositiveAccountAreIgnored(t *testing.T) {
	start := time.Now().UTC()
	end := start.Add(time.Hour)
	zero := int64(0)
	join, where, args, next := buildUsageWhere(&service.OpsDashboardFilter{Model: "  ", AccountID: &zero}, start, end, 1)
	require.Empty(t, join)
	require.NotContains(t, where, "model")
	require.NotContains(t, where, "account_id")
	require.Equal(t, []any{start, end}, args)
	require.Equal(t, 3, next)
	// Keep the assertion useful if SQL spacing is changed later.
	require.True(t, strings.HasPrefix(where, "WHERE "))
}
