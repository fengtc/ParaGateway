//go:build unit

package service

import (
	"context"
	"testing"

	"github.com/stretchr/testify/require"
)

type copilotAvailableModelsRepo struct {
	AccountRepository
	accounts []Account
}

func (r *copilotAvailableModelsRepo) ListSchedulable(_ context.Context) ([]Account, error) {
	return append([]Account(nil), r.accounts...), nil
}

func (r *copilotAvailableModelsRepo) ListSchedulableByGroupID(_ context.Context, _ int64) ([]Account, error) {
	return append([]Account(nil), r.accounts...), nil
}

func TestGetAvailableModels_AnthropicIncludesMixedCopilotMappings(t *testing.T) {
	groupID := int64(77)
	repo := &copilotAvailableModelsRepo{accounts: []Account{
		{
			ID:          1,
			Platform:    PlatformAnthropic,
			Status:      StatusActive,
			Schedulable: true,
			Credentials: map[string]any{"model_mapping": map[string]any{"claude-native": "claude-native"}},
		},
		{
			ID:          2,
			Platform:    PlatformOpenAI,
			Type:        AccountTypeOAuth,
			Status:      StatusActive,
			Schedulable: true,
			Credentials: map[string]any{
				"oauth_profile": CopilotOAuthProfile,
				"model_mapping": map[string]any{"claude-copilot": "claude-sonnet-4-5"},
			},
		},
		{
			ID:          3,
			Platform:    PlatformOpenAI,
			Type:        AccountTypeOAuth,
			Status:      StatusActive,
			Schedulable: true,
			Credentials: map[string]any{
				"oauth_profile": "chatgpt",
				"model_mapping": map[string]any{"regular-openai": "gpt-5"},
			},
		},
		{
			ID:          4,
			Platform:    PlatformAntigravity,
			Status:      StatusActive,
			Schedulable: true,
			Extra:       map[string]any{"mixed_scheduling": true},
			Credentials: map[string]any{"model_mapping": map[string]any{"claude-antigravity": "claude-sonnet-4-5"}},
		},
	}}
	svc := &GatewayService{accountRepo: repo}

	models := svc.GetAvailableModels(context.Background(), &groupID, PlatformAnthropic)

	require.Contains(t, models, "claude-antigravity")
	require.Contains(t, models, "claude-copilot")
	require.Contains(t, models, "claude-native")
	require.NotContains(t, models, "regular-openai")
}

func TestGetAvailableModels_OpenAICopilotOnlyWithoutMappingsUsesCopilotDefaults(t *testing.T) {
	groupID := int64(78)
	repo := &copilotAvailableModelsRepo{accounts: []Account{
		{
			ID:          1,
			Platform:    PlatformOpenAI,
			Type:        AccountTypeOAuth,
			Status:      StatusActive,
			Schedulable: true,
			Credentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
		},
		{
			ID:          2,
			Platform:    PlatformOpenAI,
			Type:        AccountTypeOAuth,
			Status:      StatusActive,
			Schedulable: true,
			Credentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
		},
	}}
	svc := &GatewayService{accountRepo: repo}

	require.Equal(t, CopilotDefaultModels(), svc.GetAvailableModels(context.Background(), &groupID, PlatformOpenAI))
}

func TestGetAvailableModels_OpenAIMixedGroupWithoutMappingsUsesOpenAIFallback(t *testing.T) {
	groupID := int64(79)
	repo := &copilotAvailableModelsRepo{accounts: []Account{
		{
			ID:          1,
			Platform:    PlatformOpenAI,
			Type:        AccountTypeOAuth,
			Status:      StatusActive,
			Schedulable: true,
			Credentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
		},
		{
			ID:          2,
			Platform:    PlatformOpenAI,
			Type:        AccountTypeOAuth,
			Status:      StatusActive,
			Schedulable: true,
			Credentials: map[string]any{"oauth_profile": "chatgpt"},
		},
	}}
	svc := &GatewayService{accountRepo: repo}

	require.Nil(t, svc.GetAvailableModels(context.Background(), &groupID, PlatformOpenAI))
}

func TestGetAvailableModels_OpenAICopilotMappingRemainsWhitelist(t *testing.T) {
	groupID := int64(80)
	repo := &copilotAvailableModelsRepo{accounts: []Account{
		{
			ID:          1,
			Platform:    PlatformOpenAI,
			Type:        AccountTypeOAuth,
			Status:      StatusActive,
			Schedulable: true,
			Credentials: map[string]any{
				"oauth_profile": CopilotOAuthProfile,
				"model_mapping": map[string]any{"copilot-public-model": "gpt-4.1"},
			},
		},
	}}
	svc := &GatewayService{accountRepo: repo}

	require.Equal(t, []string{"copilot-public-model"}, svc.GetAvailableModels(context.Background(), &groupID, PlatformOpenAI))
}
