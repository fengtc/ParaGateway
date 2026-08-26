//go:build unit

package service

import (
	"context"
	"errors"
	"sync"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/stretchr/testify/require"
)

type copilotStickyAccountRepo struct {
	AccountRepository
	accounts []Account
}

func (r *copilotStickyAccountRepo) GetByID(_ context.Context, id int64) (*Account, error) {
	for i := range r.accounts {
		if r.accounts[i].ID == id {
			return &r.accounts[i], nil
		}
	}
	return nil, errors.New("account not found")
}

func (r *copilotStickyAccountRepo) listByPlatforms(platforms []string) []Account {
	allowed := make(map[string]struct{}, len(platforms))
	for _, platform := range platforms {
		allowed[platform] = struct{}{}
	}
	result := make([]Account, 0, len(r.accounts))
	for _, account := range r.accounts {
		if _, ok := allowed[account.Platform]; ok && account.IsSchedulable() {
			result = append(result, account)
		}
	}
	return result
}

func (r *copilotStickyAccountRepo) ListSchedulableByPlatforms(_ context.Context, platforms []string) ([]Account, error) {
	return r.listByPlatforms(platforms), nil
}

func (r *copilotStickyAccountRepo) ListSchedulableByGroupIDAndPlatforms(_ context.Context, _ int64, platforms []string) ([]Account, error) {
	return r.listByPlatforms(platforms), nil
}

func (r *copilotStickyAccountRepo) ListSchedulableUngroupedByPlatforms(_ context.Context, platforms []string) ([]Account, error) {
	return r.listByPlatforms(platforms), nil
}

type copilotStickyGatewayCache struct {
	GatewayCache
	bindings map[string]int64
	setCalls []int64
}

func (c *copilotStickyGatewayCache) GetSessionAccountID(_ context.Context, _ int64, sessionHash string) (int64, error) {
	if accountID, ok := c.bindings[sessionHash]; ok {
		return accountID, nil
	}
	return 0, ErrStickySessionNotFound
}

func (c *copilotStickyGatewayCache) SetSessionAccountID(_ context.Context, _ int64, sessionHash string, accountID int64, _ time.Duration) error {
	if c.bindings == nil {
		c.bindings = make(map[string]int64)
	}
	c.bindings[sessionHash] = accountID
	c.setCalls = append(c.setCalls, accountID)
	return nil
}

func (c *copilotStickyGatewayCache) RefreshSessionTTL(_ context.Context, _ int64, _ string, _ time.Duration) error {
	return nil
}

func (c *copilotStickyGatewayCache) DeleteSessionAccountID(_ context.Context, _ int64, sessionHash string) error {
	delete(c.bindings, sessionHash)
	return nil
}

type copilotStickyConcurrencyCache struct {
	ConcurrencyCache
	mu             sync.Mutex
	acquireResults map[int64]bool
	loadMap        map[int64]*AccountLoadInfo
	freshLoadMap   map[int64]*AccountLoadInfo
	waitCounts     map[int64]int
	acquireCalls   []int64
	loadBatchCalls int
}

func (c *copilotStickyConcurrencyCache) AcquireAccountSlot(_ context.Context, accountID int64, _ int, _ string) (bool, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.acquireCalls = append(c.acquireCalls, accountID)
	acquired, ok := c.acquireResults[accountID]
	if !ok {
		return true, nil
	}
	return acquired, nil
}

func (c *copilotStickyConcurrencyCache) ReleaseAccountSlot(_ context.Context, _ int64, _ string) error {
	return nil
}

func (c *copilotStickyConcurrencyCache) GetAccountWaitingCount(_ context.Context, accountID int64) (int, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	return c.waitCounts[accountID], nil
}

func (c *copilotStickyConcurrencyCache) GetAccountsLoadBatch(_ context.Context, accounts []AccountWithConcurrency) (map[int64]*AccountLoadInfo, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.loadBatchCalls++
	loads := c.loadMap
	if c.loadBatchCalls > 1 && c.freshLoadMap != nil {
		loads = c.freshLoadMap
	}
	result := make(map[int64]*AccountLoadInfo, len(accounts))
	for _, account := range accounts {
		load := &AccountLoadInfo{AccountID: account.ID}
		if configured, ok := loads[account.ID]; ok {
			copy := *configured
			load = &copy
		}
		result[account.ID] = load
	}
	return result, nil
}

func copilotStickyAccount(id int64, priority int) Account {
	return Account{
		ID:          id,
		Platform:    PlatformOpenAI,
		Type:        AccountTypeOAuth,
		Status:      StatusActive,
		Schedulable: true,
		Concurrency: 1,
		Priority:    priority,
		Credentials: map[string]any{"oauth_profile": CopilotOAuthProfile},
	}
}

func newCopilotStickyService(accounts []Account, stickyID int64, concurrency *copilotStickyConcurrencyCache, loadBatch bool) (*GatewayService, *copilotStickyGatewayCache) {
	repo := &copilotStickyAccountRepo{accounts: accounts}
	cache := &copilotStickyGatewayCache{bindings: map[string]int64{"sticky-session": stickyID}}
	cfg := &config.Config{
		RunMode: config.RunModeStandard,
		Gateway: config.GatewayConfig{Scheduling: config.GatewaySchedulingConfig{
			LoadBatchEnabled:         loadBatch,
			StickySessionMaxWaiting:  3,
			StickySessionWaitTimeout: 2 * time.Minute,
			FallbackWaitTimeout:      30 * time.Second,
			FallbackMaxWaiting:       100,
		}},
	}
	return &GatewayService{
		accountRepo:        repo,
		cache:              cache,
		cfg:                cfg,
		concurrencyService: NewConcurrencyService(concurrency),
	}, cache
}

func TestGatewayService_CopilotStickyEscape(t *testing.T) {
	const sessionHash = "sticky-session"

	t.Run("available sticky keeps affinity", func(t *testing.T) {
		concurrency := &copilotStickyConcurrencyCache{
			acquireResults: map[int64]bool{1: true, 2: true},
		}
		svc, cache := newCopilotStickyService([]Account{
			copilotStickyAccount(1, 1),
			copilotStickyAccount(2, 1),
		}, 1, concurrency, true)

		selection, err := svc.SelectAccountWithLoadAwareness(context.Background(), nil, sessionHash, "", nil, "", 0)

		require.NoError(t, err)
		require.True(t, selection.Acquired)
		require.False(t, selection.PreserveStickyBinding)
		require.Equal(t, int64(1), selection.Account.ID)
		require.Equal(t, int64(1), cache.bindings[sessionHash])
		require.Empty(t, cache.setCalls)
		require.Equal(t, []int64{1}, concurrency.acquireCalls)
		selection.ReleaseFunc()
	})

	t.Run("busy sticky spills to free copilot without rebinding", func(t *testing.T) {
		concurrency := &copilotStickyConcurrencyCache{
			acquireResults: map[int64]bool{1: false, 2: true},
			loadMap: map[int64]*AccountLoadInfo{
				1: {AccountID: 1, CurrentConcurrency: 1, LoadRate: 100},
				2: {AccountID: 2, LoadRate: 0},
			},
		}
		svc, cache := newCopilotStickyService([]Account{
			copilotStickyAccount(1, 1),
			copilotStickyAccount(2, 1),
		}, 1, concurrency, true)

		selection, err := svc.SelectAccountWithLoadAwareness(context.Background(), nil, sessionHash, "", nil, "", 0)

		require.NoError(t, err)
		require.True(t, selection.Acquired)
		require.True(t, selection.PreserveStickyBinding)
		require.Equal(t, int64(2), selection.Account.ID)
		require.Equal(t, int64(1), cache.bindings[sessionHash])
		require.Empty(t, cache.setCalls)
		require.Equal(t, []int64{1, 2}, concurrency.acquireCalls)
		selection.ReleaseFunc()
	})

	t.Run("all copilot accounts full returns an overflow wait plan", func(t *testing.T) {
		concurrency := &copilotStickyConcurrencyCache{
			acquireResults: map[int64]bool{1: false, 2: false},
			loadMap: map[int64]*AccountLoadInfo{
				1: {AccountID: 1, CurrentConcurrency: 1, LoadRate: 100},
				2: {AccountID: 2, CurrentConcurrency: 1, LoadRate: 100},
			},
		}
		svc, cache := newCopilotStickyService([]Account{
			copilotStickyAccount(1, 2),
			copilotStickyAccount(2, 1),
		}, 1, concurrency, true)

		selection, err := svc.SelectAccountWithLoadAwareness(context.Background(), nil, sessionHash, "", nil, "", 0)

		require.NoError(t, err)
		require.False(t, selection.Acquired)
		require.NotNil(t, selection.WaitPlan)
		require.True(t, selection.PreserveStickyBinding)
		require.Equal(t, int64(2), selection.WaitPlan.AccountID)
		require.Equal(t, int64(1), cache.bindings[sessionHash])
		require.Empty(t, cache.setCalls)
	})

	t.Run("legacy scheduling also escapes without rebinding", func(t *testing.T) {
		concurrency := &copilotStickyConcurrencyCache{
			acquireResults: map[int64]bool{1: false, 2: true},
		}
		svc, cache := newCopilotStickyService([]Account{
			copilotStickyAccount(1, 1),
			copilotStickyAccount(2, 2),
		}, 1, concurrency, false)

		selection, err := svc.SelectAccountWithLoadAwareness(context.Background(), nil, sessionHash, "", nil, "", 0)

		require.NoError(t, err)
		require.True(t, selection.Acquired)
		require.True(t, selection.PreserveStickyBinding)
		require.Equal(t, int64(2), selection.Account.ID)
		require.Equal(t, int64(1), cache.bindings[sessionHash])
		require.Empty(t, cache.setCalls)
		require.Equal(t, []int64{1, 2}, concurrency.acquireCalls)
		selection.ReleaseFunc()
	})

	t.Run("non copilot sticky keeps existing wait behavior", func(t *testing.T) {
		concurrency := &copilotStickyConcurrencyCache{
			acquireResults: map[int64]bool{1: false, 2: true},
			waitCounts:     map[int64]int{1: 0},
		}
		anthropicAccount := func(id int64, priority int) Account {
			return Account{
				ID:          id,
				Platform:    PlatformAnthropic,
				Type:        AccountTypeOAuth,
				Status:      StatusActive,
				Schedulable: true,
				Concurrency: 1,
				Priority:    priority,
			}
		}
		svc, cache := newCopilotStickyService([]Account{
			anthropicAccount(1, 1),
			anthropicAccount(2, 2),
		}, 1, concurrency, true)

		selection, err := svc.SelectAccountWithLoadAwareness(context.Background(), nil, sessionHash, "", nil, "", 0)

		require.NoError(t, err)
		require.False(t, selection.Acquired)
		require.NotNil(t, selection.WaitPlan)
		require.False(t, selection.PreserveStickyBinding)
		require.Equal(t, int64(1), selection.WaitPlan.AccountID)
		require.Equal(t, int64(1), cache.bindings[sessionHash])
		require.Equal(t, []int64{1}, concurrency.acquireCalls)
	})

	t.Run("queue full retry context preserves the original binding", func(t *testing.T) {
		concurrency := &copilotStickyConcurrencyCache{
			acquireResults: map[int64]bool{2: true},
			loadMap: map[int64]*AccountLoadInfo{
				2: {AccountID: 2, LoadRate: 0},
			},
		}
		svc, cache := newCopilotStickyService([]Account{
			copilotStickyAccount(1, 1),
			copilotStickyAccount(2, 1),
		}, 1, concurrency, true)
		ctx := WithPreserveCopilotStickyBinding(context.Background())

		selection, err := svc.SelectAccountWithLoadAwareness(ctx, nil, sessionHash, "", map[int64]struct{}{1: {}}, "", 0)

		require.NoError(t, err)
		require.True(t, selection.Acquired)
		require.True(t, selection.PreserveStickyBinding)
		require.Equal(t, int64(2), selection.Account.ID)
		require.Equal(t, int64(1), cache.bindings[sessionHash])
		require.Empty(t, cache.setCalls)
		require.Equal(t, []int64{2}, concurrency.acquireCalls)
		selection.ReleaseFunc()
	})
}
