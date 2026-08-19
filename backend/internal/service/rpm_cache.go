package service

import (
	"context"
	"time"
)

// RPMCache RPM 计数器缓存接口
// 用于 Anthropic OAuth/SetupToken 账号的每分钟请求数限制
type RPMCache interface {
	// IncrementRPM 原子递增并返回当前分钟的计数
	// 使用 Redis 服务器时间确定 minute key，避免多实例时钟偏差
	IncrementRPM(ctx context.Context, accountID int64) (count int, err error)

	// GetRPM 获取当前分钟的 RPM 计数
	GetRPM(ctx context.Context, accountID int64) (count int, err error)

	// GetRPMBatch 批量获取多个账号的 RPM 计数（使用 Pipeline）
	GetRPMBatch(ctx context.Context, accountIDs []int64) (map[int64]int, error)
}

type AccountRuntimeGateReason string

const (
	AccountRuntimeGateAllowed     AccountRuntimeGateReason = ""
	AccountRuntimeGateRPMExceeded AccountRuntimeGateReason = "rpm_limit"
	AccountRuntimeGateCircuitOpen AccountRuntimeGateReason = "circuit_open"
)

type AccountRuntimeGateResult struct {
	Allowed    bool
	Reason     AccountRuntimeGateReason
	RetryAfter time.Duration
	Generation int64
}

// AccountRuntimePolicyStore 是账号通用调度策略的原子 Redis 运行态。
// 它与历史 RPMCache 接口分离，避免把 OAuth base_rpm 的软计数语义混入硬门槛。
type AccountRuntimePolicyStore interface {
	TryAcquireAccountRequest(ctx context.Context, accountID int64, rpmLimit int, circuitBreakerEnabled bool, circuitTTL time.Duration, proposedGeneration int64) (AccountRuntimeGateResult, error)
	IsAccountCircuitOpen(ctx context.Context, accountID int64) (open bool, retryAfter time.Duration, err error)
	RecordAccountResult(ctx context.Context, accountID int64, generation int64, success bool, threshold int, cooldown time.Duration) error
	ClearAccountCircuit(ctx context.Context, accountID int64) error
}
