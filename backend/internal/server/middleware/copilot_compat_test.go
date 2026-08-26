//go:build unit

package middleware

import (
	"context"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/Wei-Shaw/sub2api/internal/pkg/ctxkey"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func TestForceGitHubCopilotSetsCanonicalPlatformAndStrictIdentity(t *testing.T) {
	gin.SetMode(gin.TestMode)

	router := gin.New()
	router.Use(ForceGitHubCopilot())
	router.GET("/test", func(c *gin.Context) {
		require.True(t, service.IsGitHubCopilotOnly(c.Request.Context()))
		require.Equal(t, service.PlatformOpenAI, c.Request.Context().Value(ctxkey.ForcePlatform))
		require.True(t, HasForcePlatform(c))

		platform, ok := GetForcePlatformFromContext(c)
		require.True(t, ok)
		require.Equal(t, service.PlatformOpenAI, platform)
		c.Status(http.StatusNoContent)
	})

	recorder := httptest.NewRecorder()
	request := httptest.NewRequest(http.MethodGet, "/test", nil)
	router.ServeHTTP(recorder, request)

	require.Equal(t, http.StatusNoContent, recorder.Code)
}

func TestSetGroupContextRestoresCopilotOnlyBehaviorForStandardV1Key(t *testing.T) {
	gin.SetMode(gin.TestMode)
	recorder := httptest.NewRecorder()
	c, _ := gin.CreateTestContext(recorder)
	c.Request = httptest.NewRequest(http.MethodGet, "/v1/models", nil)
	group := &service.Group{
		ID:                71,
		Hydrated:          true,
		Platform:          service.PlatformOpenAI,
		Status:            service.StatusActive,
		GitHubCopilotOnly: true,
	}

	setGroupContext(c, group)

	require.True(t, service.IsGitHubCopilotOnly(c.Request.Context()))
	require.Equal(t, service.PlatformOpenAI, c.Request.Context().Value(ctxkey.ForcePlatform))
	require.Equal(t, service.PlatformOpenAI, c.MustGet(string(ContextKeyForcePlatform)))
	contextGroup, ok := c.Request.Context().Value(ctxkey.Group).(*service.Group)
	require.True(t, ok)
	require.Same(t, group, contextGroup)
}

func TestAPIKeyAuthRestoresCopilotOnlyContextFromHydratedGroup(t *testing.T) {
	gin.SetMode(gin.TestMode)
	groupID := int64(72)
	group := &service.Group{
		ID:                groupID,
		Hydrated:          true,
		Platform:          service.PlatformOpenAI,
		Status:            service.StatusActive,
		GitHubCopilotOnly: true,
	}
	user := &service.User{
		ID:          73,
		Status:      service.StatusActive,
		Role:        service.RoleUser,
		Balance:     10,
		Concurrency: 1,
	}
	apiKey := &service.APIKey{
		ID:      74,
		Key:     "copilot-only-key",
		Status:  service.StatusActive,
		UserID:  user.ID,
		User:    user,
		GroupID: &groupID,
		Group:   group,
	}
	repo := &stubApiKeyRepo{getByKey: func(_ context.Context, key string) (*service.APIKey, error) {
		require.Equal(t, apiKey.Key, key)
		clone := *apiKey
		return &clone, nil
	}}
	cfg := &config.Config{RunMode: config.RunModeSimple}
	apiKeyService := service.NewAPIKeyService(repo, nil, nil, nil, nil, nil, cfg)

	router := gin.New()
	router.Use(gin.HandlerFunc(NewAPIKeyAuthMiddleware(apiKeyService, nil, cfg)))
	router.GET("/v1/models", func(c *gin.Context) {
		require.True(t, service.IsGitHubCopilotOnly(c.Request.Context()))
		platform, ok := GetForcePlatformFromContext(c)
		require.True(t, ok)
		require.Equal(t, service.PlatformOpenAI, platform)
		contextGroup, ok := c.Request.Context().Value(ctxkey.Group).(*service.Group)
		require.True(t, ok)
		require.True(t, contextGroup.GitHubCopilotOnly)
		c.Status(http.StatusNoContent)
	})

	recorder := httptest.NewRecorder()
	request := httptest.NewRequest(http.MethodGet, "/v1/models", nil)
	request.Header.Set("Authorization", "Bearer "+apiKey.Key)
	router.ServeHTTP(recorder, request)

	require.Equal(t, http.StatusNoContent, recorder.Code)
}
