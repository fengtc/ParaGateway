//go:build unit

package handler

import (
	"context"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync/atomic"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/config"
	middleware "github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
	"github.com/tidwall/gjson"
)

type openAICopilotProbeAccountRepo struct {
	service.AccountRepository
	account   service.Account
	listCalls atomic.Int32
}

func (r *openAICopilotProbeAccountRepo) ListSchedulableByPlatform(_ context.Context, platform string) ([]service.Account, error) {
	r.listCalls.Add(1)
	if platform != r.account.Platform {
		return nil, nil
	}
	return []service.Account{r.account}, nil
}

func (r *openAICopilotProbeAccountRepo) GetByID(_ context.Context, id int64) (*service.Account, error) {
	if id != r.account.ID {
		return nil, nil
	}
	account := r.account
	return &account, nil
}

type openAICopilotProbeUsageRepo struct {
	service.UsageLogRepository
	createCalls atomic.Int32
}

func (r *openAICopilotProbeUsageRepo) Create(_ context.Context, _ *service.UsageLog) (bool, error) {
	r.createCalls.Add(1)
	return true, nil
}

func newOpenAICopilotProbeHandler(t *testing.T) (*OpenAIGatewayHandler, *service.APIKey, *openAICopilotProbeAccountRepo, *openAICopilotProbeUsageRepo, *gatewayCopilotCompatUpstream) {
	t.Helper()

	groupID := int64(9811)
	accountRepo := &openAICopilotProbeAccountRepo{account: service.Account{
		ID:          9812,
		Name:        "copilot-probe-test",
		Platform:    service.PlatformOpenAI,
		Type:        service.AccountTypeOAuth,
		Status:      service.StatusActive,
		Schedulable: true,
		Concurrency: 1,
		Credentials: map[string]any{
			"oauth_profile": service.CopilotOAuthProfile,
			"access_token":  "copilot-probe-token",
			"base_url":      "https://api.githubcopilot.com",
		},
		AccountGroups: []service.AccountGroup{{AccountID: 9812, GroupID: groupID}},
	}}
	usageRepo := &openAICopilotProbeUsageRepo{}
	upstream := &gatewayCopilotCompatUpstream{}
	cfg := &config.Config{RunMode: config.RunModeSimple}
	cfg.Default.RateMultiplier = 1
	cfg.Security.URLAllowlist.Enabled = false

	billingCache := service.NewBillingCacheService(nil, nil, nil, nil, nil, nil, cfg, nil)
	t.Cleanup(billingCache.Stop)
	gatewayService := service.NewOpenAIGatewayService(
		accountRepo, usageRepo, nil, nil, nil, nil, nil, cfg, nil, nil,
		service.NewBillingService(cfg, nil), nil, billingCache, upstream,
		&service.DeferredService{}, nil, nil, nil, nil, nil, nil, nil,
	)
	handler := NewOpenAIGatewayHandler(
		gatewayService,
		service.NewConcurrencyService(nil),
		billingCache,
		service.NewAPIKeyService(nil, nil, nil, nil, nil, nil, cfg),
		nil, nil, nil, nil, cfg,
	)
	apiKey := &service.APIKey{
		ID:      9813,
		UserID:  9814,
		GroupID: &groupID,
		Status:  service.StatusActive,
		User:    &service.User{ID: 9814, Status: service.StatusActive, Balance: 100},
		Group: &service.Group{
			ID:                    groupID,
			Platform:              service.PlatformAnthropic,
			Status:                service.StatusActive,
			AllowMessagesDispatch: true,
		},
	}
	return handler, apiKey, accountRepo, usageRepo, upstream
}

func openAICopilotProbeContext(t *testing.T, apiKey *service.APIKey, body string, copilotOnly bool) (*gin.Context, *httptest.ResponseRecorder) {
	t.Helper()

	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	ctx := context.Background()
	if copilotOnly {
		ctx = service.WithGitHubCopilotOnly(ctx)
	}
	c.Request = httptest.NewRequest(http.MethodPost, "/copilot/v1/messages", strings.NewReader(body)).WithContext(ctx)
	c.Request.Header.Set("Content-Type", "application/json")
	c.Set(string(middleware.ContextKeyAPIKey), apiKey)
	c.Set(string(middleware.ContextKeyUser), middleware.AuthSubject{UserID: apiKey.UserID, Concurrency: 0})
	return c, recorder
}

func TestOpenAIGatewayMessages_CopilotHaikuProbeReturnsLegacyMockWithoutSideEffects(t *testing.T) {
	gin.SetMode(gin.TestMode)
	handler, apiKey, accountRepo, usageRepo, upstream := newOpenAICopilotProbeHandler(t)
	c, recorder := openAICopilotProbeContext(t, apiKey,
		`{"model":"claude-3-5-haiku-20241022","max_tokens":1,"messages":[{"role":"user","content":"ping"}],"stream":false}`,
		true,
	)

	handler.Messages(c)

	require.Equal(t, http.StatusOK, recorder.Code)
	require.Equal(t, "message", gjson.GetBytes(recorder.Body.Bytes(), "type").String())
	require.Equal(t, "claude-3-5-haiku-20241022", gjson.GetBytes(recorder.Body.Bytes(), "model").String())
	require.Equal(t, "#", gjson.GetBytes(recorder.Body.Bytes(), "content.0.text").String())
	require.Equal(t, "max_tokens", gjson.GetBytes(recorder.Body.Bytes(), "stop_reason").String())
	require.Equal(t, int64(10), gjson.GetBytes(recorder.Body.Bytes(), "usage.input_tokens").Int())
	require.Equal(t, int64(1), gjson.GetBytes(recorder.Body.Bytes(), "usage.output_tokens").Int())
	require.Zero(t, accountRepo.listCalls.Load(), "probe must not schedule an account")
	paths, _ := upstream.snapshot()
	require.Empty(t, paths, "probe must not call the Copilot upstream")
	require.Zero(t, usageRepo.createCalls.Load(), "probe must not create a usage record")
	_, selected := c.Get(opsAccountIDKey)
	require.False(t, selected, "probe must not report a selected account")
}

func TestOpenAIGatewayMessages_CopilotHaikuStreamIsNotIntercepted(t *testing.T) {
	gin.SetMode(gin.TestMode)
	handler, apiKey, accountRepo, _, upstream := newOpenAICopilotProbeHandler(t)
	c, recorder := openAICopilotProbeContext(t, apiKey,
		`{"model":"claude-3-5-haiku-20241022","max_tokens":1,"messages":[{"role":"user","content":"ping"}],"stream":true}`,
		true,
	)

	handler.Messages(c)

	require.Greater(t, accountRepo.listCalls.Load(), int32(0), "streaming probe-shaped request must enter scheduling")
	paths, _ := upstream.snapshot()
	require.Equal(t, []string{"/chat/completions"}, paths)
	require.Contains(t, recorder.Body.String(), "message_start")
	require.NotContains(t, recorder.Body.String(), `"text":"#"`)
}

func TestOpenAIGatewayMessages_OrdinaryOpenAIHaikuRequestIsNotIntercepted(t *testing.T) {
	gin.SetMode(gin.TestMode)
	groupID := int64(9821)
	apiKey := &service.APIKey{
		ID:      9822,
		UserID:  9823,
		GroupID: &groupID,
		User:    &service.User{ID: 9823},
		Group: &service.Group{
			ID:                    groupID,
			Platform:              service.PlatformOpenAI,
			AllowMessagesDispatch: true,
		},
	}
	c, recorder := openAICopilotProbeContext(t, apiKey,
		`{"model":"claude-3-5-haiku-20241022","max_tokens":1,"messages":[{"role":"user","content":"ping"}],"stream":false}`,
		false,
	)

	(&OpenAIGatewayHandler{}).Messages(c)

	require.Equal(t, http.StatusServiceUnavailable, recorder.Code)
	require.Equal(t, "api_error", gjson.GetBytes(recorder.Body.Bytes(), "error.type").String())
	require.NotEqual(t, "#", gjson.GetBytes(recorder.Body.Bytes(), "content.0.text").String())
}
