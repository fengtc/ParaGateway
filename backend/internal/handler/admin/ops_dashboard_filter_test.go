package admin

import (
	"net/http/httptest"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func TestParseOpsDashboardFilter_ParsesModelAndAccount(t *testing.T) {
	gin.SetMode(gin.TestMode)
	c, _ := gin.CreateTestContext(httptest.NewRecorder())
	c.Request = httptest.NewRequest("GET", "/?start_time=2026-08-31T00:00:00Z&end_time=2026-08-31T01:00:00Z&platform=%20OpenAI%20&group_id=12&model=%20gpt-5.6-sol%20&account_id=34&mode=preagg", nil)

	filter, err := parseOpsDashboardFilter(c, "1h")
	require.NoError(t, err)
	require.Equal(t, "OpenAI", filter.Platform)
	require.Equal(t, "gpt-5.6-sol", filter.Model)
	require.Equal(t, service.OpsQueryModePreagg, filter.QueryMode)
	require.NotNil(t, filter.GroupID)
	require.Equal(t, int64(12), *filter.GroupID)
	require.NotNil(t, filter.AccountID)
	require.Equal(t, int64(34), *filter.AccountID)
}

func TestParseOpsDashboardFilter_RejectsInvalidAccountID(t *testing.T) {
	gin.SetMode(gin.TestMode)
	c, _ := gin.CreateTestContext(httptest.NewRecorder())
	c.Request = httptest.NewRequest("GET", "/?account_id=0", nil)

	_, err := parseOpsDashboardFilter(c, "1h")
	require.EqualError(t, err, "Invalid account_id")
}

func TestParseOpsEntityDimensions_ParsesRealtimeModelAndAccount(t *testing.T) {
	gin.SetMode(gin.TestMode)
	c, _ := gin.CreateTestContext(httptest.NewRecorder())
	c.Request = httptest.NewRequest("GET", "/?platform=%20openai%20&group_id=12&model=%20gpt-5.6-sol%20&account_id=34", nil)

	platform, model, groupID, accountID, err := parseOpsEntityDimensions(c)
	require.NoError(t, err)
	require.Equal(t, "openai", platform)
	require.Equal(t, "gpt-5.6-sol", model)
	require.NotNil(t, groupID)
	require.Equal(t, int64(12), *groupID)
	require.NotNil(t, accountID)
	require.Equal(t, int64(34), *accountID)
}

func TestParseOpsEntityDimensions_RejectsInvalidRealtimeAccountID(t *testing.T) {
	gin.SetMode(gin.TestMode)
	c, _ := gin.CreateTestContext(httptest.NewRecorder())
	c.Request = httptest.NewRequest("GET", "/?account_id=0", nil)

	_, _, _, _, err := parseOpsEntityDimensions(c)
	require.EqualError(t, err, "Invalid account_id")
}

func TestOpsDashboardSnapshotCacheKeyIncludesModelAndAccount(t *testing.T) {
	accountA, accountB := int64(10), int64(11)
	base := opsDashboardSnapshotV2CacheKey{StartTime: "s", EndTime: "e", Platform: "openai", QueryMode: service.OpsQueryModeRaw, BucketSecond: 60}
	a := base
	a.Model = "gpt-5.6-sol"
	a.AccountID = &accountA
	b := base
	b.Model = "gpt-5.6-sol"
	b.AccountID = &accountB
	require.NotEqual(t, a, b)
}
