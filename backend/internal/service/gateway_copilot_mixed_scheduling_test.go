//go:build unit

package service

import (
	"context"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/stretchr/testify/require"
)

type copilotMixedSchedulingRepo struct {
	AccountRepository
	accounts           []Account
	requestedGroupID   int64
	requestedPlatforms []string
}

func (r *copilotMixedSchedulingRepo) ListSchedulableByGroupIDAndPlatforms(_ context.Context, groupID int64, platforms []string) ([]Account, error) {
	r.requestedGroupID = groupID
	r.requestedPlatforms = append([]string(nil), platforms...)
	return append([]Account(nil), r.accounts...), nil
}

func (r *copilotMixedSchedulingRepo) ListSchedulableUngroupedByPlatforms(_ context.Context, platforms []string) ([]Account, error) {
	r.requestedPlatforms = append([]string(nil), platforms...)
	return append([]Account(nil), r.accounts...), nil
}

func githubCopilotSchedulingAccount(id int64, priority int) Account {
	return Account{
		ID:          id,
		Name:        "copilot",
		Platform:    PlatformOpenAI,
		Type:        AccountTypeOAuth,
		Priority:    priority,
		Status:      StatusActive,
		Schedulable: true,
		Credentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
	}
}

func TestGatewayListSchedulableAccounts_AnthropicMixedIncludesOnlyCopilotOpenAI(t *testing.T) {
	groupID := int64(77)
	copilot := githubCopilotSchedulingAccount(2, 1)
	repo := &copilotMixedSchedulingRepo{accounts: []Account{
		{ID: 1, Platform: PlatformAnthropic, Status: StatusActive, Schedulable: true},
		copilot,
		{ID: 3, Platform: PlatformOpenAI, Type: AccountTypeOAuth, Status: StatusActive, Schedulable: true,
			Credentials: map[string]any{"oauth_profile": "chatgpt"}},
	}}
	svc := &GatewayService{accountRepo: repo, cfg: &config.Config{RunMode: config.RunModeStandard}}

	accounts, mixed, err := svc.listSchedulableAccounts(context.Background(), &groupID, PlatformAnthropic, false)

	require.NoError(t, err)
	require.True(t, mixed)
	require.Equal(t, groupID, repo.requestedGroupID)
	require.ElementsMatch(t, []string{PlatformAnthropic, PlatformAntigravity, PlatformOpenAI}, repo.requestedPlatforms)
	require.Len(t, accounts, 2)
	require.Equal(t, []int64{1, 2}, []int64{accounts[0].ID, accounts[1].ID})
}

func TestMixedSchedulingPlatformAllowed_CopilotOnlyForAnthropic(t *testing.T) {
	copilot := githubCopilotSchedulingAccount(1, 1)
	regularOpenAI := copilot
	regularOpenAI.Credentials = map[string]any{"oauth_profile": "chatgpt"}
	antigravity := &Account{Platform: PlatformAntigravity, Extra: map[string]any{"mixed_scheduling": true}}

	require.True(t, isMixedSchedulingPlatformAllowed(&copilot, PlatformAnthropic))
	require.False(t, isMixedSchedulingPlatformAllowed(&copilot, PlatformGemini))
	require.False(t, isMixedSchedulingPlatformAllowed(&regularOpenAI, PlatformAnthropic))
	require.True(t, isMixedSchedulingPlatformAllowed(antigravity, PlatformAnthropic))
	require.True(t, isMixedSchedulingPlatformAllowed(antigravity, PlatformGemini))
	require.False(t, isMixedSchedulingPlatformAllowed(antigravity, PlatformOpenAI))
	require.False(t, (&GatewayService{}).isAccountAllowedForPlatform(&copilot, PlatformAnthropic, false))
}

func TestSchedulerSnapshotLoad_AnthropicMixedIncludesOnlyCopilotOpenAI(t *testing.T) {
	groupID := int64(88)
	copilot := githubCopilotSchedulingAccount(2, 1)
	repo := &copilotMixedSchedulingRepo{accounts: []Account{
		{ID: 1, Platform: PlatformAnthropic, Status: StatusActive, Schedulable: true},
		copilot,
		{ID: 3, Platform: PlatformOpenAI, Type: AccountTypeOAuth, Status: StatusActive, Schedulable: true,
			Credentials: map[string]any{"oauth_profile": "chatgpt"}},
	}}
	svc := NewSchedulerSnapshotService(nil, nil, repo, nil, &config.Config{RunMode: config.RunModeStandard})

	accounts, err := svc.loadAccountsFromDB(context.Background(), SchedulerBucket{
		GroupID:  groupID,
		Platform: PlatformAnthropic,
		Mode:     SchedulerModeMixed,
	}, true)

	require.NoError(t, err)
	require.Equal(t, groupID, repo.requestedGroupID)
	require.ElementsMatch(t, []string{PlatformAnthropic, PlatformAntigravity, PlatformOpenAI}, repo.requestedPlatforms)
	require.Len(t, accounts, 2)
	require.Equal(t, []int64{1, 2}, []int64{accounts[0].ID, accounts[1].ID})
}

func TestGatewayMixedScheduling_BillingExhaustedCopilotIsSkipped(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)

	copilot := githubCopilotSchedulingAccount(2, 1)
	require.True(t, markCopilotBillingGuardExhausted(&copilot))
	repo := &copilotMixedSchedulingRepo{accounts: []Account{
		{ID: 1, Platform: PlatformAnthropic, Priority: 2, Status: StatusActive, Schedulable: true},
		copilot,
	}}
	svc := &GatewayService{accountRepo: repo, cfg: &config.Config{RunMode: config.RunModeStandard}}

	selected, err := svc.selectAccountWithMixedScheduling(context.Background(), nil, "", "", nil, PlatformAnthropic)

	require.NoError(t, err)
	require.NotNil(t, selected)
	require.Equal(t, int64(1), selected.ID)
}

func TestGatewaySnapshotCopilotIsHydratedBeforeBillingGuard(t *testing.T) {
	metadata := githubCopilotSchedulingAccount(9, 1)
	full := metadata
	full.Credentials = map[string]any{
		"oauth_profile":    CopilotOAuthProfile,
		"billing_username": "octocat",
		"billing_pat":      "secret-pat",
	}
	full.Extra = map[string]any{"billing_auto_pause_disabled": true}
	cache := &openAISnapshotCacheStub{
		snapshotAccounts: []*Account{&metadata},
		accountsByID:     map[int64]*Account{full.ID: &full},
		getAccountErrors: map[int64]error{},
	}
	cfg := &config.Config{RunMode: config.RunModeStandard}
	snapshot := NewSchedulerSnapshotService(cache, nil, &copilotMixedSchedulingRepo{}, nil, cfg)
	svc := &GatewayService{schedulerSnapshot: snapshot, cfg: cfg}

	accounts, mixed, err := svc.listSchedulableAccounts(context.Background(), nil, PlatformAnthropic, false)

	require.NoError(t, err)
	require.True(t, mixed)
	require.Len(t, accounts, 1)
	require.Equal(t, "secret-pat", accounts[0].GetCredential("billing_pat"))
}

func TestGatewayCopilotBillingGuardHonorsCurrentMonthAuthoritativeStop(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)
	copilot := githubCopilotSchedulingAccount(10, 1)
	now := time.Now().UTC()
	copilotBillingGuardCache.Store(copilotBillingGuardAuthoritativeKey(copilot.ID, now), copilotBillingGuardCacheEntry{
		usedCredits:   DefaultCopilotBillingCreditLimit,
		expiresAt:     now.Add(time.Hour),
		forceSkip:     true,
		authoritative: true,
	})

	svc := &GatewayService{}
	require.False(t, svc.isAccountSchedulableForQuota(context.Background(), &copilot))
}
