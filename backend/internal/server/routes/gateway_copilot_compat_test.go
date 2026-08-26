package routes

import (
	"net/http"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestGatewayRoutesLegacyCopilotCompatibilityPathsAreRegistered(t *testing.T) {
	router := newGatewayRoutesTestRouter()
	registered := make(map[string]bool)
	for _, route := range router.Routes() {
		registered[route.Method+" "+route.Path] = true
	}

	for _, route := range []string{
		http.MethodPost + " /copilot/v1/messages",
		http.MethodPost + " /copilot/v1/chat/completions",
		http.MethodGet + " /copilot/v1/models",
	} {
		require.True(t, registered[route], "%s should remain registered for legacy Copilot clients", route)
	}
}
