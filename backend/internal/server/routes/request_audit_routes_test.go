package routes

import (
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/handler"
	"github.com/Wei-Shaw/sub2api/internal/requestaudit"
	servermiddleware "github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func requestAuditRouteHandlers() *handler.Handlers {
	return &handler.Handlers{Admin: &handler.AdminHandlers{
		RequestAudit: requestaudit.NewAdminHandler(nil),
	}}
}

func TestRequestAuditAdminRoutesRequireAdminAuthentication(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	adminAuth := servermiddleware.AdminAuthMiddleware(func(c *gin.Context) {
		if c.GetHeader("Authorization") == "" {
			servermiddleware.AbortWithError(c, http.StatusUnauthorized, "UNAUTHORIZED", "Authorization required")
			return
		}
		servermiddleware.AbortWithError(c, http.StatusForbidden, "FORBIDDEN", "Admin access required")
	})
	pass := func(c *gin.Context) { c.Next() }
	RegisterAdminRoutes(
		router.Group("/api/v1"), requestAuditRouteHandlers(), adminAuth,
		servermiddleware.AuditLogMiddleware(pass), servermiddleware.StepUpAuthMiddleware(pass),
		servermiddleware.StrictStepUpAuthMiddleware(pass), nil, nil,
	)

	for _, path := range []string{
		"/api/v1/admin/request-audit/policy",
		"/api/v1/admin/request-audit/runtime",
		"/api/v1/admin/request-audit/records",
		"/api/v1/admin/request-audit/records/42",
		"/api/v1/admin/request-audit/records/42/content",
	} {
		recorder := httptest.NewRecorder()
		request := httptest.NewRequest(http.MethodGet, path, nil)
		router.ServeHTTP(recorder, request)
		require.Equal(t, http.StatusUnauthorized, recorder.Code, path)
	}
}

func TestRequestAuditSensitiveRoutesUseStrictStepUp(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	pass := func(c *gin.Context) { c.Next() }
	strictCalls := 0
	strict := servermiddleware.StrictStepUpAuthMiddleware(func(c *gin.Context) {
		strictCalls++
		servermiddleware.AbortWithError(c, http.StatusForbidden, "STEP_UP_REQUIRED", "recent verification required")
	})
	RegisterAdminRoutes(
		router.Group("/api/v1"), requestAuditRouteHandlers(), servermiddleware.AdminAuthMiddleware(pass),
		servermiddleware.AuditLogMiddleware(pass), servermiddleware.StepUpAuthMiddleware(pass), strict, nil, nil,
	)

	tests := []struct {
		method string
		path   string
		body   string
	}{
		{http.MethodPut, "/api/v1/admin/request-audit/policy", `{}`},
		{http.MethodGet, "/api/v1/admin/request-audit/records/42/content", ""},
	}
	for _, tc := range tests {
		recorder := httptest.NewRecorder()
		request := httptest.NewRequest(tc.method, tc.path, strings.NewReader(tc.body))
		request.Header.Set("Content-Type", "application/json")
		router.ServeHTTP(recorder, request)
		require.Equal(t, http.StatusForbidden, recorder.Code)
		require.Contains(t, recorder.Body.String(), "STEP_UP_REQUIRED")
	}
	require.Equal(t, len(tests), strictCalls)
}
