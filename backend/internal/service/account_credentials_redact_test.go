//go:build unit

package service

import (
	"testing"

	"github.com/stretchr/testify/require"
)

func TestMergePreservingSensitiveCreds_PreservesSensitiveWhenIncomingMissing(t *testing.T) {
	existing := map[string]any{
		"refresh_token": "rt-old",
		"access_token":  "at-old",
		"api_key":       "sk-old",
		"github_token":  "gh-old",
		"billing_pat":   "pat-old",
		"base_url":      "https://old.example.com",
	}
	incoming := map[string]any{
		"base_url":      "https://new.example.com",
		"model_mapping": map[string]any{"foo": "bar"},
	}

	out := MergePreservingSensitiveCreds(existing, incoming)

	require.Equal(t, "rt-old", out["refresh_token"], "incoming 没传 refresh_token，应保留 existing")
	require.Equal(t, "at-old", out["access_token"])
	require.Equal(t, "sk-old", out["api_key"])
	require.Equal(t, "gh-old", out["github_token"])
	require.Equal(t, "pat-old", out["billing_pat"])
	require.Equal(t, "https://new.example.com", out["base_url"], "非敏感键由 incoming 决定")
	require.Equal(t, map[string]any{"foo": "bar"}, out["model_mapping"])
}

func TestMergePreservingSensitiveCreds_OverwritesWhenIncomingProvidesSensitive(t *testing.T) {
	existing := map[string]any{
		"refresh_token": "rt-old",
		"api_key":       "sk-old",
	}
	incoming := map[string]any{
		"refresh_token": "rt-new",
		// 显式没传 api_key —— 应保留
	}
	out := MergePreservingSensitiveCreds(existing, incoming)
	require.Equal(t, "rt-new", out["refresh_token"], "incoming 显式传入应覆盖")
	require.Equal(t, "sk-old", out["api_key"], "incoming 没传应保留")
}

func TestMergePreservingSensitiveCreds_DoesNotMutateInputs(t *testing.T) {
	existing := map[string]any{"refresh_token": "rt"}
	incoming := map[string]any{"base_url": "x"}

	_ = MergePreservingSensitiveCreds(existing, incoming)

	require.Equal(t, "rt", existing["refresh_token"])
	require.NotContains(t, existing, "base_url")
	require.Equal(t, "x", incoming["base_url"])
	require.NotContains(t, incoming, "refresh_token")
}

func TestMergePreservingSensitiveCreds_NilInputs(t *testing.T) {
	out := MergePreservingSensitiveCreds(nil, map[string]any{"base_url": "x"})
	require.Equal(t, "x", out["base_url"])
	require.NotContains(t, out, "refresh_token")

	out2 := MergePreservingSensitiveCreds(map[string]any{"refresh_token": "rt"}, nil)
	require.Equal(t, "rt", out2["refresh_token"])
}

func TestMergePreservingSensitiveCreds_NonSensitiveDeletionAllowed(t *testing.T) {
	existing := map[string]any{
		"refresh_token": "rt",
		"base_url":      "https://old",
		"project_id":    "p1",
	}
	incoming := map[string]any{
		"base_url": "https://new",
		// 不带 project_id —— 等同删除（非敏感键由 incoming 决定）
	}
	out := MergePreservingSensitiveCreds(existing, incoming)
	require.Equal(t, "rt", out["refresh_token"], "敏感键保留")
	require.Equal(t, "https://new", out["base_url"])
	require.NotContains(t, out, "project_id", "非敏感键 incoming 不传 = 删除")
}

func TestMergePreservingGitHubCopilotCreds_PreservesServerMetadata(t *testing.T) {
	existing := map[string]any{
		"oauth_profile":       CopilotOAuthProfile,
		"base_url":            CopilotAPIBaseURL,
		"expires_at":          "2030-01-01T00:00:00Z",
		"refresh_at":          "2029-12-31T23:59:00Z",
		"github_login":        "octocat",
		"github_user_id":      "42",
		"github_access_token": "github-secret",
		"access_token":        "copilot-secret",
		"billing_pat":         "billing-secret",
		"billing_username":    "old-user",
		"model_mapping":       map[string]any{"gpt-old": "gpt-old"},
		"obsolete":            "remove-me",
	}
	incoming := map[string]any{
		"model_mapping": map[string]any{"gpt-new": "gpt-new"},
	}

	out := MergePreservingGitHubCopilotCreds(existing, incoming)

	for _, key := range githubCopilotServerCredentialKeys {
		require.Equal(t, existing[key], out[key], key)
	}
	require.Equal(t, "github-secret", out["github_access_token"])
	require.Equal(t, "copilot-secret", out["access_token"])
	require.Equal(t, "billing-secret", out["billing_pat"])
	require.Equal(t, map[string]any{"gpt-new": "gpt-new"}, out["model_mapping"])
	require.NotContains(t, out, "billing_username", "editable billing username may be removed")
	require.NotContains(t, out, "obsolete")
}

func TestMergePreservingGitHubCopilotCreds_AllowsExplicitMetadataRotation(t *testing.T) {
	existing := map[string]any{
		"oauth_profile": CopilotOAuthProfile,
		"base_url":      CopilotAPIBaseURL,
	}
	incoming := map[string]any{
		"oauth_profile": CopilotOAuthProfile,
		"base_url":      "https://api.individual.githubcopilot.com",
	}

	out := MergePreservingGitHubCopilotCreds(existing, incoming)

	require.Equal(t, CopilotOAuthProfile, out["oauth_profile"])
	require.Equal(t, "https://api.individual.githubcopilot.com", out["base_url"])
}

func TestIsSensitiveCredentialKey(t *testing.T) {
	require.True(t, IsSensitiveCredentialKey("refresh_token"))
	require.True(t, IsSensitiveCredentialKey("api_key"))
	require.True(t, IsSensitiveCredentialKey("github_token"))
	require.True(t, IsSensitiveCredentialKey("billing_pat"))
	require.True(t, IsSensitiveCredentialKey("private_key"))
	require.False(t, IsSensitiveCredentialKey("base_url"))
	require.False(t, IsSensitiveCredentialKey(""))
	require.False(t, IsSensitiveCredentialKey("model_mapping"))
}
