package middleware

import (
	"github.com/Wei-Shaw/sub2api/internal/service"

	"github.com/gin-gonic/gin"
)

// AdminComplianceGuard is intentionally kept as a no-op compatibility
// middleware. Older route registrations still call it, but ParaGateway never
// blocks an administrator behind the upstream first-run acknowledgement.
func AdminComplianceGuard(_ *service.SettingService) gin.HandlerFunc {
	return func(c *gin.Context) {
		c.Next()
	}
}
