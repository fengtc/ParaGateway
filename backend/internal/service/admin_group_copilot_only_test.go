//go:build unit

package service

import (
	"context"
	"testing"

	"github.com/stretchr/testify/require"
)

func newCopilotOnlyAdminGroup(id int64) *Group {
	return &Group{
		ID:                    id,
		Name:                  "legacy-copilot",
		Platform:              PlatformOpenAI,
		RateMultiplier:        1,
		Status:                StatusActive,
		SubscriptionType:      SubscriptionTypeStandard,
		GitHubCopilotOnly:     true,
		AllowMessagesDispatch: true,
	}
}

func TestAdminServiceCreateGroupNormalizesCopilotProductPlatform(t *testing.T) {
	repo := &groupRepoStubForAdmin{createID: 60}
	svc := &adminServiceImpl{groupRepo: repo}

	created, err := svc.CreateGroup(context.Background(), &CreateGroupInput{
		Name:                  "copilot-default",
		Platform:              legacyGitHubCopilotPlatform,
		RateMultiplier:        1,
		AllowMessagesDispatch: false,
		AllowLive:             true,
		RequireOAuthOnly:      false,
	})

	require.NoError(t, err)
	require.NotNil(t, created)
	require.Same(t, repo.created, created)
	require.Equal(t, PlatformOpenAI, created.Platform)
	require.True(t, created.GitHubCopilotOnly)
	require.True(t, created.AllowMessagesDispatch)
	require.True(t, created.RequireOAuthOnly)
	require.False(t, created.AllowLive)
}

func TestAdminServiceUpdateGroupPreservesCopilotOnlyMarker(t *testing.T) {
	existing := newCopilotOnlyAdminGroup(61)
	repo := &groupRepoStubForAdmin{getByID: existing}
	svc := &adminServiceImpl{groupRepo: repo}
	description := "updated description"

	updated, err := svc.UpdateGroup(context.Background(), existing.ID, &UpdateGroupInput{
		Description: &description,
	})

	require.NoError(t, err)
	require.NotNil(t, updated)
	require.NotNil(t, repo.updated)
	require.True(t, repo.updated.GitHubCopilotOnly)
	require.True(t, repo.updated.AllowMessagesDispatch)
	require.Equal(t, PlatformOpenAI, repo.updated.Platform)
}

func TestAdminServiceUpdateGroupAcceptsCopilotProductPlatformForMarkedGroup(t *testing.T) {
	existing := newCopilotOnlyAdminGroup(611)
	repo := &groupRepoStubForAdmin{getByID: existing}
	svc := &adminServiceImpl{groupRepo: repo}

	updated, err := svc.UpdateGroup(context.Background(), existing.ID, &UpdateGroupInput{
		Platform: legacyGitHubCopilotPlatform,
	})

	require.NoError(t, err)
	require.NotNil(t, updated)
	require.True(t, updated.GitHubCopilotOnly)
	require.Equal(t, PlatformOpenAI, updated.Platform)
	require.True(t, updated.AllowMessagesDispatch)
	require.True(t, updated.RequireOAuthOnly)
	require.False(t, updated.AllowLive)
}

func TestAdminServiceUpdateGroupRejectsCopilotOnlyPlatformChange(t *testing.T) {
	existing := newCopilotOnlyAdminGroup(62)
	repo := &groupRepoStubForAdmin{getByID: existing}
	svc := &adminServiceImpl{groupRepo: repo}

	updated, err := svc.UpdateGroup(context.Background(), existing.ID, &UpdateGroupInput{
		Platform: PlatformAnthropic,
	})

	require.Nil(t, updated)
	requireApplicationErrorReason(t, err, "COPILOT_ONLY_GROUP_PLATFORM_IMMUTABLE")
	require.Nil(t, repo.updated)
}

func TestAdminServiceUpdateGroupRejectsOrdinaryAccountCopyIntoCopilotOnlyGroupBeforeWrite(t *testing.T) {
	target := newCopilotOnlyAdminGroup(63)
	source := &Group{ID: 64, Platform: PlatformOpenAI}
	repo := &groupRepoStubForAdmin{
		getByIDByID: map[int64]*Group{
			target.ID: target,
			source.ID: source,
		},
		getAccountIDsByGroupIDsFn: func(groupIDs []int64) ([]int64, error) {
			require.Equal(t, []int64{source.ID}, groupIDs)
			return []int64{65}, nil
		},
	}
	accountRepo := &accountRepoStubForBulkUpdate{getByIDsAccounts: []*Account{
		{ID: 65, Platform: PlatformOpenAI, Type: AccountTypeOAuth, Credentials: map[string]any{"oauth_profile": "chatgpt"}},
	}}
	svc := &adminServiceImpl{groupRepo: repo, accountRepo: accountRepo}

	updated, err := svc.UpdateGroup(context.Background(), target.ID, &UpdateGroupInput{
		CopyAccountsFromGroupIDs: []int64{source.ID},
	})

	require.Nil(t, updated)
	requireApplicationErrorReason(t, err, "COPILOT_ONLY_GROUP_SOURCE_MISMATCH")
	require.Nil(t, repo.updated)
}
