package service

import (
	mathrand "math/rand"
	"testing"
	"time"

	"github.com/stretchr/testify/require"
)

func TestSelectByLRUUsesIndependentAccountWeight(t *testing.T) {
	mathrand.Seed(20260815)
	accounts := []accountWithLoad{
		{account: &Account{ID: 1, Priority: 10, Weight: 1}},
		{account: &Account{ID: 2, Priority: 10, Weight: 9}},
	}
	counts := map[int64]int{}
	for i := 0; i < 5000; i++ {
		selected := selectByLRU(accounts, false)
		require.NotNil(t, selected)
		counts[selected.account.ID]++
	}
	require.Greater(t, counts[int64(2)], counts[int64(1)]*5)
}

func TestGenericLoadAwareWeightAppliesBeforeLoadAndLRU(t *testing.T) {
	mathrand.Seed(20260815)
	accounts := []accountWithLoad{
		{account: &Account{ID: 1, Priority: 10, Weight: 1}, loadInfo: &AccountLoadInfo{LoadRate: 0}},
		{account: &Account{ID: 2, Priority: 10, Weight: 9}, loadInfo: &AccountLoadInfo{LoadRate: 90}},
		{account: &Account{ID: 3, Priority: 11, Weight: 1_000_000}, loadInfo: &AccountLoadInfo{LoadRate: 0}},
	}
	counts := map[int64]int{}
	for i := 0; i < 5000; i++ {
		selected := selectByWeight(filterByMinPriority(accounts))
		require.NotNil(t, selected)
		counts[selected.account.ID]++
	}
	require.Greater(t, counts[int64(2)], counts[int64(1)]*5)
	require.Zero(t, counts[int64(3)], "higher priority number must never be promoted by weight")
}

func TestGenericLoadAwareEqualWeightPreservesLowestLoad(t *testing.T) {
	now := time.Now()
	older := now.Add(-time.Hour)
	accounts := []accountWithLoad{
		{account: &Account{ID: 1, Priority: 10, Weight: 100, LastUsedAt: &now}, loadInfo: &AccountLoadInfo{LoadRate: 0}},
		{account: &Account{ID: 2, Priority: 10, Weight: 100, LastUsedAt: &older}, loadInfo: &AccountLoadInfo{LoadRate: 90}},
	}

	selected := selectGenericLoadAwareCandidate(accounts, false)
	require.NotNil(t, selected)
	require.Equal(t, int64(1), selected.account.ID, "equal weights must preserve lowest-load selection")
}

func TestGenericLoadAwareEqualWeightPreservesLRU(t *testing.T) {
	now := time.Now()
	older := now.Add(-time.Hour)
	accounts := []accountWithLoad{
		{account: &Account{ID: 1, Priority: 10, Weight: 100, LastUsedAt: &now}, loadInfo: &AccountLoadInfo{LoadRate: 20}},
		{account: &Account{ID: 2, Priority: 10, Weight: 100, LastUsedAt: &older}, loadInfo: &AccountLoadInfo{LoadRate: 20}},
	}

	selected := selectGenericLoadAwareCandidate(accounts, false)
	require.NotNil(t, selected)
	require.Equal(t, int64(2), selected.account.ID, "equal weights must preserve least-recently-used selection")
}

func TestOpenAIHardLayerEqualWeightPreservesLegacyFirst(t *testing.T) {
	now := time.Now()
	older := now.Add(-time.Hour)
	ordered := []*Account{
		{ID: 1, Priority: 10, Weight: 100, LastUsedAt: &older},
		{ID: 2, Priority: 10, Weight: 100, LastUsedAt: &now},
	}

	selected := selectOpenAIHardLayerByWeightOrFirst(ordered, nil, false, openAILegacyUpstreamRateOrder{})
	require.NotNil(t, selected)
	require.Equal(t, int64(1), selected.ID)
}

func TestOpenAIHardLayerWeightDistribution(t *testing.T) {
	mathrand.Seed(20260815)
	ordered := []*Account{
		{ID: 1, Priority: 10, Weight: 1},
		{ID: 2, Priority: 10, Weight: 9},
		{ID: 3, Priority: 11, Weight: 1_000_000},
	}
	counts := map[int64]int{}
	for i := 0; i < 5000; i++ {
		selected := selectOpenAIHardLayerByWeightOrFirst(ordered, nil, false, openAILegacyUpstreamRateOrder{})
		require.NotNil(t, selected)
		counts[selected.ID]++
	}
	require.Greater(t, counts[int64(2)], counts[int64(1)]*5)
	require.Zero(t, counts[int64(3)], "weight must not promote a higher priority number")
}

func TestWeightedShuffleNeverPromotesHigherPriority(t *testing.T) {
	mathrand.Seed(20260815)
	for i := 0; i < 1000; i++ {
		accounts := []*Account{
			{ID: 1, Priority: 1, Weight: 1},
			{ID: 2, Priority: 1, Weight: 9},
			{ID: 3, Priority: 2, Weight: 1_000_000},
		}
		sortAccountsByPriorityAndLastUsed(accounts, false)
		require.Equal(t, 1, accounts[0].Priority)
	}
}
