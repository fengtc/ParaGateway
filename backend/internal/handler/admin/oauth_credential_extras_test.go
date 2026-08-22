package admin

import (
	"testing"

	"github.com/stretchr/testify/require"
)

func TestMergeOAuthCredentialExtrasOnlyAcceptsModelMapping(t *testing.T) {
	serverCredentials := map[string]any{
		"access_token":  "server-access",
		"refresh_token": "server-refresh",
	}
	extras := map[string]any{
		"access_token": "client-access",
		"api_key":      "client-key",
		"model_mapping": map[string]any{
			" gpt-5.4 ": " gpt-5.6-sol ",
		},
	}

	merged, err := mergeOAuthCredentialExtras(serverCredentials, extras)

	require.NoError(t, err)
	require.Equal(t, "server-access", merged["access_token"])
	require.Equal(t, "server-refresh", merged["refresh_token"])
	require.NotContains(t, merged, "api_key")
	require.Equal(t, map[string]any{"gpt-5.4": "gpt-5.6-sol"}, merged["model_mapping"])
	require.NotContains(t, serverCredentials, "model_mapping")
}

func TestMergeOAuthCredentialExtrasPreservesExplicitEmptyMapping(t *testing.T) {
	merged, err := mergeOAuthCredentialExtras(
		map[string]any{"access_token": "server-access"},
		map[string]any{"model_mapping": map[string]any{}},
	)

	require.NoError(t, err)
	require.Equal(t, map[string]any{}, merged["model_mapping"])
}

func TestMergeOAuthCredentialExtrasRejectsInvalidMapping(t *testing.T) {
	tests := []struct {
		name    string
		mapping any
	}{
		{name: "not an object", mapping: "gpt-5.4"},
		{name: "missing target", mapping: map[string]any{"gpt-5.4": ""}},
		{name: "source wildcard in middle", mapping: map[string]any{"gpt-*-mini": "gpt-5.4"}},
		{name: "target wildcard", mapping: map[string]any{"gpt-5": "gpt-*"}},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			_, err := mergeOAuthCredentialExtras(nil, map[string]any{"model_mapping": test.mapping})
			require.Error(t, err)
		})
	}
}
