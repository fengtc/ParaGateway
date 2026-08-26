//go:build unit

package service

import (
	"context"
	"net/http"
	"testing"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"github.com/stretchr/testify/require"
)

type copilotCreateGroupRepo struct {
	*groupRepoStubForAdmin
	listPlatform string
	groups       []Group
}

func (r *copilotCreateGroupRepo) ListActiveByPlatform(_ context.Context, platform string) ([]Group, error) {
	r.listPlatform = platform
	return append([]Group(nil), r.groups...), nil
}

func TestValidateGitHubCopilotGroupPlatform(t *testing.T) {
	for _, platform := range []string{PlatformOpenAI, PlatformAnthropic, " ANTHROPIC "} {
		require.NoError(t, ValidateGitHubCopilotGroupPlatform(platform))
	}
	for _, platform := range []string{PlatformGemini, PlatformGrok, PlatformComposite, ""} {
		var appErr *infraerrors.ApplicationError
		err := ValidateGitHubCopilotGroupPlatform(platform)
		require.ErrorAs(t, err, &appErr)
		require.Equal(t, "COPILOT_GROUP_PLATFORM_MISMATCH", appErr.Reason)
	}
}

func TestNormalizeGitHubCopilotIdentityCanonical(t *testing.T) {
	tests := []struct {
		name     string
		platform string
		typeName string
		profile  any
		want     bool
		wantErr  bool
	}{
		{name: "normalized identity", platform: PlatformOpenAI, typeName: AccountTypeOAuth, profile: CopilotOAuthProfile, want: true},
		{name: "case and whitespace normalized", platform: " OPENAI ", typeName: " OAUTH ", profile: " GitHub_Copilot ", want: true},
		{name: "wrong platform rejected", platform: PlatformAnthropic, typeName: AccountTypeOAuth, profile: CopilotOAuthProfile, wantErr: true},
		{name: "wrong type rejected", platform: PlatformOpenAI, typeName: AccountTypeAPIKey, profile: CopilotOAuthProfile, wantErr: true},
		{name: "another OAuth profile ignored", platform: PlatformOpenAI, typeName: AccountTypeOAuth, profile: "chatgpt"},
		{name: "non string profile ignored", platform: PlatformOpenAI, typeName: AccountTypeOAuth, profile: map[string]any{"name": CopilotOAuthProfile}},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			originalCredentials := map[string]any{accountOAuthProfileCredentialKey: tt.profile}

			platform, accountType, credentials, got, err := NormalizeGitHubCopilotIdentity(
				tt.platform,
				tt.typeName,
				originalCredentials,
			)

			require.Equal(t, tt.want, got)
			if tt.wantErr {
				require.Equal(t, http.StatusBadRequest, infraerrors.Code(err))
				requireApplicationErrorReason(t, err, "COPILOT_IDENTITY_INVALID")
				return
			}
			require.NoError(t, err)
			if tt.want {
				require.Equal(t, PlatformOpenAI, platform)
				require.Equal(t, AccountTypeOAuth, accountType)
				require.Equal(t, CopilotOAuthProfile, credentials[accountOAuthProfileCredentialKey])
			}
			require.Equal(t, tt.profile, originalCredentials[accountOAuthProfileCredentialKey])
		})
	}
}

func TestNormalizeGitHubCopilotIdentityLegacy(t *testing.T) {
	t.Run("github_token shape becomes canonical OAuth", func(t *testing.T) {
		originalCredentials := map[string]any{
			"github_token":     "  legacy-token  ",
			"billing_username": "octocat",
		}

		platform, accountType, credentials, got, err := NormalizeGitHubCopilotIdentity(
			" CoPiLoT ",
			" APIKEY ",
			originalCredentials,
		)

		require.NoError(t, err)
		require.True(t, got)
		require.Equal(t, PlatformOpenAI, platform)
		require.Equal(t, AccountTypeOAuth, accountType)
		require.Equal(t, CopilotOAuthProfile, credentials[accountOAuthProfileCredentialKey])
		require.Equal(t, "legacy-token", credentials["github_access_token"])
		require.NotContains(t, credentials, "github_token")
		require.Equal(t, "octocat", credentials["billing_username"])
		require.Equal(t, "  legacy-token  ", originalCredentials["github_token"])
		require.NotContains(t, originalCredentials, "github_access_token")
		require.NotContains(t, originalCredentials, accountOAuthProfileCredentialKey)
	})

	t.Run("github_access_token-only shape becomes canonical OAuth", func(t *testing.T) {
		credentials := map[string]any{
			"github_access_token": "  canonical-token  ",
		}

		platform, accountType, normalized, got, err := NormalizeGitHubCopilotIdentity(
			legacyGitHubCopilotPlatform,
			AccountTypeAPIKey,
			credentials,
		)

		require.NoError(t, err)
		require.True(t, got)
		require.Equal(t, PlatformOpenAI, platform)
		require.Equal(t, AccountTypeOAuth, accountType)
		require.Equal(t, "canonical-token", normalized["github_access_token"])
		require.Equal(t, "  canonical-token  ", credentials["github_access_token"])
	})

	t.Run("matching token aliases are accepted after trimming", func(t *testing.T) {
		original := map[string]any{
			"github_token":        " token ",
			"github_access_token": "token",
		}

		_, _, credentials, got, err := NormalizeGitHubCopilotIdentity(
			legacyGitHubCopilotPlatform,
			AccountTypeAPIKey,
			original,
		)

		require.NoError(t, err)
		require.True(t, got)
		require.Equal(t, "token", credentials["github_access_token"])
		require.NotContains(t, credentials, "github_token")
		require.Contains(t, original, "github_token")
	})

	t.Run("conflicting token aliases are rejected", func(t *testing.T) {
		original := map[string]any{
			"github_token":        "legacy-token",
			"github_access_token": "canonical-token",
		}

		_, _, normalized, got, err := NormalizeGitHubCopilotIdentity(
			legacyGitHubCopilotPlatform,
			AccountTypeAPIKey,
			original,
		)

		require.False(t, got)
		require.Equal(t, http.StatusBadRequest, infraerrors.Code(err))
		requireApplicationErrorReason(t, err, "COPILOT_GITHUB_TOKEN_CONFLICT")
		require.Equal(t, original, normalized)
		require.NotContains(t, err.Error(), "legacy-token")
		require.NotContains(t, err.Error(), "canonical-token")
	})

	for _, tt := range []struct {
		name        string
		typeName    string
		credentials map[string]any
		reason      string
	}{
		{name: "missing credentials", typeName: AccountTypeAPIKey, reason: "COPILOT_GITHUB_TOKEN_REQUIRED"},
		{name: "missing token", typeName: AccountTypeAPIKey, credentials: map[string]any{}, reason: "COPILOT_GITHUB_TOKEN_REQUIRED"},
		{name: "wrong legacy type", typeName: AccountTypeOAuth, credentials: map[string]any{"github_token": "token"}, reason: "COPILOT_IDENTITY_INVALID"},
	} {
		t.Run(tt.name, func(t *testing.T) {
			_, _, _, got, err := NormalizeGitHubCopilotIdentity(
				legacyGitHubCopilotPlatform,
				tt.typeName,
				tt.credentials,
			)

			require.False(t, got)
			require.Equal(t, http.StatusBadRequest, infraerrors.Code(err))
			requireApplicationErrorReason(t, err, tt.reason)
		})
	}
}

func TestAdminServiceCreateNormalizesCopilotBeforeDefaultGroupLookup(t *testing.T) {
	group := Group{ID: 71, Name: "copilot-default", Platform: PlatformOpenAI, GitHubCopilotOnly: true}
	groupRepo := &copilotCreateGroupRepo{
		groupRepoStubForAdmin: &groupRepoStubForAdmin{
			getByIDByID: map[int64]*Group{group.ID: &group},
		},
		groups: []Group{group},
	}
	accountRepo := &accountRepoStubForBulkUpdate{createID: 72}
	svc := &adminServiceImpl{accountRepo: accountRepo, groupRepo: groupRepo}

	created, err := svc.CreateAccount(context.Background(), &CreateAccountInput{
		Name:     "normalized-copilot",
		Platform: " OPENAI ",
		Type:     " OAUTH ",
		Credentials: map[string]any{
			accountOAuthProfileCredentialKey: " GitHub_Copilot ",
			"access_token":                   "test-token",
		},
		SkipMixedChannelCheck: true,
	})

	require.NoError(t, err)
	require.NotNil(t, created)
	require.Equal(t, PlatformOpenAI, groupRepo.listPlatform)
	require.Equal(t, PlatformOpenAI, created.Platform)
	require.Equal(t, AccountTypeOAuth, created.Type)
	require.Equal(t, CopilotOAuthProfile, created.GetCredential(accountOAuthProfileCredentialKey))
	require.Equal(t, []int64{group.ID}, accountRepo.bindGroupsByAccount[created.ID])
}

func TestAdminServiceCreateNormalizesLegacyCopilotBeforeDefaultGroupLookup(t *testing.T) {
	group := Group{ID: 73, Name: "copilot-default", Platform: PlatformOpenAI, GitHubCopilotOnly: true}
	groupRepo := &copilotCreateGroupRepo{
		groupRepoStubForAdmin: &groupRepoStubForAdmin{
			getByIDByID: map[int64]*Group{group.ID: &group},
		},
		groups: []Group{group},
	}
	accountRepo := &accountRepoStubForBulkUpdate{createID: 74}
	svc := &adminServiceImpl{accountRepo: accountRepo, groupRepo: groupRepo}

	created, err := svc.CreateAccount(context.Background(), &CreateAccountInput{
		Name:     "legacy-copilot",
		Platform: legacyGitHubCopilotPlatform,
		Type:     AccountTypeAPIKey,
		Credentials: map[string]any{
			"github_token":     "legacy-github-token",
			"billing_username": "octocat",
		},
		SkipMixedChannelCheck: true,
	})

	require.NoError(t, err)
	require.NotNil(t, created)
	require.Equal(t, PlatformOpenAI, groupRepo.listPlatform)
	require.Equal(t, PlatformOpenAI, created.Platform)
	require.Equal(t, AccountTypeOAuth, created.Type)
	require.Equal(t, CopilotOAuthProfile, created.GetCredential(accountOAuthProfileCredentialKey))
	require.Equal(t, "legacy-github-token", created.GetCredential("github_access_token"))
	require.Empty(t, created.GetCredential("github_token"))
	require.Equal(t, "octocat", created.GetCredential("billing_username"))
	require.Equal(t, []int64{group.ID}, accountRepo.bindGroupsByAccount[created.ID])
}

func TestAdminServiceCreateCopilotDoesNotFallBackToOrdinaryOpenAIDefault(t *testing.T) {
	ordinaryDefault := Group{ID: 76, Name: "openai-default", Platform: PlatformOpenAI}
	groupRepo := &copilotCreateGroupRepo{
		groupRepoStubForAdmin: &groupRepoStubForAdmin{},
		groups:                []Group{ordinaryDefault},
	}
	accountRepo := &accountRepoStubForBulkUpdate{createID: 77}
	svc := &adminServiceImpl{accountRepo: accountRepo, groupRepo: groupRepo}

	created, err := svc.CreateAccount(context.Background(), &CreateAccountInput{
		Name:     "copilot-without-dedicated-default",
		Platform: PlatformOpenAI,
		Type:     AccountTypeOAuth,
		Credentials: map[string]any{
			accountOAuthProfileCredentialKey: CopilotOAuthProfile,
			"github_access_token":            "test-token",
		},
		SkipMixedChannelCheck: true,
	})

	require.NoError(t, err)
	require.NotNil(t, created)
	require.Equal(t, PlatformOpenAI, groupRepo.listPlatform)
	require.Empty(t, accountRepo.bindGroupsByAccount[created.ID])
}

func TestAdminServiceCreateRejectsConflictingLegacyCopilotTokensBeforeWrite(t *testing.T) {
	accountRepo := &accountRepoStubForBulkUpdate{createID: 75}
	svc := &adminServiceImpl{accountRepo: accountRepo}
	originalCredentials := map[string]any{
		"github_token":        "legacy-token",
		"github_access_token": "canonical-token",
	}

	created, err := svc.CreateAccount(context.Background(), &CreateAccountInput{
		Name:        "legacy-copilot-conflict",
		Platform:    legacyGitHubCopilotPlatform,
		Type:        AccountTypeAPIKey,
		Credentials: originalCredentials,
	})

	require.Nil(t, created)
	require.Equal(t, http.StatusBadRequest, infraerrors.Code(err))
	requireApplicationErrorReason(t, err, "COPILOT_GITHUB_TOKEN_CONFLICT")
	require.Nil(t, accountRepo.createAccount)
	require.Equal(t, "legacy-token", originalCredentials["github_token"])
	require.Equal(t, "canonical-token", originalCredentials["github_access_token"])
}

func TestAdminServiceUpdateCopilotGroupCompatibility(t *testing.T) {
	groupIDs := []int64{2}
	account := &Account{
		ID:       9,
		Platform: PlatformOpenAI,
		Type:     AccountTypeOAuth,
		Status:   StatusActive,
		Credentials: map[string]any{
			"oauth_profile": CopilotOAuthProfile,
		},
	}
	repo := &accountRepoStubForBulkUpdate{getByIDAccounts: map[int64]*Account{9: account}}
	svc := &adminServiceImpl{
		accountRepo: repo,
		groupRepo: &groupRepoStubForAdmin{getByIDByID: map[int64]*Group{
			2: {ID: 2, Platform: PlatformOpenAI, GitHubCopilotOnly: true},
		}},
	}

	_, err := svc.UpdateAccount(context.Background(), 9, &UpdateAccountInput{GroupIDs: &groupIDs})
	require.NoError(t, err)
	require.Equal(t, []int64{9}, repo.bindGroupsCalls)
}

func TestAdminServiceCreateRejectsRegularAccountInCopilotOnlyGroupBeforeWrite(t *testing.T) {
	groupIDs := []int64{21}
	accountRepo := &accountRepoStubForBulkUpdate{createID: 22}
	svc := &adminServiceImpl{
		accountRepo: accountRepo,
		groupRepo: &groupRepoStubForAdmin{getByIDByID: map[int64]*Group{
			21: {ID: 21, Platform: PlatformOpenAI, GitHubCopilotOnly: true},
		}},
	}

	created, err := svc.CreateAccount(context.Background(), &CreateAccountInput{
		Name:                  "ordinary-openai",
		Platform:              PlatformOpenAI,
		Type:                  AccountTypeAPIKey,
		Credentials:           map[string]any{"api_key": "ordinary-key"},
		GroupIDs:              groupIDs,
		SkipMixedChannelCheck: true,
		SkipDefaultGroupBind:  true,
	})

	require.Nil(t, created)
	requireApplicationErrorReason(t, err, "COPILOT_ONLY_GROUP_ACCOUNT_MISMATCH")
	require.Nil(t, accountRepo.createAccount)
	require.Empty(t, accountRepo.bindGroupsCalls)
}

func TestAdminServiceUpdateRejectsRegularAccountInCopilotOnlyGroupBeforeWrite(t *testing.T) {
	groupIDs := []int64{31}
	account := &Account{ID: 32, Platform: PlatformOpenAI, Type: AccountTypeAPIKey, Status: StatusActive}
	accountRepo := &accountRepoStubForBulkUpdate{getByIDAccounts: map[int64]*Account{account.ID: account}}
	svc := &adminServiceImpl{
		accountRepo: accountRepo,
		groupRepo: &groupRepoStubForAdmin{getByIDByID: map[int64]*Group{
			31: {ID: 31, Platform: PlatformOpenAI, GitHubCopilotOnly: true},
		}},
	}

	updated, err := svc.UpdateAccount(context.Background(), account.ID, &UpdateAccountInput{
		GroupIDs:              &groupIDs,
		SkipMixedChannelCheck: true,
	})

	require.Nil(t, updated)
	requireApplicationErrorReason(t, err, "COPILOT_ONLY_GROUP_ACCOUNT_MISMATCH")
	require.Empty(t, accountRepo.updatedAccounts)
	require.Empty(t, accountRepo.bindGroupsCalls)
}

func TestAdminServiceBulkUpdateRejectsRegularAccountInCopilotOnlyGroupBeforeWrite(t *testing.T) {
	groupIDs := []int64{41}
	accountRepo := &accountRepoStubForBulkUpdate{getByIDsAccounts: []*Account{
		{ID: 42, Platform: PlatformOpenAI, Type: AccountTypeOAuth, Credentials: map[string]any{"oauth_profile": "chatgpt"}},
	}}
	svc := &adminServiceImpl{
		accountRepo: accountRepo,
		groupRepo: &groupRepoStubForAdmin{getByIDByID: map[int64]*Group{
			41: {ID: 41, Platform: PlatformOpenAI, GitHubCopilotOnly: true},
		}},
	}

	result, err := svc.BulkUpdateAccounts(context.Background(), &BulkUpdateAccountsInput{
		AccountIDs:            []int64{42},
		GroupIDs:              &groupIDs,
		SkipMixedChannelCheck: true,
	})

	require.Nil(t, result)
	requireApplicationErrorReason(t, err, "COPILOT_ONLY_GROUP_ACCOUNT_MISMATCH")
	require.Zero(t, accountRepo.bulkUpdateCalls)
	require.Empty(t, accountRepo.bindGroupsCalls)
}

func TestAdminServiceUpdateRejectsOAuthProfileIdentityConversion(t *testing.T) {
	tests := []struct {
		name               string
		accountType        string
		accountCredentials map[string]any
		inputType          string
		inputCredentials   map[string]any
	}{
		{
			name:               "regular OpenAI OAuth cannot become Copilot",
			accountType:        AccountTypeOAuth,
			accountCredentials: map[string]any{"access_token": "regular-token"},
			inputCredentials:   map[string]any{"oauth_profile": CopilotOAuthProfile},
		},
		{
			name:               "Copilot cannot become another OAuth profile",
			accountType:        AccountTypeOAuth,
			accountCredentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
			inputCredentials:   map[string]any{"oauth_profile": "chatgpt"},
		},
		{
			name:               "Copilot cannot leave OAuth by changing account type",
			accountType:        AccountTypeOAuth,
			accountCredentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
			inputType:          AccountTypeAPIKey,
		},
		{
			name:               "stored Copilot profile cannot become active through account type change",
			accountType:        AccountTypeAPIKey,
			accountCredentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
			inputType:          AccountTypeOAuth,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			account := &Account{
				ID:          10,
				Platform:    PlatformOpenAI,
				Type:        tt.accountType,
				Status:      StatusActive,
				Credentials: tt.accountCredentials,
			}
			repo := &accountRepoStubForBulkUpdate{getByIDAccounts: map[int64]*Account{10: account}}
			svc := &adminServiceImpl{accountRepo: repo}

			updated, err := svc.UpdateAccount(context.Background(), 10, &UpdateAccountInput{
				Type:        tt.inputType,
				Credentials: tt.inputCredentials,
			})

			require.Nil(t, updated)
			require.Equal(t, http.StatusBadRequest, infraerrors.Code(err))
			requireApplicationErrorReason(t, err, "OAUTH_PROFILE_IMMUTABLE")
			require.Empty(t, repo.updatedAccounts)
			require.Empty(t, repo.bindGroupsCalls)
		})
	}
}

func TestAdminServiceUpdateAllowsCopilotBillingAndModelMapping(t *testing.T) {
	account := canonicalCopilotAccount(11)
	repo := &accountRepoStubForBulkUpdate{getByIDAccounts: map[int64]*Account{11: account}}
	svc := &adminServiceImpl{accountRepo: repo}

	updated, err := svc.UpdateAccount(context.Background(), 11, &UpdateAccountInput{
		Credentials: map[string]any{
			"billing_username": "octocat",
			"billing_pat":      "billing-pat-test",
			"model_mapping":    map[string]any{"claude-*": "gpt-4.1"},
		},
	})

	require.NoError(t, err)
	require.NotNil(t, updated)
	require.Len(t, repo.updatedAccounts, 1)
	require.Equal(t, CopilotOAuthProfile, updated.GetCredential("oauth_profile"))
	require.Equal(t, "octocat", updated.GetCredential("billing_username"))
	require.Equal(t, "billing-pat-test", updated.GetCredential("billing_pat"))
	require.Equal(t, map[string]any{"claude-*": "gpt-4.1"}, updated.Credentials["model_mapping"])
}

func TestAdminServiceUpdateRejectsManagedCopilotCredentialsBeforeWrite(t *testing.T) {
	values := map[string]any{
		"oauth_profile":       CopilotOAuthProfile,
		"access_token":        "replacement-token",
		"github_access_token": "replacement-github-token",
		"github_login":        "replacement-login",
		"github_user_id":      "replacement-user-id",
		"base_url":            "https://replacement.example",
		"expires_at":          "2026-08-27T00:00:00Z",
		"refresh_at":          "2026-08-26T23:30:00Z",
	}

	for key, value := range values {
		t.Run(key, func(t *testing.T) {
			account := canonicalCopilotAccount(12)
			repo := &accountRepoStubForBulkUpdate{getByIDAccounts: map[int64]*Account{12: account}}
			svc := &adminServiceImpl{accountRepo: repo}

			updated, err := svc.UpdateAccount(context.Background(), 12, &UpdateAccountInput{
				Credentials: map[string]any{key: value},
			})

			require.Nil(t, updated)
			requireApplicationErrorReason(t, err, "COPILOT_CREDENTIAL_MANAGED")
			require.Empty(t, repo.updatedAccounts)
			require.Empty(t, repo.bindGroupsCalls)
		})
	}
}

func TestAdminServiceInternalRefreshCanRotateManagedCopilotToken(t *testing.T) {
	account := canonicalCopilotAccount(13)
	repo := &accountRepoStubForBulkUpdate{getByIDAccounts: map[int64]*Account{13: account}}
	svc := &adminServiceImpl{accountRepo: repo}

	updated, err := svc.UpdateAccount(context.Background(), 13, &UpdateAccountInput{
		AllowManagedCredentialUpdate: true,
		Credentials: map[string]any{
			"oauth_profile":       CopilotOAuthProfile,
			"access_token":        "new-short-lived-token",
			"github_access_token": "github-token",
			"base_url":            CopilotAPIBaseURL,
			"expires_at":          "2026-08-27T00:00:00Z",
			"refresh_at":          "2026-08-26T23:30:00Z",
		},
	})

	require.NoError(t, err)
	require.Equal(t, "new-short-lived-token", updated.GetCredential("access_token"))
	require.Equal(t, CopilotOAuthProfile, updated.GetCredential("oauth_profile"))
	require.Len(t, repo.updatedAccounts, 1)
}

func TestAdminServiceBulkUpdateRejectsOAuthProfileWithoutGroupUpdate(t *testing.T) {
	repo := &accountRepoStubForBulkUpdate{}
	svc := &adminServiceImpl{accountRepo: repo}

	result, err := svc.BulkUpdateAccounts(context.Background(), &BulkUpdateAccountsInput{
		AccountIDs:  []int64{12},
		Credentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
	})

	require.Nil(t, result)
	require.Equal(t, http.StatusBadRequest, infraerrors.Code(err))
	requireApplicationErrorReason(t, err, "OAUTH_PROFILE_BULK_UPDATE_UNSUPPORTED")
	require.True(t, repo.getByIDsCalled)
	require.Zero(t, repo.bulkUpdateCalls)
	require.Empty(t, repo.bindGroupsCalls)
}

func TestAdminServiceBulkUpdateRejectsManagedCopilotCredentialsBeforeWrite(t *testing.T) {
	for _, key := range githubCopilotServerCredentialKeys {
		t.Run(key, func(t *testing.T) {
			repo := &accountRepoStubForBulkUpdate{getByIDsAccounts: []*Account{
				canonicalCopilotAccount(14),
				{ID: 15, Platform: PlatformAnthropic, Type: AccountTypeAPIKey},
			}}
			svc := &adminServiceImpl{accountRepo: repo}
			value := any("replacement")
			if key == accountOAuthProfileCredentialKey {
				value = CopilotOAuthProfile
			}

			result, err := svc.BulkUpdateAccounts(context.Background(), &BulkUpdateAccountsInput{
				AccountIDs:  []int64{14, 15},
				Credentials: map[string]any{key: value},
			})

			require.Nil(t, result)
			requireApplicationErrorReason(t, err, "COPILOT_CREDENTIAL_MANAGED")
			require.True(t, repo.getByIDsCalled)
			require.Zero(t, repo.bulkUpdateCalls)
			require.Empty(t, repo.bindGroupsCalls)
		})
	}
}

func canonicalCopilotAccount(id int64) *Account {
	return &Account{
		ID:       id,
		Platform: PlatformOpenAI,
		Type:     AccountTypeOAuth,
		Status:   StatusActive,
		Credentials: map[string]any{
			"oauth_profile":       CopilotOAuthProfile,
			"access_token":        "old-short-lived-token",
			"github_access_token": "github-token",
			"base_url":            CopilotAPIBaseURL,
		},
	}
}

func TestAdminServiceBulkUpdateRejectsIncompatibleCopilotGroupBeforeWrite(t *testing.T) {
	groupIDs := []int64{3}
	repo := &accountRepoStubForBulkUpdate{getByIDsAccounts: []*Account{{
		ID:       9,
		Platform: PlatformOpenAI,
		Type:     AccountTypeOAuth,
		Credentials: map[string]any{
			"oauth_profile": CopilotOAuthProfile,
		},
	}}}
	svc := &adminServiceImpl{
		accountRepo: repo,
		groupRepo: &groupRepoStubForAdmin{getByIDByID: map[int64]*Group{
			3: {ID: 3, Platform: PlatformGemini},
		}},
	}

	result, err := svc.BulkUpdateAccounts(context.Background(), &BulkUpdateAccountsInput{
		AccountIDs:            []int64{9},
		GroupIDs:              &groupIDs,
		SkipMixedChannelCheck: true,
	})
	require.Nil(t, result)
	require.Error(t, err)
	require.Zero(t, repo.bulkUpdateCalls)
	require.Empty(t, repo.bindGroupsCalls)
}
