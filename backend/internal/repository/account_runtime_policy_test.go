package repository

import (
	"context"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/alicebob/miniredis/v2"
	"github.com/redis/go-redis/v9"
	"github.com/stretchr/testify/require"
)

func newRuntimePolicyStoreForTest(t *testing.T) (service.AccountRuntimePolicyStore, *miniredis.Miniredis) {
	t.Helper()
	server := miniredis.RunT(t)
	client := redis.NewClient(&redis.Options{Addr: server.Addr()})
	t.Cleanup(func() { _ = client.Close() })
	cache := NewRPMCache(client)
	store, ok := cache.(service.AccountRuntimePolicyStore)
	require.True(t, ok)
	return store, server
}

func TestAccountRuntimePolicyRPMHardGate(t *testing.T) {
	store, server := newRuntimePolicyStoreForTest(t)
	ctx := context.Background()

	first, err := store.TryAcquireAccountRequest(ctx, 42, 2, false, 0, 0)
	require.NoError(t, err)
	require.True(t, first.Allowed)
	second, err := store.TryAcquireAccountRequest(ctx, 42, 2, false, 0, 0)
	require.NoError(t, err)
	require.True(t, second.Allowed)
	blocked, err := store.TryAcquireAccountRequest(ctx, 42, 2, false, 0, 0)
	require.NoError(t, err)
	require.False(t, blocked.Allowed)
	require.Equal(t, service.AccountRuntimeGateRPMExceeded, blocked.Reason)
	require.Positive(t, blocked.RetryAfter)

	server.SetTime(time.Now().Add(61 * time.Second))
	afterWindow, err := store.TryAcquireAccountRequest(ctx, 42, 2, false, 0, 0)
	require.NoError(t, err)
	require.True(t, afterWindow.Allowed)
}

func TestAccountRuntimePolicyRPMHardGateIsAtomicUnderConcurrency(t *testing.T) {
	store, _ := newRuntimePolicyStoreForTest(t)
	ctx := context.Background()
	const limit = 10
	const attempts = 64
	var allowed atomic.Int32
	var wg sync.WaitGroup
	errCh := make(chan error, attempts)
	wg.Add(attempts)
	for i := 0; i < attempts; i++ {
		go func() {
			defer wg.Done()
			gate, err := store.TryAcquireAccountRequest(ctx, 420, limit, false, 0, 0)
			if err != nil {
				errCh <- err
				return
			}
			if gate.Allowed {
				allowed.Add(1)
			}
		}()
	}
	wg.Wait()
	close(errCh)
	for err := range errCh {
		require.NoError(t, err)
	}
	require.Equal(t, int32(limit), allowed.Load())
}

func TestAccountRuntimePolicyCircuitBreaker(t *testing.T) {
	store, server := newRuntimePolicyStoreForTest(t)
	ctx := context.Background()

	admission, err := store.TryAcquireAccountRequest(ctx, 7, 0, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.Positive(t, admission.Generation)
	require.NoError(t, store.RecordAccountResult(ctx, 7, admission.Generation, false, 2, 30*time.Second))
	open, _, err := store.IsAccountCircuitOpen(ctx, 7)
	require.NoError(t, err)
	require.False(t, open)

	require.NoError(t, store.RecordAccountResult(ctx, 7, admission.Generation, false, 2, 30*time.Second))
	gate, err := store.TryAcquireAccountRequest(ctx, 7, 0, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.False(t, gate.Allowed)
	require.Equal(t, service.AccountRuntimeGateCircuitOpen, gate.Reason)

	require.NoError(t, store.RecordAccountResult(ctx, 7, admission.Generation, true, 2, 30*time.Second))
	gate, err = store.TryAcquireAccountRequest(ctx, 7, 0, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.True(t, gate.Allowed)

	require.NoError(t, store.RecordAccountResult(ctx, 7, gate.Generation, false, 1, 30*time.Second))
	server.SetTime(time.Now().Add(31 * time.Second))
	gate, err = store.TryAcquireAccountRequest(ctx, 7, 0, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.True(t, gate.Allowed)
}

func TestAccountRuntimePolicyClearCircuitKeepsRPMWindow(t *testing.T) {
	store, _ := newRuntimePolicyStoreForTest(t)
	ctx := context.Background()

	admission, err := store.TryAcquireAccountRequest(ctx, 17, 2, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.NoError(t, store.RecordAccountResult(ctx, 17, admission.Generation, false, 1, 30*time.Second))
	open, _, err := store.IsAccountCircuitOpen(ctx, 17)
	require.NoError(t, err)
	require.True(t, open)

	require.NoError(t, store.ClearAccountCircuit(ctx, 17))
	open, _, err = store.IsAccountCircuitOpen(ctx, 17)
	require.NoError(t, err)
	require.False(t, open)
	// 清熔断不能清掉同一 key 中的 RPM 计数：第 2 次仍允许，第 3 次必须被限流。
	gate, err := store.TryAcquireAccountRequest(ctx, 17, 2, false, 0, 0)
	require.NoError(t, err)
	require.True(t, gate.Allowed)
	gate, err = store.TryAcquireAccountRequest(ctx, 17, 2, false, 0, 0)
	require.NoError(t, err)
	require.False(t, gate.Allowed)
	require.Equal(t, service.AccountRuntimeGateRPMExceeded, gate.Reason)
}

func TestAccountRuntimePolicyGenerationFencesStaleInFlightResult(t *testing.T) {
	store, _ := newRuntimePolicyStoreForTest(t)
	ctx := context.Background()

	oldAdmission, err := store.TryAcquireAccountRequest(ctx, 23, 0, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.Positive(t, oldAdmission.Generation)

	require.NoError(t, store.ClearAccountCircuit(ctx, 23))
	require.NoError(t, store.RecordAccountResult(ctx, 23, oldAdmission.Generation, false, 1, 30*time.Second))

	newAdmission, err := store.TryAcquireAccountRequest(ctx, 23, 0, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.True(t, newAdmission.Allowed)
	require.Positive(t, newAdmission.Generation)
	require.NotEqual(t, oldAdmission.Generation, newAdmission.Generation)

	require.NoError(t, store.RecordAccountResult(ctx, 23, newAdmission.Generation, false, 1, 30*time.Second))
	gate, err := store.TryAcquireAccountRequest(ctx, 23, 0, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.False(t, gate.Allowed)
	require.Equal(t, service.AccountRuntimeGateCircuitOpen, gate.Reason)
}

func TestAccountRuntimePolicyRPMOnlyIgnoresStaleCircuit(t *testing.T) {
	store, _ := newRuntimePolicyStoreForTest(t)
	ctx := context.Background()

	admission, err := store.TryAcquireAccountRequest(ctx, 29, 0, true, 30*time.Second, 0)
	require.NoError(t, err)
	require.NoError(t, store.RecordAccountResult(ctx, 29, admission.Generation, false, 1, 30*time.Second))

	gate, err := store.TryAcquireAccountRequest(ctx, 29, 10, false, 0, 0)
	require.NoError(t, err)
	require.True(t, gate.Allowed, "breaker-disabled RPM admission must ignore stale circuit state")
	open, _, err := store.IsAccountCircuitOpen(ctx, 29)
	require.NoError(t, err)
	require.False(t, open)
}
