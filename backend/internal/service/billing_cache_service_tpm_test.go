package service

import (
	"context"
	"errors"
	"testing"

	"github.com/stretchr/testify/require"
)

type userTPMCacheStub struct {
	used       int
	getErr     error
	increments []int
}

func (s *userTPMCacheStub) IncrementUserGroupRPM(context.Context, int64, int64) (int, error) {
	return 0, nil
}

func (s *userTPMCacheStub) IncrementUserRPM(context.Context, int64) (int, error) {
	return 0, nil
}

func (s *userTPMCacheStub) GetUserGroupRPM(context.Context, int64, int64) (int, error) {
	return 0, nil
}

func (s *userTPMCacheStub) GetUserRPM(context.Context, int64) (int, error) {
	return 0, nil
}

func (s *userTPMCacheStub) IncrementUserTPM(_ context.Context, _ int64, tokens int) (int, error) {
	s.increments = append(s.increments, tokens)
	s.used += tokens
	return s.used, nil
}

func (s *userTPMCacheStub) GetUserTPM(context.Context, int64) (int, error) {
	return s.used, s.getErr
}

func TestBillingCacheServiceCheckTPM(t *testing.T) {
	tests := []struct {
		name    string
		limit   int
		used    int
		getErr  error
		wantErr bool
	}{
		{name: "unlimited", limit: 0, used: 1000},
		{name: "below limit", limit: 1000, used: 999},
		{name: "at limit", limit: 1000, used: 1000, wantErr: true},
		{name: "redis fail open", limit: 1000, used: 1000, getErr: errors.New("redis unavailable")},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			cache := &userTPMCacheStub{used: tt.used, getErr: tt.getErr}
			svc := &BillingCacheService{userRPMCache: cache}
			err := svc.checkTPM(context.Background(), &User{ID: 42, TPMLimit: tt.limit})
			if tt.wantErr {
				require.ErrorIs(t, err, ErrUserTPMExceeded)
			} else {
				require.NoError(t, err)
			}
		})
	}
}

func TestBillingCacheServiceRecordUserTPM(t *testing.T) {
	cache := &userTPMCacheStub{}
	svc := &BillingCacheService{userRPMCache: cache}
	svc.RecordUserTPM(nil, 42, 321)
	require.Equal(t, []int{321}, cache.increments)
	require.Equal(t, 321, cache.used)
}
