package routes

import (
	"net/http"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/handler"
	adminhandler "github.com/Wei-Shaw/sub2api/internal/handler/admin"
	"github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func TestRegisterAccountRoutesIncludesCopilotBillingCompatibilityRoute(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	adminGroup := router.Group("/api/v1/admin")
	handlers := &handler.Handlers{
		Admin: &handler.AdminHandlers{
			Account:     &adminhandler.AccountHandler{},
			OpenAIOAuth: &adminhandler.OpenAIOAuthHandler{},
		},
	}
	stepUp := middleware.StepUpAuthMiddleware(func(ctx *gin.Context) {
		ctx.Next()
	})

	registerAccountRoutes(adminGroup, handlers, stepUp)

	found := false
	for _, route := range router.Routes() {
		if route.Method == http.MethodPost && route.Path == "/api/v1/admin/accounts/copilot-billing-pat/validate" {
			found = true
			break
		}
	}
	require.True(t, found, "Copilot billing PAT compatibility route is not registered")
}

func TestRegisterOpenAIOAuthRoutesIncludesBothCopilotCancelRoutes(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	adminGroup := router.Group("/api/v1/admin")
	handlers := &handler.Handlers{
		Admin: &handler.AdminHandlers{
			OpenAIOAuth: &adminhandler.OpenAIOAuthHandler{},
		},
	}

	registerOpenAIOAuthRoutes(adminGroup, handlers)

	found := map[string]bool{
		"/api/v1/admin/openai/copilot/flows/:id":         false,
		"/api/v1/admin/provider-oauth/copilot/flows/:id": false,
	}
	for _, route := range router.Routes() {
		if route.Method == http.MethodDelete {
			if _, expected := found[route.Path]; expected {
				found[route.Path] = true
			}
		}
	}
	for path, registered := range found {
		require.Truef(t, registered, "Copilot cancel route %s is not registered", path)
	}
}
