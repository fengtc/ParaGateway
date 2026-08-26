package service

import (
	"context"
	"errors"
	"testing"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/Wei-Shaw/sub2api/internal/pkg/ctxkey"
	"github.com/stretchr/testify/require"
)

type copilotOnlySchedulingRepo struct {
	AccountRepository
	accounts []Account
}

func (r *copilotOnlySchedulingRepo) ListSchedulableByGroupIDAndPlatform(_ context.Context, _ int64, _ string) ([]Account, error) {
	return append([]Account(nil), r.accounts...), nil
}

func canonicalCopilotOnlySchedulingAccount(id int64, priority int) Account {
	return Account{
		ID:          id,
		Name:        "canonical-copilot",
		Platform:    PlatformOpenAI,
		Type:        AccountTypeOAuth,
		Priority:    priority,
		Status:      StatusActive,
		Schedulable: true,
		Credentials: map[string]any{
			"oauth_profile": CopilotOAuthProfile,
			"access_token":  "copilot-token",
		},
	}
}

func githubCopilotOnlySchedulingContext() context.Context {
	ctx := WithGitHubCopilotOnly(context.Background())
	return context.WithValue(ctx, ctxkey.ForcePlatform, PlatformOpenAI)
}

func TestGatewayCopilotOnlySchedulingSelectsCanonicalIdentityExclusively(t *testing.T) {
	canonical := canonicalCopilotOnlySchedulingAccount(4, 100)
	repo := &copilotOnlySchedulingRepo{accounts: []Account{
		{
			ID: 1, Name: "ordinary-openai", Platform: PlatformOpenAI, Type: AccountTypeOAuth,
			Priority: 1, Status: StatusActive, Schedulable: true,
			Credentials: map[string]any{"oauth_profile": "chatgpt"},
		},
		{
			ID: 2, Name: "legacy-copilot", Platform: "copilot", Type: AccountTypeAPIKey,
			Priority: 1, Status: StatusActive, Schedulable: true,
			Credentials: map[string]any{"github_token": "legacy-token"},
		},
		{
			ID: 3, Name: "extra-only-copilot", Platform: PlatformOpenAI, Type: AccountTypeOAuth,
			Priority: 1, Status: StatusActive, Schedulable: true,
			Extra: map[string]any{"oauth_profile": CopilotOAuthProfile},
		},
		canonical,
	}}
	svc := &GatewayService{
		accountRepo: repo,
		cfg:         &config.Config{RunMode: config.RunModeStandard},
	}
	groupID := int64(77)

	selected, err := svc.SelectAccountForModel(githubCopilotOnlySchedulingContext(), &groupID, "", "")

	require.NoError(t, err)
	require.NotNil(t, selected)
	require.Equal(t, canonical.ID, selected.ID)
	require.True(t, selected.IsGitHubCopilot())
}

func TestGatewayCopilotOnlySchedulingRejectsNonCanonicalIdentities(t *testing.T) {
	tests := []struct {
		name    string
		account Account
	}{
		{
			name: "ordinary OpenAI OAuth",
			account: Account{
				ID: 1, Platform: PlatformOpenAI, Type: AccountTypeOAuth,
				Status: StatusActive, Schedulable: true,
				Credentials: map[string]any{"oauth_profile": "chatgpt"},
			},
		},
		{
			name: "legacy Copilot platform",
			account: Account{
				ID: 2, Platform: "copilot", Type: AccountTypeAPIKey,
				Status: StatusActive, Schedulable: true,
				Credentials: map[string]any{"github_token": "legacy-token"},
			},
		},
		{
			name: "oauth profile only in extra",
			account: Account{
				ID: 3, Platform: PlatformOpenAI, Type: AccountTypeOAuth,
				Status: StatusActive, Schedulable: true,
				Extra: map[string]any{"oauth_profile": CopilotOAuthProfile},
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			svc := &GatewayService{
				accountRepo: &copilotOnlySchedulingRepo{accounts: []Account{tt.account}},
				cfg:         &config.Config{RunMode: config.RunModeStandard},
			}
			groupID := int64(78)

			selected, err := svc.SelectAccountForModel(githubCopilotOnlySchedulingContext(), &groupID, "", "")

			require.Nil(t, selected)
			require.Error(t, err)
			require.True(t, errors.Is(err, ErrNoAvailableAccounts), "unexpected error: %v", err)
		})
	}
}
