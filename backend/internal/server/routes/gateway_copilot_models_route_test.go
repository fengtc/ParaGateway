//go:build unit

package routes

import (
	"context"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/Wei-Shaw/sub2api/internal/handler"
	servermiddleware "github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
	"github.com/tidwall/gjson"
)

type copilotModelsRouteAPIKeyRepo struct {
	service.APIKeyRepository
	apiKey *service.APIKey
}

func (r *copilotModelsRouteAPIKeyRepo) GetByKeyForAuth(_ context.Context, key string) (*service.APIKey, error) {
	if r.apiKey == nil || key != r.apiKey.Key {
		return nil, service.ErrAPIKeyNotFound
	}
	clone := *r.apiKey
	return &clone, nil
}

func (r *copilotModelsRouteAPIKeyRepo) UpdateLastUsed(context.Context, int64, time.Time) error {
	return nil
}

type copilotModelsRouteAccountRepo struct {
	service.AccountRepository
	groupID  int64
	accounts []service.Account
}

func (r *copilotModelsRouteAccountRepo) ListSchedulableByGroupIDAndPlatform(_ context.Context, groupID int64, platform string) ([]service.Account, error) {
	if groupID != r.groupID || platform != service.PlatformOpenAI {
		return nil, nil
	}
	accounts := make([]service.Account, len(r.accounts))
	copy(accounts, r.accounts)
	return accounts, nil
}

type copilotModelsRouteUpstream struct {
	service.HTTPUpstream
	request *http.Request
}

func (u *copilotModelsRouteUpstream) Do(req *http.Request, _ string, _ int64, _ int) (*http.Response, error) {
	u.request = req.Clone(req.Context())
	u.request.Header = req.Header.Clone()
	return &http.Response{
		StatusCode: http.StatusOK,
		Header:     http.Header{"Content-Type": []string{"application/json"}},
		Body: io.NopCloser(strings.NewReader(`{"object":"list","data":[` +
			`{"id":"live-copilot-model","object":"model"},` +
			`{"id":"claude-sonnet-4.5","object":"model"}]}`)),
	}, nil
}

func TestGatewayRoutesV1ModelsUsesLiveCopilotCatalogForCopilotOnlyAPIKeyGroup(t *testing.T) {
	gin.SetMode(gin.TestMode)

	groupID := int64(7301)
	group := &service.Group{
		ID:                groupID,
		Name:              "copilot-only",
		Platform:          service.PlatformOpenAI,
		Status:            service.StatusActive,
		Hydrated:          true,
		GitHubCopilotOnly: true,
	}
	user := &service.User{
		ID:          7302,
		Role:        service.RoleUser,
		Status:      service.StatusActive,
		Balance:     10,
		Concurrency: 1,
	}
	apiKey := &service.APIKey{
		ID:      7303,
		UserID:  user.ID,
		Key:     "copilot-models-route-key",
		Status:  service.StatusActive,
		GroupID: &groupID,
		Group:   group,
		User:    user,
	}
	apiKeyRepo := &copilotModelsRouteAPIKeyRepo{apiKey: apiKey}
	cfg := &config.Config{
		RunMode: config.RunModeStandard,
		Gateway: config.GatewayConfig{
			MaxBodySize:     1024 * 1024,
			TextMaxBodySize: 1024 * 1024,
		},
		Security: config.SecurityConfig{
			URLAllowlist: config.URLAllowlistConfig{Enabled: false},
		},
	}
	apiKeyService := service.NewAPIKeyService(
		apiKeyRepo, nil, nil, nil, nil, nil, cfg,
	)

	accountRepo := &copilotModelsRouteAccountRepo{
		groupID: groupID,
		accounts: []service.Account{
			{
				ID:          7304,
				Name:        "ordinary-openai",
				Platform:    service.PlatformOpenAI,
				Type:        service.AccountTypeOAuth,
				Status:      service.StatusActive,
				Schedulable: true,
				Concurrency: 1,
				Priority:    0,
				Credentials: map[string]any{"access_token": "ordinary-token"},
			},
			{
				ID:          7305,
				Name:        "github-copilot",
				Platform:    service.PlatformOpenAI,
				Type:        service.AccountTypeOAuth,
				Status:      service.StatusActive,
				Schedulable: true,
				Concurrency: 1,
				Priority:    10,
				Credentials: map[string]any{
					"oauth_profile": service.CopilotOAuthProfile,
					"access_token":  "copilot-route-token",
					"base_url":      service.CopilotAPIBaseURL,
				},
			},
		},
	}
	upstream := &copilotModelsRouteUpstream{}
	openAIService := service.NewOpenAIGatewayService(
		accountRepo, nil, nil, nil, nil, nil, nil, cfg, nil, nil, nil,
		nil, nil, upstream, nil, nil, nil, nil, nil, nil, nil, nil,
	)
	openAIHandler := handler.NewOpenAIGatewayHandler(
		openAIService, nil, nil, apiKeyService, nil, nil, nil, nil, cfg,
	)

	router := gin.New()
	RegisterGatewayRoutes(
		router,
		&handler.Handlers{
			Gateway:       &handler.GatewayHandler{},
			OpenAIGateway: openAIHandler,
			AsyncImage:    handler.NewAsyncImageHandler(nil, nil),
		},
		servermiddleware.NewAPIKeyAuthMiddleware(apiKeyService, nil, cfg),
		apiKeyService,
		nil,
		nil,
		nil,
		nil,
		cfg,
	)

	req := httptest.NewRequest(http.MethodGet, "/v1/models", nil)
	req.Header.Set("Authorization", "Bearer "+apiKey.Key)
	recorder := httptest.NewRecorder()
	router.ServeHTTP(recorder, req)

	require.Equal(t, http.StatusOK, recorder.Code)
	require.Equal(t, "live-copilot-model", gjson.Get(recorder.Body.String(), "data.0.id").String())
	require.Equal(t, "claude-sonnet-4-5", gjson.Get(recorder.Body.String(), "data.1.id").String())
	require.NotContains(t, recorder.Body.String(), "gpt-5.6-sol")
	require.NotNil(t, upstream.request)
	require.Equal(t, http.MethodGet, upstream.request.Method)
	require.Equal(t, "/models", upstream.request.URL.Path)
	require.Equal(t, "Bearer copilot-route-token", upstream.request.Header.Get("Authorization"))
}
