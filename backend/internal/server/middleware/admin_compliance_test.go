package middleware

import (
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func TestAdminComplianceGuardAlwaysAllowsAdminRoutes(t *testing.T) {
	for _, envValue := range []string{"", "false", "true"} {
		t.Run("env="+envValue, func(t *testing.T) {
			t.Setenv("PARAGATEWAY_DISABLE_ADMIN_COMPLIANCE", envValue)
			gin.SetMode(gin.TestMode)
			router := gin.New()
			router.Use(AdminComplianceGuard(nil))
			router.GET("/api/v1/admin/users", func(c *gin.Context) {
				c.String(http.StatusOK, "ok")
			})

			req := httptest.NewRequest(http.MethodGet, "/api/v1/admin/users", nil)
			w := httptest.NewRecorder()
			router.ServeHTTP(w, req)

			require.Equal(t, http.StatusOK, w.Code)
			require.Equal(t, "ok", w.Body.String())
		})
	}
}
