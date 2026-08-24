package service

import (
	"context"
	"net/http"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/config"
	"github.com/stretchr/testify/require"
)

type copilotMonthlyQuotaPriorityRepo struct {
	AccountRepository
	calls  int
	until  time.Time
	reason string
}

func (r *copilotMonthlyQuotaPriorityRepo) SetTempUnschedulable(_ context.Context, _ int64, until time.Time, reason string) error {
	r.calls++
	r.until = until
	r.reason = reason
	return nil
}

func TestCopilotMonthlyQuotaExceededTakesPriorityOverCustomTempRule(t *testing.T) {
	copilotBillingGuardCache.Clear()
	t.Cleanup(copilotBillingGuardCache.Clear)
	repo := &copilotMonthlyQuotaPriorityRepo{}
	svc := NewRateLimitService(repo, nil, &config.Config{}, nil, nil)
	account := newCopilotGatewayTestAccount()
	account.ID = 5301
	account.Credentials["temp_unschedulable_enabled"] = true
	account.Credentials["temp_unschedulable_rules"] = []any{
		map[string]any{
			"error_code":       float64(http.StatusPaymentRequired),
			"keywords":         []any{"quota_exceeded"},
			"duration_minutes": float64(10),
		},
	}
	body := []byte(`{"error":{"code":"quota_exceeded","message":"monthly quota exhausted"}}`)

	require.Equal(t, ErrorPolicyNone, svc.CheckErrorPolicy(context.Background(), account, http.StatusPaymentRequired, body))
	require.False(t, svc.HandleTempUnschedulable(context.Background(), account, http.StatusPaymentRequired, body))
	require.True(t, svc.HandleUpstreamError(context.Background(), account, http.StatusPaymentRequired, http.Header{}, body))

	require.Equal(t, 1, repo.calls)
	require.Equal(t, CopilotMonthlyQuotaExceededReason, repo.reason)
	require.Equal(t, nextCopilotMonthlyQuotaReset(time.Now().UTC()), repo.until)
}
