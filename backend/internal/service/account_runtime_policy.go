package service

import (
	"context"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"strings"
)

// AccountRuntimeOutcome 描述一次已选账号请求对账号健康度的影响。
// Neutral 用于客户端取消、本地校验/转换、凭据准备等不能归因于上游账号的结果；
// Healthy 会清零连续失败；Failure 才会累加熔断计数。
type AccountRuntimeOutcome uint8

const (
	AccountRuntimeOutcomeNeutral AccountRuntimeOutcome = iota
	AccountRuntimeOutcomeHealthy
	AccountRuntimeOutcomeFailure
)

func runtimePolicyStore(cache RPMCache) AccountRuntimePolicyStore {
	store, _ := cache.(AccountRuntimePolicyStore)
	return store
}

func cloneAccountExclusions(source map[int64]struct{}) map[int64]struct{} {
	cloned := make(map[int64]struct{}, len(source)+1)
	for id := range source {
		cloned[id] = struct{}{}
	}
	return cloned
}

func acquireAccountRuntimePolicy(ctx context.Context, store AccountRuntimePolicyStore, account *Account) (AccountRuntimeGateResult, error) {
	if account == nil || store == nil {
		return AccountRuntimeGateResult{Allowed: true}, nil
	}
	// 兼容既有部署：四个新字段的零值表示策略未启用。未启用时必须完全
	// 绕过 Redis，不能让 Redis 故障改变旧账号的可用性或请求延迟。
	if account.EffectiveRPMLimit() <= 0 &&
		(account.EffectiveCircuitBreakerThreshold() <= 0 || account.EffectiveCircuitBreakerCooldown() <= 0) {
		return AccountRuntimeGateResult{Allowed: true}, nil
	}
	breakerEnabled := account.EffectiveCircuitBreakerThreshold() > 0 && account.EffectiveCircuitBreakerCooldown() > 0
	result, err := store.TryAcquireAccountRequest(ctx, account.ID, account.EffectiveRPMLimit(), breakerEnabled, account.EffectiveCircuitBreakerCooldown(), 0)
	if err != nil {
		return AccountRuntimeGateResult{}, err
	}
	return result, nil
}

func accountRuntimeGateError(result AccountRuntimeGateResult) error {
	return fmt.Errorf("%w: %s (retry_after=%s)", ErrNoAvailableAccounts, result.Reason, result.RetryAfter)
}

func acquireAccountSelectionRuntimePolicy(ctx context.Context, store AccountRuntimePolicyStore, selection *AccountSelectionResult) (AccountRuntimeGateResult, error) {
	if selection == nil || selection.Account == nil {
		return AccountRuntimeGateResult{Allowed: true}, nil
	}
	// 已在选号时拿到并发槽的分支会同步完成 runtime gate；等待分支只在
	// handler 真正取得槽位后调用本函数。重复调用不得再次消耗 RPM。
	if selection.RuntimePolicyAdmitted {
		return AccountRuntimeGateResult{Allowed: true, Generation: selection.RuntimePolicyGeneration}, nil
	}
	if selection.Account.EffectiveRPMLimit() <= 0 &&
		(selection.Account.EffectiveCircuitBreakerThreshold() <= 0 || selection.Account.EffectiveCircuitBreakerCooldown() <= 0) {
		selection.RuntimePolicyAdmitted = true
		return AccountRuntimeGateResult{Allowed: true}, nil
	}
	gate, err := acquireAccountRuntimePolicy(ctx, store, selection.Account)
	if err != nil {
		return AccountRuntimeGateResult{}, err
	}
	if gate.Allowed {
		selection.RuntimePolicyGeneration = gate.Generation
		selection.RuntimePolicyAdmitted = true
	}
	return gate, nil
}

// AcquireAccountSelectionRuntimePolicy 在 WaitPlan 真正取得账号并发槽之后执行
// RPM/熔断原子准入。排队失败或客户端取消不会调用它，因此不会消耗 RPM。
func (s *GatewayService) AcquireAccountSelectionRuntimePolicy(ctx context.Context, selection *AccountSelectionResult) (AccountRuntimeGateResult, error) {
	if s == nil {
		return AccountRuntimeGateResult{Allowed: true}, nil
	}
	return acquireAccountSelectionRuntimePolicy(ctx, s.accountRuntimePolicyStore(), selection)
}

// AcquireAccountSelectionRuntimePolicy 是 OpenAI 调度栈对应的延迟准入入口。
func (s *OpenAIGatewayService) AcquireAccountSelectionRuntimePolicy(ctx context.Context, selection *AccountSelectionResult) (AccountRuntimeGateResult, error) {
	if s == nil {
		return AccountRuntimeGateResult{Allowed: true}, nil
	}
	return acquireAccountSelectionRuntimePolicy(ctx, s.accountRuntimePolicy, selection)
}

func accountWithRuntimePolicyGeneration(account *Account, generation int64) *Account {
	if account == nil || generation <= 0 {
		return account
	}
	// Legacy selectors return *Account rather than AccountSelectionResult. Make
	// the admission token request-private so concurrent requests never mutate a
	// scheduler snapshot/cache object.
	cloned := *account
	cloned.runtimePolicyGeneration = generation
	return &cloned
}

func recordAccountRuntimeOutcome(ctx context.Context, store AccountRuntimePolicyStore, account *Account, generation int64, outcome AccountRuntimeOutcome) error {
	if account == nil || store == nil || outcome == AccountRuntimeOutcomeNeutral {
		return nil
	}
	threshold := account.EffectiveCircuitBreakerThreshold()
	cooldown := account.EffectiveCircuitBreakerCooldown()
	// RPM 门控不需要回报；账号熔断未显式启用时完全不碰 Redis。
	if threshold <= 0 || cooldown <= 0 {
		return nil
	}
	if generation <= 0 {
		return nil
	}
	return store.RecordAccountResult(
		ctx,
		account.ID,
		generation,
		outcome == AccountRuntimeOutcomeHealthy,
		threshold,
		cooldown,
	)
}

// AccountRuntimeOutcomeFromError 按 Worker 的账号熔断归因语义分类错误。
// 401/402/403/408/409/429/5xx、网络及真正上游超时为账号失败；404/模型级
// 不支持为 neutral；其它已收到的 4xx 说明上游仍健康，会清连续失败。
func AccountRuntimeOutcomeFromError(err error) AccountRuntimeOutcome {
	if err == nil {
		return AccountRuntimeOutcomeHealthy
	}
	if errors.Is(err, context.Canceled) {
		return AccountRuntimeOutcomeNeutral
	}

	var upstreamErr *UpstreamFailoverError
	if errors.As(err, &upstreamErr) {
		if upstreamErr.RequestScopedTransient {
			return AccountRuntimeOutcomeNeutral
		}
		if upstreamErr.IsCredentialFailure() {
			switch upstreamErr.Scope {
			case GatewayFailureScopeRequest, GatewayFailureScopeProvider:
				return AccountRuntimeOutcomeNeutral
			case GatewayFailureScopeAccount:
				if upstreamErr.StatusCode <= 0 {
					return AccountRuntimeOutcomeFailure
				}
			default:
				// 旧调用方没有 scope：有明确上游 HTTP 状态时仍按状态归因；
				// 无状态的本地凭据准备错误保持 neutral。
				if upstreamErr.StatusCode <= 0 {
					return AccountRuntimeOutcomeNeutral
				}
			}
		}
		return accountRuntimeOutcomeFromUpstreamStatus(upstreamErr.StatusCode, upstreamErr.ResponseBody)
	}

	var statusErr *UpstreamHTTPStatusError
	if errors.As(err, &statusErr) {
		if statusErr.Scope == GatewayFailureScopeRequest || statusErr.Scope == GatewayFailureScopeProvider {
			return AccountRuntimeOutcomeNeutral
		}
		return accountRuntimeOutcomeFromUpstreamStatus(statusErr.StatusCode, statusErr.ResponseBody)
	}

	var transportErr *UpstreamTransportError
	if errors.As(err, &transportErr) {
		return AccountRuntimeOutcomeFailure
	}

	var manifestErr *codexModelsManifestUpstreamError
	if errors.As(err, &manifestErr) {
		if manifestErr.statusCode > 0 {
			return accountRuntimeOutcomeFromUpstreamStatus(manifestErr.statusCode, manifestErr.body)
		}
		if isRetryableCodexModelsManifestTransportError(manifestErr.err) {
			return AccountRuntimeOutcomeFailure
		}
		return AccountRuntimeOutcomeNeutral
	}

	// 只有具备明确网络类型的错误才能归因为上游失败；普通 fmt/error 很可能
	// 来自本地请求转换、校验或凭据准备，必须保持 neutral。
	if errors.Is(err, context.DeadlineExceeded) {
		// 入站 context deadline 本身保持 neutral；只有明确的 HTTP URL 传输
		// 包装才说明真正的上游请求超时。
		var urlErr *url.Error
		if errors.As(err, &urlErr) {
			return AccountRuntimeOutcomeFailure
		}
		return AccountRuntimeOutcomeNeutral
	}
	if errors.Is(err, io.EOF) || errors.Is(err, io.ErrUnexpectedEOF) || errors.Is(err, net.ErrClosed) {
		return AccountRuntimeOutcomeFailure
	}
	var netErr net.Error
	if errors.As(err, &netErr) {
		return AccountRuntimeOutcomeFailure
	}
	return AccountRuntimeOutcomeNeutral
}

func accountRuntimeOutcomeFromUpstreamStatus(statusCode int, body []byte) AccountRuntimeOutcome {
	if isAccountRuntimeModelOnlyError(statusCode, body) {
		return AccountRuntimeOutcomeNeutral
	}
	switch statusCode {
	case http.StatusUnauthorized,
		http.StatusPaymentRequired,
		http.StatusForbidden,
		http.StatusRequestTimeout,
		http.StatusConflict,
		http.StatusTooManyRequests:
		return AccountRuntimeOutcomeFailure
	}
	if statusCode >= http.StatusInternalServerError {
		return AccountRuntimeOutcomeFailure
	}
	if statusCode >= http.StatusBadRequest {
		return AccountRuntimeOutcomeHealthy
	}
	if statusCode > 0 {
		return AccountRuntimeOutcomeHealthy
	}
	// UpstreamFailoverError 的零状态表示请求未取得 HTTP 响应，按传输失败处理。
	return AccountRuntimeOutcomeFailure
}

func isAccountRuntimeModelOnlyError(statusCode int, body []byte) bool {
	if statusCode == http.StatusNotFound || isUpstreamModelNotFoundError(statusCode, body) {
		return true
	}
	if statusCode != http.StatusBadRequest {
		return false
	}
	normalized := normalizeModelNotFoundBody(body)
	if normalized == "" || !strings.Contains(normalized, "model") {
		return false
	}
	return containsModelNotFoundKeyword(normalized) ||
		strings.Contains(normalized, "unsupported model") ||
		strings.Contains(normalized, "model unsupported") ||
		strings.Contains(normalized, "model is not supported") ||
		strings.Contains(normalized, "model does not support")
}

func AccountRuntimeOutcomeFromUpstreamStatus(statusCode int, body []byte) AccountRuntimeOutcome {
	return accountRuntimeOutcomeFromUpstreamStatus(statusCode, body)
}

func AccountRuntimeOutcomeFromForward(result *ForwardResult, err error) AccountRuntimeOutcome {
	if result != nil && result.ClientDisconnect {
		return AccountRuntimeOutcomeNeutral
	}
	return AccountRuntimeOutcomeFromError(err)
}

func AccountRuntimeOutcomeFromContext(ctx context.Context, err error) AccountRuntimeOutcome {
	if ctx != nil && ctx.Err() != nil {
		return AccountRuntimeOutcomeNeutral
	}
	return AccountRuntimeOutcomeFromError(err)
}

func AccountRuntimeOutcomeFromForwardContext(ctx context.Context, result *ForwardResult, err error) AccountRuntimeOutcome {
	if ctx != nil && ctx.Err() != nil {
		return AccountRuntimeOutcomeNeutral
	}
	return AccountRuntimeOutcomeFromForward(result, err)
}

func AccountRuntimeOutcomeFromOpenAIForwardContext(ctx context.Context, result *OpenAIForwardResult, err error) AccountRuntimeOutcome {
	if ctx != nil && ctx.Err() != nil {
		return AccountRuntimeOutcomeNeutral
	}
	if result != nil && result.ClientDisconnect {
		return AccountRuntimeOutcomeNeutral
	}
	if err != nil {
		return AccountRuntimeOutcomeFromError(err)
	}
	if result != nil && !result.SucceededForScheduling() {
		return AccountRuntimeOutcomeNeutral
	}
	return AccountRuntimeOutcomeHealthy
}

func (s *GatewayService) accountRuntimePolicyStore() AccountRuntimePolicyStore {
	if s == nil {
		return nil
	}
	return runtimePolicyStore(s.rpmCache)
}

func (s *GatewayService) RecordAccountRuntimeResult(ctx context.Context, account *Account, success bool) {
	if !success {
		// 旧布尔接口无法区分本地错误、客户端取消与真正上游失败；为避免误熔断，
		// 失败必须改走 RecordAccountRuntimeOutcome 的三态接口。
		return
	}
	s.RecordAccountRuntimeOutcome(ctx, account, AccountRuntimeOutcomeHealthy)
}

func (s *GatewayService) RecordAccountRuntimeOutcome(ctx context.Context, account *Account, outcome AccountRuntimeOutcome) {
	generation := int64(0)
	if account != nil {
		generation = account.runtimePolicyGeneration
	}
	_ = recordAccountRuntimeOutcome(ctx, s.accountRuntimePolicyStore(), account, generation, outcome)
}

func (s *GatewayService) RecordAccountSelectionRuntimeOutcome(ctx context.Context, selection *AccountSelectionResult, outcome AccountRuntimeOutcome) {
	if selection == nil {
		return
	}
	_ = recordAccountRuntimeOutcome(ctx, s.accountRuntimePolicyStore(), selection.Account, selection.RuntimePolicyGeneration, outcome)
}

func (s *OpenAIGatewayService) SetAccountRuntimePolicyCache(cache RPMCache) {
	if s != nil {
		s.accountRuntimePolicy = runtimePolicyStore(cache)
	}
}

// ClearAccountRuntimeCircuit 供管理写路径在关闭熔断策略时立即删除 Redis 中
// 的连续失败/open-until 状态，避免同一账号日后重新启用时继承旧熔断。
func (s *OpenAIGatewayService) ClearAccountRuntimeCircuit(ctx context.Context, accountID int64) error {
	if s == nil || s.accountRuntimePolicy == nil || accountID <= 0 {
		return nil
	}
	return s.accountRuntimePolicy.ClearAccountCircuit(ctx, accountID)
}

func (s *OpenAIGatewayService) recordAccountRuntimeOutcome(ctx context.Context, account *Account, generation int64, outcome AccountRuntimeOutcome) {
	if s == nil || s.accountRuntimePolicy == nil || account == nil || account.ID <= 0 || outcome == AccountRuntimeOutcomeNeutral {
		return
	}
	_ = recordAccountRuntimeOutcome(ctx, s.accountRuntimePolicy, account, generation, outcome)
}
