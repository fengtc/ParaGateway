//go:build unit

package service

import (
	"context"
	"testing"

	"github.com/stretchr/testify/require"
)

type accountServiceCopilotRepo struct {
	AccountRepository
	account  *Account
	created  *Account
	updated  *Account
	boundIDs []int64
}

func (r *accountServiceCopilotRepo) Create(_ context.Context, account *Account) error {
	account.ID = 101
	r.created = cloneAccountForCopilotServiceTest(account)
	return nil
}

func (r *accountServiceCopilotRepo) GetByID(_ context.Context, _ int64) (*Account, error) {
	return cloneAccountForCopilotServiceTest(r.account), nil
}

func (r *accountServiceCopilotRepo) Update(_ context.Context, account *Account) error {
	r.updated = cloneAccountForCopilotServiceTest(account)
	return nil
}

func (r *accountServiceCopilotRepo) BindGroups(_ context.Context, _ int64, groupIDs []int64) error {
	r.boundIDs = append([]int64(nil), groupIDs...)
	return nil
}

func cloneAccountForCopilotServiceTest(account *Account) *Account {
	if account == nil {
		return nil
	}
	clone := *account
	clone.Credentials = make(map[string]any, len(account.Credentials))
	for key, value := range account.Credentials {
		clone.Credentials[key] = value
	}
	return &clone
}

type accountServiceCopilotGroupRepo struct {
	GroupRepository
	groups map[int64]*Group
}

func (r *accountServiceCopilotGroupRepo) GetByID(_ context.Context, id int64) (*Group, error) {
	group, ok := r.groups[id]
	if !ok {
		return nil, ErrGroupNotFound
	}
	clone := *group
	return &clone, nil
}

func TestAccountServiceCreateNormalizesLegacyCopilotBeforeWrite(t *testing.T) {
	group := &Group{ID: 91, Platform: PlatformOpenAI, GitHubCopilotOnly: true}
	accountRepo := &accountServiceCopilotRepo{}
	svc := NewAccountService(accountRepo, &accountServiceCopilotGroupRepo{groups: map[int64]*Group{group.ID: group}})

	created, err := svc.Create(context.Background(), CreateAccountRequest{
		Name:        "legacy-copilot",
		Platform:    legacyGitHubCopilotPlatform,
		Type:        AccountTypeAPIKey,
		GroupIDs:    []int64{group.ID},
		Credentials: map[string]any{"github_token": "legacy-token"},
	})

	require.NoError(t, err)
	require.NotNil(t, created)
	require.NotNil(t, accountRepo.created)
	require.Equal(t, PlatformOpenAI, accountRepo.created.Platform)
	require.Equal(t, AccountTypeOAuth, accountRepo.created.Type)
	require.True(t, accountRepo.created.IsGitHubCopilot())
	require.Equal(t, "legacy-token", accountRepo.created.GetCredential("github_access_token"))
	require.Empty(t, accountRepo.created.GetCredential("github_token"))
	require.Equal(t, []int64{group.ID}, accountRepo.boundIDs)
}

func TestAccountServiceCreateRejectsOrdinaryAccountInCopilotOnlyGroupBeforeWrite(t *testing.T) {
	group := &Group{ID: 92, Platform: PlatformOpenAI, GitHubCopilotOnly: true}
	accountRepo := &accountServiceCopilotRepo{}
	svc := NewAccountService(accountRepo, &accountServiceCopilotGroupRepo{groups: map[int64]*Group{group.ID: group}})

	created, err := svc.Create(context.Background(), CreateAccountRequest{
		Name:        "ordinary-openai",
		Platform:    PlatformOpenAI,
		Type:        AccountTypeAPIKey,
		GroupIDs:    []int64{group.ID},
		Credentials: map[string]any{"api_key": "test-key"},
	})

	require.Nil(t, created)
	requireApplicationErrorReason(t, err, "COPILOT_ONLY_GROUP_ACCOUNT_MISMATCH")
	require.Nil(t, accountRepo.created)
	require.Empty(t, accountRepo.boundIDs)
}

func TestAccountServiceUpdateRejectsOrdinaryAccountInCopilotOnlyGroupBeforeWrite(t *testing.T) {
	group := &Group{ID: 93, Platform: PlatformOpenAI, GitHubCopilotOnly: true}
	accountRepo := &accountServiceCopilotRepo{account: &Account{
		ID:          94,
		Name:        "ordinary-openai",
		Platform:    PlatformOpenAI,
		Type:        AccountTypeOAuth,
		Credentials: map[string]any{"oauth_profile": "chatgpt"},
	}}
	svc := NewAccountService(accountRepo, &accountServiceCopilotGroupRepo{groups: map[int64]*Group{group.ID: group}})
	groupIDs := []int64{group.ID}

	updated, err := svc.Update(context.Background(), accountRepo.account.ID, UpdateAccountRequest{GroupIDs: &groupIDs})

	require.Nil(t, updated)
	requireApplicationErrorReason(t, err, "COPILOT_ONLY_GROUP_ACCOUNT_MISMATCH")
	require.Nil(t, accountRepo.updated)
	require.Empty(t, accountRepo.boundIDs)
}

func TestAccountServiceUpdatePreservesCopilotIdentityAndAcceptsEditableMetadata(t *testing.T) {
	group := &Group{ID: 95, Platform: PlatformOpenAI, GitHubCopilotOnly: true}
	accountRepo := &accountServiceCopilotRepo{account: &Account{
		ID:       96,
		Name:     "copilot",
		Platform: PlatformOpenAI,
		Type:     AccountTypeOAuth,
		Credentials: map[string]any{
			accountOAuthProfileCredentialKey: CopilotOAuthProfile,
			"github_access_token":            "server-token",
		},
	}}
	svc := NewAccountService(accountRepo, &accountServiceCopilotGroupRepo{groups: map[int64]*Group{group.ID: group}})
	groupIDs := []int64{group.ID}
	credentials := map[string]any{"billing_username": "octocat"}

	updated, err := svc.Update(context.Background(), accountRepo.account.ID, UpdateAccountRequest{
		Credentials: &credentials,
		GroupIDs:    &groupIDs,
	})

	require.NoError(t, err)
	require.NotNil(t, updated)
	require.NotNil(t, accountRepo.updated)
	require.True(t, accountRepo.updated.IsGitHubCopilot())
	require.Equal(t, "server-token", accountRepo.updated.GetCredential("github_access_token"))
	require.Equal(t, "octocat", accountRepo.updated.GetCredential("billing_username"))
	require.Equal(t, []int64{group.ID}, accountRepo.boundIDs)
}
