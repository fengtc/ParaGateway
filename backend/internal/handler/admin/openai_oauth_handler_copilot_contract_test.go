package admin

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

func TestCopilotBillingPATValidateRequestAcceptsReferenceTokenField(t *testing.T) {
	gin.SetMode(gin.TestMode)
	ctx, _ := gin.CreateTestContext(httptest.NewRecorder())
	ctx.Request = httptest.NewRequest(
		http.MethodPost,
		"/api/v1/admin/accounts/copilot-billing-pat/validate",
		strings.NewReader(`{"username":"octocat","token":"github_pat_reference","proxy_id":9}`),
	)
	ctx.Request.Header.Set("Content-Type", "application/json")

	var request CopilotBillingPATValidateRequest
	require.NoError(t, ctx.ShouldBindJSON(&request))
	require.Equal(t, "octocat", request.Username)
	require.Equal(t, "github_pat_reference", request.token())
	require.NotNil(t, request.ProxyID)
	require.EqualValues(t, 9, *request.ProxyID)
}

func TestCopilotBillingPATValidateRequestKeepsLegacyBillingPATField(t *testing.T) {
	gin.SetMode(gin.TestMode)
	ctx, _ := gin.CreateTestContext(httptest.NewRecorder())
	ctx.Request = httptest.NewRequest(
		http.MethodPost,
		"/api/v1/admin/openai/copilot/billing-pat/validate",
		strings.NewReader(`{"username":"octocat","billing_pat":"github_pat_legacy"}`),
	)
	ctx.Request.Header.Set("Content-Type", "application/json")

	var request CopilotBillingPATValidateRequest
	require.NoError(t, ctx.ShouldBindJSON(&request))
	require.Equal(t, "github_pat_legacy", request.token())
}

func TestCopilotOAuthFlowResponseDoesNotExposeServerSecretsOrOperationIDs(t *testing.T) {
	now := time.Now().UTC()
	response := copilotOAuthFlowResponse(&service.CopilotOAuthFlowResult{
		FlowID:          "flow-safe",
		Profile:         service.CopilotOAuthProfile,
		Status:          service.CopilotOAuthStatusCompleted,
		UserCode:        "VISIBLE-CODE",
		VerificationURI: "https://github.com/login/device",
		ExpiresAt:       now.Add(time.Minute),
		NextPollAt:      now,
		ProviderAccount: &service.Account{
			ID:       42,
			Name:     "Copilot",
			Platform: service.PlatformOpenAI,
			Type:     service.AccountTypeOAuth,
			Credentials: map[string]any{
				"oauth_profile":       service.CopilotOAuthProfile,
				"github_access_token": "github-secret",
				"access_token":        "copilot-secret",
				"billing_pat":         "billing-secret",
			},
			Extra: map[string]any{
				service.AccountCreateOperationIDExtraKey: "create-operation-secret",
				"duplicate_operation_id":                 "duplicate-operation-secret",
				"ordinary":                               "kept",
			},
		},
	})

	raw, err := json.Marshal(response)
	require.NoError(t, err)
	serialized := string(raw)
	require.Contains(t, serialized, "VISIBLE-CODE")
	require.Contains(t, serialized, `"ordinary":"kept"`)
	for _, forbidden := range []string{
		"device_code", "github-secret", "copilot-secret", "billing-secret",
		service.AccountCreateOperationIDExtraKey, "duplicate_operation_id",
		"create-operation-secret", "duplicate-operation-secret",
	} {
		require.NotContains(t, serialized, forbidden)
	}
}
