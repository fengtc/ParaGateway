package admin

import (
	"encoding/json"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/stretchr/testify/require"
)

func TestUpstreamAccountDTOIsIndependentAndNeverSerializesCiphertext(t *testing.T) {
	now := time.Now().UTC()
	account := &service.UpstreamAccount{
		ID: "upstream-1", Name: "Independent", ProviderType: service.UpstreamProviderOpenAI,
		BaseURL: "https://api.openai.com", AuthType: service.UpstreamAuthAPIKey,
		CredentialCiphertext: "encrypted-super-secret", CredentialHint: "cret",
		IsActive: true, Priority: 100, Weight: 200, MaxConcurrency: 8, RPMLimit: 120,
		CircuitBreakerThreshold: 3, CircuitBreakerCooldownSeconds: 60,
		QuotaStatus: "unknown", CreatedAt: now, UpdatedAt: now,
	}

	payload, err := json.Marshal(upstreamAccountFromService(account))

	require.NoError(t, err)
	require.NotContains(t, string(payload), "encrypted-super-secret")
	require.NotContains(t, string(payload), "credential_ciphertext")
	require.Contains(t, string(payload), `"masked_credential":"********cret"`)
	require.Contains(t, string(payload), `"weight":200`)
}

func TestOfficialAccountContractRejectsIndependentWIFConfiguration(t *testing.T) {
	require.True(t, containsIndependentUpstreamAuth(map[string]any{"auth_type": "wif"}, nil))
	require.True(t, containsIndependentUpstreamAuth(map[string]any{"wif_client_id": "client"}, nil))
	require.True(t, containsIndependentUpstreamAuth(nil, map[string]any{"wif_client_secret": "secret"}))
	require.False(t, containsIndependentUpstreamAuth(map[string]any{"upstream_billing_probe_enabled": true}, map[string]any{"api_key": "secret"}))
}

func TestOfficialAccountRequestAndResponseDoNotExposeWorkerPolicyJSONFields(t *testing.T) {
	createJSON, err := json.Marshal(CreateAccountRequest{Weight: 200, RPMLimit: 120, CircuitBreakerThreshold: 3, CircuitBreakerCooldownSeconds: 60})
	require.NoError(t, err)
	require.NotContains(t, string(createJSON), "weight")
	require.NotContains(t, string(createJSON), "rpm_limit")
	require.NotContains(t, string(createJSON), "circuit_breaker")
}
