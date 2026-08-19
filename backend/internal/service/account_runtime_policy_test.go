package service

import (
	"context"
	"errors"
	"net"
	"net/http"
	"testing"
	"time"

	"github.com/stretchr/testify/require"
)

type countingAccountRuntimePolicyStore struct {
	acquireCalls       int
	recordCalls        int
	clearCalls         int
	lastRPMLimit       int
	lastBreakerEnabled bool
}

func (s *countingAccountRuntimePolicyStore) TryAcquireAccountRequest(_ context.Context, _ int64, rpmLimit int, circuitBreakerEnabled bool, _ time.Duration, _ int64) (AccountRuntimeGateResult, error) {
	s.acquireCalls++
	s.lastRPMLimit = rpmLimit
	s.lastBreakerEnabled = circuitBreakerEnabled
	result := AccountRuntimeGateResult{Allowed: true}
	if circuitBreakerEnabled {
		result.Generation = 1
	}
	return result, nil
}

func (s *countingAccountRuntimePolicyStore) IsAccountCircuitOpen(context.Context, int64) (bool, time.Duration, error) {
	return false, 0, nil
}

func (s *countingAccountRuntimePolicyStore) RecordAccountResult(context.Context, int64, int64, bool, int, time.Duration) error {
	s.recordCalls++
	return nil
}

func (s *countingAccountRuntimePolicyStore) ClearAccountCircuit(context.Context, int64) error {
	s.clearCalls++
	return nil
}

func TestAccountRuntimePolicyDisabledDoesNotTouchStore(t *testing.T) {
	store := &countingAccountRuntimePolicyStore{}
	account := &Account{ID: 7, Weight: 100}

	gate, err := acquireAccountRuntimePolicy(context.Background(), store, account)
	require.NoError(t, err)
	require.True(t, gate.Allowed)
	require.NoError(t, recordAccountRuntimeOutcome(context.Background(), store, account, 0, AccountRuntimeOutcomeHealthy))
	require.NoError(t, recordAccountRuntimeOutcome(context.Background(), store, account, 0, AccountRuntimeOutcomeFailure))
	require.Zero(t, store.acquireCalls)
	require.Zero(t, store.recordCalls)
}

func TestAccountRuntimeOutcomeMatchesWorkerAccountFailureSemantics(t *testing.T) {
	tests := []struct {
		name string
		err  error
		want AccountRuntimeOutcome
	}{
		{name: "401 account failure", err: &UpstreamFailoverError{StatusCode: http.StatusUnauthorized}, want: AccountRuntimeOutcomeFailure},
		{name: "409 transient account failure", err: &UpstreamFailoverError{StatusCode: http.StatusConflict}, want: AccountRuntimeOutcomeFailure},
		{name: "429 account failure", err: &UpstreamFailoverError{StatusCode: http.StatusTooManyRequests}, want: AccountRuntimeOutcomeFailure},
		{name: "500 account failure", err: &UpstreamFailoverError{StatusCode: http.StatusInternalServerError}, want: AccountRuntimeOutcomeFailure},
		{name: "404 model only", err: &UpstreamFailoverError{StatusCode: http.StatusNotFound}, want: AccountRuntimeOutcomeNeutral},
		{name: "400 unsupported model", err: &UpstreamFailoverError{StatusCode: http.StatusBadRequest, ResponseBody: []byte(`{"error":{"code":"model_not_found"}}`)}, want: AccountRuntimeOutcomeNeutral},
		{name: "ordinary 400 proves healthy upstream", err: &UpstreamFailoverError{StatusCode: http.StatusBadRequest, ResponseBody: []byte(`{"error":{"code":"invalid_request"}}`)}, want: AccountRuntimeOutcomeHealthy},
		{name: "ordinary 422 proves healthy upstream", err: &UpstreamFailoverError{StatusCode: http.StatusUnprocessableEntity}, want: AccountRuntimeOutcomeHealthy},
		{name: "request scoped credential is neutral", err: &UpstreamFailoverError{StatusCode: http.StatusUnauthorized, Stage: GatewayFailureStageAccountAuth, Scope: GatewayFailureScopeRequest}, want: AccountRuntimeOutcomeNeutral},
		{name: "account scoped credential fails", err: &UpstreamFailoverError{StatusCode: http.StatusUnauthorized, Stage: GatewayFailureStageAccountAuth, Scope: GatewayFailureScopeAccount}, want: AccountRuntimeOutcomeFailure},
		{name: "request scoped policy 403 is neutral", err: newRequestScopedUpstreamHTTPStatusError(http.StatusForbidden, []byte(`{"error":"content policy"}`), "content policy"), want: AccountRuntimeOutcomeNeutral},
		{name: "account scoped HTTP 403 fails", err: newUpstreamHTTPStatusError(http.StatusForbidden, nil, "forbidden"), want: AccountRuntimeOutcomeFailure},
		{name: "typed ordinary HTTP 400 proves healthy", err: newUpstreamHTTPStatusError(http.StatusBadRequest, []byte(`{"error":{"code":"invalid_request"}}`), "bad request"), want: AccountRuntimeOutcomeHealthy},
		{name: "typed transport unwraps network failure", err: newUpstreamTransportError(&net.DNSError{Err: "timeout", IsTimeout: true}, "dial failed"), want: AccountRuntimeOutcomeFailure},
		{name: "local conversion neutral", err: errors.New("convert request locally"), want: AccountRuntimeOutcomeNeutral},
		{name: "client cancel neutral", err: context.Canceled, want: AccountRuntimeOutcomeNeutral},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			require.Equal(t, test.want, AccountRuntimeOutcomeFromError(test.err))
		})
	}
}

func TestAccountSelectionRuntimePolicyWaitPlanConsumesOnlyAfterAdmission(t *testing.T) {
	store := &countingAccountRuntimePolicyStore{}
	selection := &AccountSelectionResult{
		Account:  &Account{ID: 17, RPMLimit: 120},
		Acquired: false,
		WaitPlan: &AccountWaitPlan{AccountID: 17, MaxConcurrency: 1},
	}

	// 排队阶段不调用准入函数，等同排队超时/取消：RPM 保持 0 次。
	require.Zero(t, store.acquireCalls)
	gate, err := acquireAccountSelectionRuntimePolicy(context.Background(), store, selection)
	require.NoError(t, err)
	require.True(t, gate.Allowed)
	require.Equal(t, 1, store.acquireCalls)
	require.Equal(t, 120, store.lastRPMLimit)
	require.False(t, store.lastBreakerEnabled)
	require.True(t, selection.RuntimePolicyAdmitted)
	require.Zero(t, selection.RuntimePolicyGeneration)

	// RPM-only admission 的 generation 为 0，仍必须由显式 admitted 位保证幂等。
	_, err = acquireAccountSelectionRuntimePolicy(context.Background(), store, selection)
	require.NoError(t, err)
	require.Equal(t, 1, store.acquireCalls)
	require.NoError(t, recordAccountRuntimeOutcome(context.Background(), store, selection.Account, selection.RuntimePolicyGeneration, AccountRuntimeOutcomeFailure))
	require.Zero(t, store.recordCalls, "RPM-only policy must not write circuit state")
}

func TestReportOpenAIAccountScheduleOutcomeNeutralSkipsSchedulerAndStore(t *testing.T) {
	store := &countingAccountRuntimePolicyStore{}
	svc := &OpenAIGatewayService{accountRuntimePolicy: store}
	svc.ReportOpenAIAccountScheduleOutcome(9, "gpt-test", AccountRuntimeOutcomeNeutral, nil)
	require.Zero(t, store.recordCalls)
}

func TestAccountRuntimeOutcomeFromOpenAIForwardClientDisconnectIsNeutral(t *testing.T) {
	result := &OpenAIForwardResult{ClientDisconnect: true}
	err := newUpstreamTransportError(&net.OpError{Op: "write", Err: errors.New("broken pipe")}, "downstream write failed")
	require.Equal(t, AccountRuntimeOutcomeNeutral, AccountRuntimeOutcomeFromOpenAIForwardContext(context.Background(), result, err))
}

func TestOpenAIStreamFailureStatusErrorPreservesRuntimeScope(t *testing.T) {
	tests := []struct {
		name    string
		payload string
		want    AccountRuntimeOutcome
	}{
		{
			name:    "content policy is request scoped",
			payload: `{"type":"response.failed","response":{"error":{"code":"content_policy_violation","message":"blocked by policy"}}}`,
			want:    AccountRuntimeOutcomeNeutral,
		},
		{
			name:    "ordinary invalid request proves upstream healthy",
			payload: `{"type":"response.failed","response":{"error":{"type":"invalid_request_error","code":"invalid_request","message":"bad input"}}}`,
			want:    AccountRuntimeOutcomeHealthy,
		},
		{
			name:    "server failure is account failure",
			payload: `{"type":"response.failed","response":{"error":{"code":"internal_server_error","message":"server failed"}}}`,
			want:    AccountRuntimeOutcomeFailure,
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			err := newOpenAIStreamFailureStatusError([]byte(test.payload), "")
			require.Equal(t, test.want, AccountRuntimeOutcomeFromError(err))
		})
	}
}
