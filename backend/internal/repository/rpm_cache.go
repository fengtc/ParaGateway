package repository

import (
	"context"
	cryptorand "crypto/rand"
	"encoding/binary"
	"errors"
	"fmt"
	"strconv"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/redis/go-redis/v9"
)

// RPM 计数器缓存常量定义
//
// 设计说明：
// 使用 Redis 简单计数器跟踪每个账号每分钟的请求数：
// - Key: rpm:{accountID}:{minuteTimestamp}
// - Value: 当前分钟内的请求计数
// - TTL: 120 秒（覆盖当前分钟 + 一定冗余）
//
// 使用 TxPipeline（MULTI/EXEC）执行 INCR + EXPIRE，保证原子性且兼容 Redis Cluster。
// 通过 rdb.Time() 获取服务端时间，避免多实例时钟不同步。
//
// 设计决策：
//   - TxPipeline vs Pipeline：Pipeline 仅合并发送但不保证原子，TxPipeline 使用 MULTI/EXEC 事务保证原子执行。
//   - rdb.Time() 单独调用：Pipeline/TxPipeline 中无法引用前一命令的结果，因此 TIME 必须单独调用（2 RTT）。
//     Lua 脚本可以做到 1 RTT，但在 Redis Cluster 中动态拼接 key 存在 CROSSSLOT 风险，选择安全性优先。
const (
	// RPM 计数器键前缀
	// 格式: rpm:{accountID}:{minuteTimestamp}
	rpmKeyPrefix = "rpm:"

	// RPM 计数器 TTL（120 秒，覆盖当前分钟窗口 + 冗余）
	rpmKeyTTL               = 120 * time.Second
	accountRuntimeKeyPrefix = "account_runtime_policy:"
)

var accountRuntimeAcquireScript = redis.NewScript(`
local now = redis.call('TIME')
local now_ms = tonumber(now[1]) * 1000 + math.floor(tonumber(now[2]) / 1000)
local breaker_enabled = tonumber(ARGV[2]) == 1
local generation = 0
if breaker_enabled then
  redis.call('HSETNX', KEYS[1], 'policy_generation', ARGV[3])
  generation = tonumber(redis.call('HGET', KEYS[1], 'policy_generation') or ARGV[3])
  local open_until = tonumber(redis.call('HGET', KEYS[1], 'open_until_ms') or '0')
  if open_until > now_ms then
    return {2, open_until - now_ms, generation}
  end
  if open_until > 0 then
    redis.call('HDEL', KEYS[1], 'open_until_ms', 'consecutive_failures')
  end
else
  -- RPM-only accounts must never be blocked by stale circuit state.
  redis.call('HDEL', KEYS[1], 'open_until_ms', 'consecutive_failures', 'policy_generation')
end
local limit = tonumber(ARGV[1])
local ttl_ms = math.max(120000, tonumber(ARGV[4]) or 120000)
if limit <= 0 then
  if redis.call('HLEN', KEYS[1]) > 0 then
    redis.call('PEXPIRE', KEYS[1], ttl_ms)
  end
  return {0, 0, generation}
end
local window_start = tonumber(redis.call('HGET', KEYS[1], 'rpm_window_start_ms') or '0')
local count = tonumber(redis.call('HGET', KEYS[1], 'rpm_count') or '0')
if window_start == 0 or now_ms - window_start >= 60000 then
  window_start = now_ms
  count = 0
end
if count >= limit then
  return {1, math.max(1, 60000 - (now_ms - window_start)), generation}
end
count = count + 1
redis.call('HSET', KEYS[1], 'rpm_window_start_ms', window_start, 'rpm_count', count)
redis.call('PEXPIRE', KEYS[1], ttl_ms)
return {0, 0, generation}
`)

var accountRuntimeCircuitStatusScript = redis.NewScript(`
local now = redis.call('TIME')
local now_ms = tonumber(now[1]) * 1000 + math.floor(tonumber(now[2]) / 1000)
local open_until = tonumber(redis.call('HGET', KEYS[1], 'open_until_ms') or '0')
if open_until > now_ms then
  return open_until - now_ms
end
if open_until > 0 then
  redis.call('HDEL', KEYS[1], 'open_until_ms', 'consecutive_failures')
end
return 0
`)

var accountRuntimeRecordScript = redis.NewScript(`
local success = tonumber(ARGV[1])
local threshold = tonumber(ARGV[2])
local cooldown_ms = tonumber(ARGV[3])
local expected_generation = tonumber(ARGV[4])
local current_generation = tonumber(redis.call('HGET', KEYS[1], 'policy_generation') or '0')
if expected_generation <= 0 or current_generation ~= expected_generation then
  return -1
end
if success == 1 then
  redis.call('HDEL', KEYS[1], 'consecutive_failures', 'open_until_ms')
  return 0
end
if threshold <= 0 or cooldown_ms <= 0 then
  return 0
end
local failures = redis.call('HINCRBY', KEYS[1], 'consecutive_failures', 1)
if failures >= threshold then
  local now = redis.call('TIME')
  local now_ms = tonumber(now[1]) * 1000 + math.floor(tonumber(now[2]) / 1000)
  redis.call('HSET', KEYS[1], 'open_until_ms', now_ms + cooldown_ms)
end
redis.call('PEXPIRE', KEYS[1], tonumber(ARGV[5]))
return failures
`)

var accountRuntimeClearCircuitScript = redis.NewScript(`
redis.call('HSET', KEYS[1], 'policy_generation', ARGV[1])
redis.call('HDEL', KEYS[1], 'consecutive_failures', 'open_until_ms')
redis.call('PEXPIRE', KEYS[1], 86400000)
return ARGV[1]
`)

// RPMCacheImpl RPM 计数器缓存 Redis 实现
type RPMCacheImpl struct {
	rdb *redis.Client
}

// NewRPMCache 创建 RPM 计数器缓存
func NewRPMCache(rdb *redis.Client) service.RPMCache {
	return &RPMCacheImpl{rdb: rdb}
}

func accountRuntimeKey(accountID int64) string {
	return fmt.Sprintf("%s{%d}", accountRuntimeKeyPrefix, accountID)
}

func scriptInt64Pair(value any) (int64, int64, error) {
	values, ok := value.([]any)
	if !ok || len(values) < 2 {
		return 0, 0, fmt.Errorf("unexpected redis script result: %T", value)
	}
	code, err := redisScriptInt64(values[0])
	if err != nil {
		return 0, 0, err
	}
	retry, err := redisScriptInt64(values[1])
	if err != nil {
		return 0, 0, err
	}
	return code, retry, nil
}

func scriptInt64Triple(value any) (int64, int64, int64, error) {
	values, ok := value.([]any)
	if !ok || len(values) < 3 {
		return 0, 0, 0, fmt.Errorf("unexpected redis script result: %T", value)
	}
	first, err := redisScriptInt64(values[0])
	if err != nil {
		return 0, 0, 0, err
	}
	second, err := redisScriptInt64(values[1])
	if err != nil {
		return 0, 0, 0, err
	}
	third, err := redisScriptInt64(values[2])
	if err != nil {
		return 0, 0, 0, err
	}
	return first, second, third, nil
}

func redisScriptInt64(value any) (int64, error) {
	switch v := value.(type) {
	case int64:
		return v, nil
	case string:
		return strconv.ParseInt(v, 10, 64)
	case []byte:
		return strconv.ParseInt(string(v), 10, 64)
	default:
		return 0, fmt.Errorf("unexpected redis integer type: %T", value)
	}
}

func (c *RPMCacheImpl) TryAcquireAccountRequest(ctx context.Context, accountID int64, rpmLimit int, circuitBreakerEnabled bool, circuitTTL time.Duration, proposedGeneration int64) (service.AccountRuntimeGateResult, error) {
	if c == nil || c.rdb == nil || accountID <= 0 {
		return service.AccountRuntimeGateResult{Allowed: true}, nil
	}
	breakerArg := 0
	if circuitBreakerEnabled {
		breakerArg = 1
	}
	if proposedGeneration <= 0 {
		proposedGeneration = newAccountRuntimePolicyGeneration()
	}
	ttl := 24 * time.Hour
	if circuitTTL > 0 && circuitTTL+time.Minute > ttl {
		ttl = circuitTTL + time.Minute
	}
	value, err := accountRuntimeAcquireScript.Run(ctx, c.rdb, []string{accountRuntimeKey(accountID)}, rpmLimit, breakerArg, proposedGeneration, ttl.Milliseconds()).Result()
	if err != nil {
		return service.AccountRuntimeGateResult{}, fmt.Errorf("account runtime acquire: %w", err)
	}
	code, retryMs, generation, err := scriptInt64Triple(value)
	if err != nil {
		return service.AccountRuntimeGateResult{}, fmt.Errorf("account runtime acquire: %w", err)
	}
	result := service.AccountRuntimeGateResult{Allowed: code == 0, RetryAfter: time.Duration(retryMs) * time.Millisecond, Generation: generation}
	switch code {
	case 1:
		result.Reason = service.AccountRuntimeGateRPMExceeded
	case 2:
		result.Reason = service.AccountRuntimeGateCircuitOpen
	}
	return result, nil
}

func (c *RPMCacheImpl) IsAccountCircuitOpen(ctx context.Context, accountID int64) (bool, time.Duration, error) {
	if c == nil || c.rdb == nil || accountID <= 0 {
		return false, 0, nil
	}
	value, err := accountRuntimeCircuitStatusScript.Run(ctx, c.rdb, []string{accountRuntimeKey(accountID)}).Int64()
	if err != nil {
		return false, 0, fmt.Errorf("account circuit status: %w", err)
	}
	return value > 0, time.Duration(value) * time.Millisecond, nil
}

func (c *RPMCacheImpl) RecordAccountResult(ctx context.Context, accountID int64, generation int64, success bool, threshold int, cooldown time.Duration) error {
	if c == nil || c.rdb == nil || accountID <= 0 {
		return nil
	}
	successArg := 0
	if success {
		successArg = 1
	}
	ttl := 24 * time.Hour
	if cooldown > 0 && cooldown+time.Minute > ttl {
		ttl = cooldown + time.Minute
	}
	if err := accountRuntimeRecordScript.Run(ctx, c.rdb, []string{accountRuntimeKey(accountID)}, successArg, threshold, cooldown.Milliseconds(), generation, ttl.Milliseconds()).Err(); err != nil {
		return fmt.Errorf("record account runtime result: %w", err)
	}
	return nil
}

func (c *RPMCacheImpl) ClearAccountCircuit(ctx context.Context, accountID int64) error {
	if c == nil || c.rdb == nil || accountID <= 0 {
		return nil
	}
	if err := accountRuntimeClearCircuitScript.Run(ctx, c.rdb, []string{accountRuntimeKey(accountID)}, newAccountRuntimePolicyGeneration()).Err(); err != nil {
		return fmt.Errorf("clear account runtime circuit: %w", err)
	}
	return nil
}

func newAccountRuntimePolicyGeneration() int64 {
	var raw [8]byte
	if _, err := cryptorand.Read(raw[:]); err == nil {
		generation := int64(binary.LittleEndian.Uint64(raw[:]) & uint64(^uint64(0)>>1))
		if generation > 0 {
			return generation
		}
	}
	generation := time.Now().UnixNano()
	if generation < 0 {
		generation = -generation
	}
	if generation == 0 {
		return 1
	}
	return generation
}

// currentMinuteKey 获取当前分钟的完整 Redis key
// 使用 rdb.Time() 获取 Redis 服务端时间，避免多实例时钟偏差
func (c *RPMCacheImpl) currentMinuteKey(ctx context.Context, accountID int64) (string, error) {
	serverTime, err := c.rdb.Time(ctx).Result()
	if err != nil {
		return "", fmt.Errorf("redis TIME: %w", err)
	}
	minuteTS := serverTime.Unix() / 60
	return fmt.Sprintf("%s%d:%d", rpmKeyPrefix, accountID, minuteTS), nil
}

// currentMinuteSuffix 获取当前分钟时间戳后缀（供批量操作使用）
// 使用 rdb.Time() 获取 Redis 服务端时间
func (c *RPMCacheImpl) currentMinuteSuffix(ctx context.Context) (string, error) {
	serverTime, err := c.rdb.Time(ctx).Result()
	if err != nil {
		return "", fmt.Errorf("redis TIME: %w", err)
	}
	minuteTS := serverTime.Unix() / 60
	return strconv.FormatInt(minuteTS, 10), nil
}

// IncrementRPM 原子递增并返回当前分钟的计数
// 使用 TxPipeline (MULTI/EXEC) 执行 INCR + EXPIRE，保证原子性且兼容 Redis Cluster
func (c *RPMCacheImpl) IncrementRPM(ctx context.Context, accountID int64) (int, error) {
	key, err := c.currentMinuteKey(ctx, accountID)
	if err != nil {
		return 0, fmt.Errorf("rpm increment: %w", err)
	}

	// 使用 TxPipeline (MULTI/EXEC) 保证 INCR + EXPIRE 原子执行
	// EXPIRE 幂等，每次都设置不影响正确性
	pipe := c.rdb.TxPipeline()
	incrCmd := pipe.Incr(ctx, key)
	pipe.Expire(ctx, key, rpmKeyTTL)

	if _, err := pipe.Exec(ctx); err != nil {
		return 0, fmt.Errorf("rpm increment: %w", err)
	}

	return int(incrCmd.Val()), nil
}

// GetRPM 获取当前分钟的 RPM 计数
func (c *RPMCacheImpl) GetRPM(ctx context.Context, accountID int64) (int, error) {
	key, err := c.currentMinuteKey(ctx, accountID)
	if err != nil {
		return 0, fmt.Errorf("rpm get: %w", err)
	}

	val, err := c.rdb.Get(ctx, key).Int()
	if errors.Is(err, redis.Nil) {
		return 0, nil // 当前分钟无记录
	}
	if err != nil {
		return 0, fmt.Errorf("rpm get: %w", err)
	}
	return val, nil
}

// GetRPMBatch 批量获取多个账号的 RPM 计数（使用 Pipeline）
func (c *RPMCacheImpl) GetRPMBatch(ctx context.Context, accountIDs []int64) (map[int64]int, error) {
	if len(accountIDs) == 0 {
		return map[int64]int{}, nil
	}

	// 获取当前分钟后缀
	minuteSuffix, err := c.currentMinuteSuffix(ctx)
	if err != nil {
		return nil, fmt.Errorf("rpm batch get: %w", err)
	}

	// 使用 Pipeline 批量 GET
	pipe := c.rdb.Pipeline()
	cmds := make(map[int64]*redis.StringCmd, len(accountIDs))
	for _, id := range accountIDs {
		key := fmt.Sprintf("%s%d:%s", rpmKeyPrefix, id, minuteSuffix)
		cmds[id] = pipe.Get(ctx, key)
	}

	if _, err := pipe.Exec(ctx); err != nil && !errors.Is(err, redis.Nil) {
		return nil, fmt.Errorf("rpm batch get: %w", err)
	}

	result := make(map[int64]int, len(accountIDs))
	for id, cmd := range cmds {
		if val, err := cmd.Int(); err == nil {
			result[id] = val
		} else {
			result[id] = 0
		}
	}
	return result, nil
}
