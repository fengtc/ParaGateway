using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParaGateway.Frontend.Models;

/// <summary>
/// 官方 Go 首次安装向导的请求契约。字段名与 /setup API 保持一致，
/// 这样 Blazor WASM 可以直接替换官方 Vue 安装页。
/// </summary>
public sealed class SetupDatabaseInput
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string User { get; set; } = "postgres";
    public string Password { get; set; } = string.Empty;
    [JsonPropertyName("dbname")] public string DatabaseName { get; set; } = "paragateway";
    [JsonPropertyName("sslmode")] public string SslMode { get; set; } = "disable";
}

public sealed class SetupRedisInput
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Db { get; set; }
    [JsonPropertyName("enable_tls")] public bool EnableTls { get; set; }
}

public sealed class SetupAdminInput
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class SetupServerInput
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8080;
    public string Mode { get; set; } = "release";
}

public sealed class SetupInstallInput
{
    public SetupDatabaseInput Database { get; set; } = new();
    public SetupRedisInput Redis { get; set; } = new();
    public SetupAdminInput Admin { get; set; } = new();
    public SetupServerInput Server { get; set; } = new();
}

public sealed class SetupConnectionResultDto
{
    public string Message { get; set; } = string.Empty;
}

public sealed class SetupInstallResultDto
{
    public string Message { get; set; } = string.Empty;
    public bool Restart { get; set; }
}

public sealed class AuthUser
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public decimal Balance { get; set; }
    public decimal FrozenBalance { get; set; }
    public int Concurrency { get; set; }
    public int RpmLimit { get; set; }
    public string RunMode { get; set; } = "standard";
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonIgnore]
    public bool IsAdmin => string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsSimpleMode => string.Equals(RunMode, "simple", StringComparison.OrdinalIgnoreCase);

    public static AuthUser From(GoUser user) => new()
    {
        Id = user.Id.ToString(),
        Email = user.Email,
        DisplayName = user.Username,
        Role = user.Role,
        Balance = user.Balance,
        FrozenBalance = user.FrozenBalance,
        Concurrency = user.Concurrency,
        RpmLimit = user.RpmLimit,
        RunMode = string.IsNullOrWhiteSpace(user.RunMode) ? "standard" : user.RunMode,
        ExpiresAt = null
    };
}

public sealed class AuthResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "Bearer";
    public GoUser? User { get; set; }
    [JsonPropertyName("requires_2fa")] public bool Requires2FA { get; set; }
    [JsonPropertyName("temp_token")] public string? TempToken { get; set; }
    [JsonPropertyName("user_email_masked")] public string? UserEmailMasked { get; set; }
}

/// <summary>认证入口使用的一次性验证码证明。服务端会按当前启用的供应商校验。</summary>
public sealed class CaptchaProof
{
    [JsonPropertyName("turnstile_token")] public string? TurnstileToken { get; set; }
    [JsonPropertyName("tencent_captcha_ticket")] public string? TencentCaptchaTicket { get; set; }
    [JsonPropertyName("tencent_captcha_randstr")] public string? TencentCaptchaRandstr { get; set; }

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(TurnstileToken)
        && string.IsNullOrWhiteSpace(TencentCaptchaTicket);
}

/// <summary>OAuth 回调和 pending-auth 接口使用的前端安全响应。</summary>
public class OAuthTokenResponseDto
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "Bearer";
}

public sealed class OAuthCompletionDto : OAuthTokenResponseDto
{
    [JsonPropertyName("countdown")] public int Countdown { get; set; }
    [JsonPropertyName("auth_result")] public string? AuthResult { get; set; }
    [JsonPropertyName("provider")] public string? Provider { get; set; }
    [JsonPropertyName("intent")] public string? Intent { get; set; }
    [JsonPropertyName("redirect")] public string? Redirect { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("detail")] public string? Detail { get; set; }
    [JsonPropertyName("step")] public string? Step { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("resolved_email")] public string? ResolvedEmail { get; set; }
    [JsonPropertyName("pending_email")] public string? PendingEmail { get; set; }
    [JsonPropertyName("existing_account_email")] public string? ExistingAccountEmail { get; set; }
    [JsonPropertyName("existing_account_bindable")] public bool ExistingAccountBindable { get; set; }
    [JsonPropertyName("create_account_allowed")] public bool CreateAccountAllowed { get; set; }
    [JsonPropertyName("force_email_on_signup")] public bool ForceEmailOnSignup { get; set; }
    [JsonPropertyName("email_binding_required")] public bool EmailBindingRequired { get; set; }
    [JsonPropertyName("requires_email_completion")] public bool RequiresEmailCompletion { get; set; }
    [JsonPropertyName("suggested_email")] public string? SuggestedEmail { get; set; }
    [JsonPropertyName("choice_reason")] public string? ChoiceReason { get; set; }
    [JsonPropertyName("invitation_required")] public bool InvitationRequired { get; set; }
    [JsonPropertyName("adoption_required")] public bool AdoptionRequired { get; set; }
    [JsonPropertyName("requires_2fa")] public bool Requires2FA { get; set; }
    [JsonPropertyName("temp_token")] public string? TempToken { get; set; }
    [JsonPropertyName("user_email_masked")] public string? UserEmailMasked { get; set; }
    [JsonPropertyName("suggested_display_name")] public string? SuggestedDisplayName { get; set; }
    [JsonPropertyName("suggested_avatar_url")] public string? SuggestedAvatarUrl { get; set; }

    public OAuthTokenResponseDto ToTokenResponse() => new()
    {
        AccessToken = AccessToken, RefreshToken = RefreshToken,
        ExpiresIn = ExpiresIn, TokenType = TokenType
    };
}

public sealed class GoUser
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    [JsonPropertyName("avatar_url")] public string AvatarUrl { get; set; } = string.Empty;
    [JsonPropertyName("avatar_source")] public UserProfileSourceContextDto? AvatarSource { get; set; }
    [JsonPropertyName("username_source")] public UserProfileSourceContextDto? UsernameSource { get; set; }
    [JsonPropertyName("display_name_source")] public UserProfileSourceContextDto? DisplayNameSource { get; set; }
    [JsonPropertyName("nickname_source")] public UserProfileSourceContextDto? NicknameSource { get; set; }
    [JsonPropertyName("profile_sources")] public Dictionary<string, UserProfileSourceContextDto?>? ProfileSources { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    [JsonPropertyName("run_mode")] public string RunMode { get; set; } = "standard";
    public decimal Balance { get; set; }
    [JsonPropertyName("frozen_balance")] public decimal FrozenBalance { get; set; }
    public int Concurrency { get; set; }
    [JsonPropertyName("rpm_limit")] public int RpmLimit { get; set; }
    [JsonPropertyName("tpm_limit")] public int TpmLimit { get; set; }
    public string Status { get; set; } = "active";
    [JsonPropertyName("allowed_groups")] public List<long> AllowedGroups { get; set; } = [];
    [JsonPropertyName("group_rates")] public Dictionary<long, double> GroupRates { get; set; } = [];
    [JsonPropertyName("current_concurrency")] public int CurrentConcurrency { get; set; }
    [JsonPropertyName("last_used_at")] public DateTimeOffset? LastUsedAt { get; set; }
    [JsonPropertyName("total_recharged")] public double TotalRecharged { get; set; }
    public List<GoUserSubscription> Subscriptions { get; set; } = [];
    [JsonPropertyName("balance_notify_enabled")] public bool BalanceNotifyEnabled { get; set; }
    [JsonPropertyName("balance_notify_threshold_type")] public string BalanceNotifyThresholdType { get; set; } = "";
    [JsonPropertyName("balance_notify_threshold")] public double? BalanceNotifyThreshold { get; set; }
    [JsonPropertyName("balance_notify_extra_emails")] public List<NotifyEmailEntryDto> BalanceNotifyExtraEmails { get; set; } = [];
    [JsonPropertyName("last_active_at")] public DateTimeOffset? LastActiveAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted => !string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);
    [JsonPropertyName("identities")] public UserIdentitySummarySetDto? Identities { get; set; }
    [JsonPropertyName("auth_bindings")] public Dictionary<string, UserIdentitySummaryDto>? AuthBindings { get; set; }
    [JsonPropertyName("identity_bindings")] public Dictionary<string, UserIdentitySummaryDto>? IdentityBindings { get; set; }
    [JsonPropertyName("email_bound")] public bool EmailBound { get; set; }
    [JsonPropertyName("linuxdo_bound")] public bool LinuxDoBound { get; set; }
    [JsonPropertyName("oidc_bound")] public bool OidcBound { get; set; }
    [JsonPropertyName("wechat_bound")] public bool WeChatBound { get; set; }
    [JsonPropertyName("dingtalk_bound")] public bool DingTalkBound { get; set; }
}

public sealed class UserProfileSourceContextDto
{
    public string Provider { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class GoUserSubscription
{
    public long Id { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("starts_at")] public DateTimeOffset? StartsAt { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("quota_used")] public double QuotaUsed { get; set; }
    [JsonPropertyName("quota_limit")] public double QuotaLimit { get; set; }
    public GoGroup? Group { get; set; }
}

public sealed class PagedEnvelope<T>
{
    public List<T> Items { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; } = 1;
    [JsonPropertyName("page_size")] public int PageSize { get; set; } = 20;
    public int Pages { get; set; } = 1;
}

// 管理端高级资源 DTO。字段与官方 Go API 的 JSON 契约保持 snake_case 映射，
// secret 字段只承接后端返回的脱敏/是否已配置状态，不在前端缓存明文。
public sealed class BackupS3ConfigDto
{
    public string Endpoint { get; set; } = string.Empty;
    public string Region { get; set; } = "auto";
    public string Bucket { get; set; } = string.Empty;
    [JsonPropertyName("access_key_id")] public string AccessKeyId { get; set; } = string.Empty;
    [JsonPropertyName("secret_access_key")] public string? SecretAccessKey { get; set; }
    public string Prefix { get; set; } = "backups/";
    [JsonPropertyName("force_path_style")] public bool ForcePathStyle { get; set; }
}

public sealed class ImageStorageConfigDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("reuse_backup_s3")] public bool ReuseBackupS3 { get; set; } = true;
    public string Bucket { get; set; } = string.Empty;
    public string Prefix { get; set; } = "images/";
    [JsonPropertyName("public_base_url")] public string PublicBaseUrl { get; set; } = string.Empty;
    [JsonPropertyName("presign_expiry_hours")] public int PresignExpiryHours { get; set; } = 24;
    [JsonPropertyName("max_download_bytes")] public long MaxDownloadBytes { get; set; } = 33_554_432;
    public string Endpoint { get; set; } = string.Empty;
    public string Region { get; set; } = "auto";
    [JsonPropertyName("access_key_id")] public string AccessKeyId { get; set; } = string.Empty;
    [JsonPropertyName("secret_access_key")] public string? SecretAccessKey { get; set; }
    [JsonPropertyName("force_path_style")] public bool ForcePathStyle { get; set; }
}

public sealed class ImageStorageConfigResponseDto
{
    public ImageStorageConfigDto Config { get; set; } = new();
    [JsonPropertyName("secret_configured")] public bool SecretConfigured { get; set; }
}

public sealed class BackupScheduleDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("cron_expr")] public string CronExpr { get; set; } = "0 2 * * *";
    [JsonPropertyName("retain_days")] public int RetainDays { get; set; } = 14;
    [JsonPropertyName("retain_count")] public int RetainCount { get; set; } = 10;
}

public sealed class BackupPartDto
{
    public int Index { get; set; }
    [JsonPropertyName("s3_key")] public string S3Key { get; set; } = string.Empty;
    [JsonPropertyName("size_bytes")] public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
}

public sealed class BackupRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("backup_type")] public string BackupType { get; set; } = string.Empty;
    [JsonPropertyName("file_name")] public string FileName { get; set; } = string.Empty;
    [JsonPropertyName("s3_key")] public string S3Key { get; set; } = string.Empty;
    public List<BackupPartDto> Parts { get; set; } = [];
    [JsonPropertyName("size_bytes")] public long SizeBytes { get; set; }
    [JsonPropertyName("triggered_by")] public string TriggeredBy { get; set; } = string.Empty;
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("started_at")] public DateTimeOffset? StartedAt { get; set; }
    [JsonPropertyName("finished_at")] public DateTimeOffset? FinishedAt { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    public string? Progress { get; set; }
    [JsonPropertyName("restore_status")] public string? RestoreStatus { get; set; }
    [JsonPropertyName("restore_error")] public string? RestoreError { get; set; }
    [JsonPropertyName("restored_at")] public DateTimeOffset? RestoredAt { get; set; }
}

public sealed class BackupListDto
{
    public List<BackupRecordDto> Items { get; set; } = [];
}

public sealed class BackupDownloadPartDto
{
    public int Index { get; set; }
    [JsonPropertyName("size_bytes")] public long SizeBytes { get; set; }
    public string Url { get; set; } = string.Empty;
}

public sealed class BackupDownloadDto
{
    public string? Url { get; set; }
    public List<BackupDownloadPartDto> Parts { get; set; } = [];
}

public sealed class S3TestResultDto
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ErrorPassthroughRuleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    [JsonPropertyName("error_codes")] public List<int> ErrorCodes { get; set; } = [];
    public List<string> Keywords { get; set; } = [];
    [JsonPropertyName("match_mode")] public string MatchMode { get; set; } = "any";
    public List<string> Platforms { get; set; } = [];
    [JsonPropertyName("passthrough_code")] public bool PassthroughCode { get; set; } = true;
    [JsonPropertyName("response_code")] public int? ResponseCode { get; set; }
    [JsonPropertyName("passthrough_body")] public bool PassthroughBody { get; set; } = true;
    [JsonPropertyName("custom_message")] public string? CustomMessage { get; set; }
    [JsonPropertyName("skip_monitoring")] public bool SkipMonitoring { get; set; }
    public string? Description { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ErrorPassthroughRuleForm
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public string ErrorCodes { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string MatchMode { get; set; } = "any";
    public string Platforms { get; set; } = string.Empty;
    public bool PassthroughCode { get; set; } = true;
    public int? ResponseCode { get; set; }
    public bool PassthroughBody { get; set; } = true;
    public string CustomMessage { get; set; } = string.Empty;
    public bool SkipMonitoring { get; set; }
    public string Description { get; set; } = string.Empty;

    public static ErrorPassthroughRuleForm From(ErrorPassthroughRuleDto value) => new()
    {
        Name = value.Name, Enabled = value.Enabled, Priority = value.Priority,
        ErrorCodes = string.Join(", ", value.ErrorCodes), Keywords = string.Join(", ", value.Keywords),
        MatchMode = value.MatchMode, Platforms = string.Join(", ", value.Platforms),
        PassthroughCode = value.PassthroughCode, ResponseCode = value.ResponseCode,
        PassthroughBody = value.PassthroughBody, CustomMessage = value.CustomMessage ?? string.Empty,
        SkipMonitoring = value.SkipMonitoring, Description = value.Description ?? string.Empty
    };
}

public sealed class TlsFingerprintProfileDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [JsonPropertyName("enable_grease")] public bool EnableGrease { get; set; }
    [JsonPropertyName("cipher_suites")] public List<ushort> CipherSuites { get; set; } = [];
    public List<ushort> Curves { get; set; } = [];
    [JsonPropertyName("point_formats")] public List<ushort> PointFormats { get; set; } = [];
    [JsonPropertyName("signature_algorithms")] public List<ushort> SignatureAlgorithms { get; set; } = [];
    [JsonPropertyName("alpn_protocols")] public List<string> AlpnProtocols { get; set; } = [];
    [JsonPropertyName("supported_versions")] public List<ushort> SupportedVersions { get; set; } = [];
    [JsonPropertyName("key_share_groups")] public List<ushort> KeyShareGroups { get; set; } = [];
    [JsonPropertyName("psk_modes")] public List<ushort> PskModes { get; set; } = [];
    public List<ushort> Extensions { get; set; } = [];
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class TlsFingerprintProfileForm
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool EnableGrease { get; set; }
    public string CipherSuites { get; set; } = string.Empty;
    public string Curves { get; set; } = string.Empty;
    public string PointFormats { get; set; } = string.Empty;
    public string SignatureAlgorithms { get; set; } = string.Empty;
    public string AlpnProtocols { get; set; } = string.Empty;
    public string SupportedVersions { get; set; } = string.Empty;
    public string KeyShareGroups { get; set; } = string.Empty;
    public string PskModes { get; set; } = string.Empty;
    public string Extensions { get; set; } = string.Empty;

    public static TlsFingerprintProfileForm From(TlsFingerprintProfileDto value) => new()
    {
        Name = value.Name, Description = value.Description ?? string.Empty, EnableGrease = value.EnableGrease,
        CipherSuites = string.Join(", ", value.CipherSuites), Curves = string.Join(", ", value.Curves),
        PointFormats = string.Join(", ", value.PointFormats), SignatureAlgorithms = string.Join(", ", value.SignatureAlgorithms),
        AlpnProtocols = string.Join(", ", value.AlpnProtocols), SupportedVersions = string.Join(", ", value.SupportedVersions),
        KeyShareGroups = string.Join(", ", value.KeyShareGroups), PskModes = string.Join(", ", value.PskModes),
        Extensions = string.Join(", ", value.Extensions)
    };
}

public sealed class PromptAuditEndpointDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = "openai_compatible";
    [JsonPropertyName("base_url")] public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("timeout_ms")] public int TimeoutMs { get; set; }
    [JsonPropertyName("input_limit")] public int InputLimit { get; set; }
    public bool Enabled { get; set; }
    [JsonPropertyName("has_token")] public bool HasToken { get; set; }
    [JsonPropertyName("token_status")] public string TokenStatus { get; set; } = "missing";
    [JsonIgnore] public string Token { get; set; } = string.Empty;
    [JsonIgnore] public bool ClearToken { get; set; }
}

public sealed class PromptAuditConfigDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("blocking_enabled")] public bool BlockingEnabled { get; set; }
    [JsonPropertyName("blocking_latest_turn_only")] public bool BlockingLatestTurnOnly { get; set; }
    [JsonPropertyName("store_pass_events")] public bool StorePassEvents { get; set; }
    [JsonPropertyName("effective_mode")] public string EffectiveMode { get; set; } = "off";
    public string Strategy { get; set; } = "priority";
    [JsonPropertyName("worker_count")] public int WorkerCount { get; set; } = 4;
    [JsonPropertyName("queue_capacity")] public int QueueCapacity { get; set; } = 32768;
    public List<string> Scanners { get; set; } = [];
    [JsonPropertyName("all_groups")] public bool AllGroups { get; set; } = true;
    [JsonPropertyName("group_ids")] public List<long> GroupIds { get; set; } = [];
    public List<PromptAuditEndpointDto> Endpoints { get; set; } = [];
    [JsonPropertyName("config_version")] public long ConfigVersion { get; set; } = 1;
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("updated_by")] public long UpdatedBy { get; set; }
    [JsonPropertyName("change_summary")] public string ChangeSummary { get; set; } = string.Empty;
}

public sealed class PromptAuditRuntimeDto
{
    [JsonPropertyName("process_status")] public string ProcessStatus { get; set; } = string.Empty;
    [JsonPropertyName("effective_mode")] public string EffectiveMode { get; set; } = string.Empty;
    [JsonPropertyName("expected_config_version")] public long ExpectedConfigVersion { get; set; }
    [JsonPropertyName("active_config_version")] public long ActiveConfigVersion { get; set; }
    [JsonPropertyName("config_loaded_at")] public DateTimeOffset? ConfigLoadedAt { get; set; }
    [JsonPropertyName("config_load_error")] public string ConfigLoadError { get; set; } = string.Empty;
    [JsonPropertyName("worker_total")] public int WorkerTotal { get; set; }
    [JsonPropertyName("worker_active")] public int WorkerActive { get; set; }
    [JsonPropertyName("worker_heartbeat_at")] public DateTimeOffset? WorkerHeartbeatAt { get; set; }
    [JsonPropertyName("queue_capacity")] public int QueueCapacity { get; set; }
    public PromptAuditQueueDto Queue { get; set; } = new();
    [JsonPropertyName("processed_total")] public long ProcessedTotal { get; set; }
    [JsonPropertyName("failed_total")] public long FailedTotal { get; set; }
    [JsonPropertyName("enqueued_total")] public long EnqueuedTotal { get; set; }
    [JsonPropertyName("dropped_total")] public long DroppedTotal { get; set; }
    [JsonPropertyName("last_processed_at")] public DateTimeOffset? LastProcessedAt { get; set; }
    [JsonPropertyName("last_error_code")] public string LastErrorCode { get; set; } = string.Empty;
    [JsonPropertyName("last_error_message")] public string LastErrorMessage { get; set; } = string.Empty;
    [JsonPropertyName("database_status")] public string DatabaseStatus { get; set; } = string.Empty;
    [JsonPropertyName("redis_status")] public string RedisStatus { get; set; } = string.Empty;
    public Dictionary<string, PromptAuditProbeResultDto> Endpoints { get; set; } = [];
    [JsonPropertyName("guard_metrics")] public PromptAuditGuardMetricsDto GuardMetrics { get; set; } = new();
}
public sealed class PromptAuditQueueDto
{
    public long Staging { get; set; } public long Queued { get; set; } public long Processing { get; set; } public long Retry { get; set; } public long Done { get; set; } public long Failed { get; set; } public long Active { get; set; }
}
public sealed class PromptAuditGuardMetricsDto
{
    public long Total { get; set; } public long Allowed { get; set; } public long Flagged { get; set; } public long Blocked { get; set; } public long Unavailable { get; set; } public long Invalid { get; set; } public long Timeouts { get; set; } public long Failovers { get; set; }
    [JsonPropertyName("bulkhead_full")] public long BulkheadFull { get; set; } [JsonPropertyName("record_failed")] public long RecordFailed { get; set; }
    [JsonPropertyName("latency_avg_ms")] public double? LatencyAvgMs { get; set; } [JsonPropertyName("latency_p50_ms")] public double? LatencyP50Ms { get; set; } [JsonPropertyName("latency_p95_ms")] public double? LatencyP95Ms { get; set; } [JsonPropertyName("latency_p99_ms")] public double? LatencyP99Ms { get; set; } [JsonPropertyName("latency_max_ms")] public double? LatencyMaxMs { get; set; }
}
public sealed class PromptAuditProbeResultDto
{
    public bool Ok { get; set; } public string Status { get; set; } = string.Empty; [JsonPropertyName("error_code")] public string ErrorCode { get; set; } = string.Empty; public string Message { get; set; } = string.Empty;
    [JsonPropertyName("latency_ms")] public long LatencyMs { get; set; } [JsonPropertyName("http_status")] public int HttpStatus { get; set; } public bool Retryable { get; set; } [JsonPropertyName("checked_at")] public DateTimeOffset? CheckedAt { get; set; } [JsonPropertyName("token_applied")] public bool TokenApplied { get; set; }
}

public sealed class PromptAuditEventDto
{
    public long Id { get; set; }
    [JsonPropertyName("job_id")] public long JobId { get; set; }
    public PromptAuditSnapshotDto Snapshot { get; set; } = new();
    public string Decision { get; set; } = string.Empty;
    [JsonPropertyName("risk_level")] public string RiskLevel { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("latency_ms")] public int LatencyMs { get; set; }
    public List<string> Categories { get; set; } = [];
    [JsonPropertyName("matched_scanners")] public List<string> MatchedScanners { get; set; } = [];
    [JsonPropertyName("scanner_backend")] public string ScannerBackend { get; set; } = string.Empty;
    [JsonPropertyName("scanner_version")] public string ScannerVersion { get; set; } = string.Empty;
    [JsonPropertyName("scanner_scores")] public Dictionary<string, double> ScannerScores { get; set; } = [];
    [JsonPropertyName("scanner_evidence")] public Dictionary<string, string> ScannerEvidence { get; set; } = [];
    [JsonPropertyName("guard_endpoint_id")] public string GuardEndpointId { get; set; } = string.Empty;
    [JsonPropertyName("policy_id")] public string PolicyId { get; set; } = string.Empty;
    [JsonPropertyName("policy_version")] public int PolicyVersion { get; set; }
    [JsonPropertyName("config_version")] public long ConfigVersion { get; set; }
    [JsonPropertyName("chunk_total")] public int ChunkTotal { get; set; }
    [JsonPropertyName("issue_summaries")] public List<PromptAuditIssueDto> IssueSummaries { get; set; } = [];

    // Official Go returns request metadata inside Event.Snapshot. These
    // read-only projections keep the grid columns concise without flattening
    // or altering the wire contract.
    [JsonIgnore] public string Endpoint => Snapshot.Endpoint;
    [JsonIgnore] public string Model => Snapshot.Model;
    [JsonIgnore] public string RequestId => Snapshot.RequestId;
    [JsonIgnore] public string PromptHash => Snapshot.PromptHash;
    [JsonIgnore] public string RedactedPreview => Snapshot.RedactedPreview;
}

public sealed class PromptAuditIssueDto
{
    public string Category { get; set; } = string.Empty; [JsonPropertyName("scanner_id")] public string ScannerId { get; set; } = string.Empty; public string Title { get; set; } = string.Empty; public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; [JsonPropertyName("severity_label")] public string SeverityLabel { get; set; } = string.Empty; public string Action { get; set; } = string.Empty; [JsonPropertyName("action_label")] public string ActionLabel { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; public double Score { get; set; } public string Evidence { get; set; } = string.Empty; [JsonPropertyName("evidence_hash")] public string EvidenceHash { get; set; } = string.Empty; [JsonPropertyName("start_rune")] public int? StartRune { get; set; } [JsonPropertyName("end_rune")] public int? EndRune { get; set; }
}

public sealed class PromptAuditGroupDto { public long Id { get; set; } public string Name { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public string Platform { get; set; } = string.Empty; }
public sealed class PromptAuditEventFiltersDto
{
    public string Decision { get; set; } = string.Empty; [JsonPropertyName("risk_level")] public string RiskLevel { get; set; } = string.Empty; public string Endpoint { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public string GroupId { get; set; } = string.Empty; [JsonPropertyName("user_id")] public string UserId { get; set; } = string.Empty; [JsonPropertyName("api_key_id")] public string ApiKeyId { get; set; } = string.Empty;
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty; [JsonPropertyName("prompt_hash")] public string PromptHash { get; set; } = string.Empty; public string Keyword { get; set; } = string.Empty;
    [JsonPropertyName("start_at")] public string StartAt { get; set; } = string.Empty; [JsonPropertyName("end_at")] public string EndAt { get; set; } = string.Empty;
}
public sealed class PromptAuditDeleteResultDto { [JsonPropertyName("deleted_events")] public long DeletedEvents { get; set; } [JsonPropertyName("deleted_jobs")] public long DeletedJobs { get; set; } }
public sealed class PromptAuditDeletePreviewDto
{
    [JsonPropertyName("matched_count")] public long MatchedCount { get; set; } [JsonPropertyName("filter_summary")] public Dictionary<string, JsonElement> FilterSummary { get; set; } = []; [JsonPropertyName("snapshot_max_id")] public long SnapshotMaxId { get; set; }
    [JsonPropertyName("filter_hash")] public string FilterHash { get; set; } = string.Empty; [JsonPropertyName("confirmation_token")] public string ConfirmationToken { get; set; } = string.Empty; [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class PromptAuditSnapshotDto
{
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty;
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;
    [JsonPropertyName("api_key_id")] public long ApiKeyId { get; set; }
    [JsonPropertyName("api_key_name")] public string ApiKeyName { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("prompt_hash")] public string PromptHash { get; set; } = string.Empty;
    [JsonPropertyName("redacted_preview")] public string RedactedPreview { get; set; } = string.Empty;
    [JsonPropertyName("full_prompt")] public string? FullPrompt { get; set; }
    [JsonPropertyName("prompt_length")] public int PromptLength { get; set; }
    [JsonPropertyName("message_count")] public int MessageCount { get; set; }
    public string Stage { get; set; } = string.Empty;
}

public sealed class SystemVersionDto
{
    public string Version { get; set; } = string.Empty;
}

public sealed class SystemReleaseInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
    [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;
}

public sealed class SystemUpdateInfoDto
{
    [JsonPropertyName("current_version")] public string CurrentVersion { get; set; } = string.Empty;
    [JsonPropertyName("latest_version")] public string LatestVersion { get; set; } = string.Empty;
    [JsonPropertyName("has_update")] public bool HasUpdate { get; set; }
    [JsonPropertyName("release_info")] public SystemReleaseInfoDto? ReleaseInfo { get; set; }
    public bool Cached { get; set; }
    public string? Warning { get; set; }
    [JsonPropertyName("build_type")] public string BuildType { get; set; } = string.Empty;
}

public sealed class SystemActionResultDto
{
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("need_restart")] public bool NeedRestart { get; set; }
    [JsonPropertyName("already_up_to_date")] public bool AlreadyUpToDate { get; set; }
    [JsonPropertyName("current_version")] public string CurrentVersion { get; set; } = string.Empty;
    [JsonPropertyName("latest_version")] public string LatestVersion { get; set; } = string.Empty;
    [JsonPropertyName("operation_id")] public string OperationId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

public sealed class RollbackVersionDto
{
    public string Version { get; set; } = string.Empty;
    [JsonPropertyName("published_at")] public string PublishedAt { get; set; } = string.Empty;
    [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;
}

public sealed class RollbackVersionsDto
{
    public List<RollbackVersionDto> Versions { get; set; } = [];
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "请输入邮箱")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密码")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("turnstile_token")] public string? TurnstileToken { get; set; }
    [JsonPropertyName("tencent_captcha_ticket")] public string? TencentCaptchaTicket { get; set; }
    [JsonPropertyName("tencent_captcha_randstr")] public string? TencentCaptchaRandstr { get; set; }

    public void ApplyCaptcha(CaptchaProof? proof)
    {
        TurnstileToken = proof?.TurnstileToken;
        TencentCaptchaTicket = proof?.TencentCaptchaTicket;
        TencentCaptchaRandstr = proof?.TencentCaptchaRandstr;
    }
}

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "请输入邮箱")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入显示名称")]
    [MaxLength(100, ErrorMessage = "显示名称不能超过 100 个字符")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密码")]
    [MinLength(12, ErrorMessage = "密码至少需要 12 个字符")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("verify_code")]
    public string VerifyCode { get; set; } = string.Empty;

    [JsonPropertyName("turnstile_token")] public string? TurnstileToken { get; set; }
    [JsonPropertyName("tencent_captcha_ticket")] public string? TencentCaptchaTicket { get; set; }
    [JsonPropertyName("tencent_captcha_randstr")] public string? TencentCaptchaRandstr { get; set; }

    public void ApplyCaptcha(CaptchaProof? proof)
    {
        TurnstileToken = proof?.TurnstileToken;
        TencentCaptchaTicket = proof?.TencentCaptchaTicket;
        TencentCaptchaRandstr = proof?.TencentCaptchaRandstr;
    }

    [Required(ErrorMessage = "请再次输入密码")]
    [Compare(nameof(Password), ErrorMessage = "两次输入的密码不一致")]
    [JsonIgnore]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "请输入邮箱")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("turnstile_token")] public string? TurnstileToken { get; set; }
    [JsonPropertyName("tencent_captcha_ticket")] public string? TencentCaptchaTicket { get; set; }
    [JsonPropertyName("tencent_captcha_randstr")] public string? TencentCaptchaRandstr { get; set; }

    public void ApplyCaptcha(CaptchaProof? proof)
    {
        TurnstileToken = proof?.TurnstileToken;
        TencentCaptchaTicket = proof?.TencentCaptchaTicket;
        TencentCaptchaRandstr = proof?.TencentCaptchaRandstr;
    }
}

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "重置链接缺少 token")]
    public string Token { get; set; } = string.Empty;
    [Required(ErrorMessage = "请输入新密码"), MinLength(6, ErrorMessage = "密码至少需要 6 个字符")]
    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "请再次输入新密码"), Compare(nameof(NewPassword), ErrorMessage = "两次输入的新密码不一致")]
    [JsonIgnore]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class SendVerifyCodeRequest
{
    [Required, EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("turnstile_token")] public string? TurnstileToken { get; set; }
    [JsonPropertyName("tencent_captcha_ticket")] public string? TencentCaptchaTicket { get; set; }
    [JsonPropertyName("tencent_captcha_randstr")] public string? TencentCaptchaRandstr { get; set; }
    [JsonPropertyName("pending_auth_token")] public string? PendingAuthToken { get; set; }
    [JsonPropertyName("pending_oauth_token")] public string? PendingOAuthToken { get; set; }

    public void ApplyCaptcha(CaptchaProof? proof)
    {
        TurnstileToken = proof?.TurnstileToken;
        TencentCaptchaTicket = proof?.TencentCaptchaTicket;
        TencentCaptchaRandstr = proof?.TencentCaptchaRandstr;
    }
}

public sealed class SendVerifyCodeResponse
{
    public string Message { get; set; } = string.Empty;
    public int Countdown { get; set; }
}

public sealed class RegisterResult
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsActive { get; set; }
    public bool RequiresActivation { get; set; }
}

public sealed class ProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public UserProfileSourceContextDto? AvatarSource { get; set; }
    public UserProfileSourceContextDto? UsernameSource { get; set; }
    public UserProfileSourceContextDto? DisplayNameSource { get; set; }
    public UserProfileSourceContextDto? NicknameSource { get; set; }
    public Dictionary<string, UserProfileSourceContextDto?> ProfileSources { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Role { get; set; } = "user";
    public long BalanceMicros { get; set; }
    public long FrozenBalanceMicros { get; set; }
    public int MaxConcurrency { get; set; }
    public int RpmLimit { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public UserIdentitySummarySetDto Identities { get; set; } = new();
    public Dictionary<string, UserIdentitySummaryDto> AuthBindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, UserIdentitySummaryDto> IdentityBindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool EmailBound { get; set; }
    public bool LinuxDoBound { get; set; }
    public bool OidcBound { get; set; }
    public bool WeChatBound { get; set; }
    public bool DingTalkBound { get; set; }
    public bool BalanceNotifyEnabled { get; set; }
    public string BalanceNotifyThresholdType { get; set; } = "";
    public double? BalanceNotifyThreshold { get; set; }
    public List<NotifyEmailEntryDto> BalanceNotifyExtraEmails { get; set; } = [];

    public static ProfileDto From(GoUser user) => new()
    {
        Id = user.Id.ToString(), Email = user.Email, DisplayName = user.Username,
        AvatarUrl = user.AvatarUrl, AvatarSource = user.AvatarSource, UsernameSource = user.UsernameSource,
        DisplayNameSource = user.DisplayNameSource, NicknameSource = user.NicknameSource,
        ProfileSources = user.ProfileSources ?? new(StringComparer.OrdinalIgnoreCase),
        Role = user.Role, BalanceMicros = decimal.ToInt64(decimal.Round(user.Balance * 1_000_000m)),
        FrozenBalanceMicros = decimal.ToInt64(decimal.Round(user.FrozenBalance * 1_000_000m)),
        MaxConcurrency = user.Concurrency, RpmLimit = user.RpmLimit,
        IsActive = string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase),
        CreatedAt = user.CreatedAt, UpdatedAt = user.UpdatedAt, LastLoginAt = user.LastActiveAt,
        Identities = user.Identities ?? new(),
        AuthBindings = user.AuthBindings ?? new(StringComparer.OrdinalIgnoreCase),
        IdentityBindings = user.IdentityBindings ?? new(StringComparer.OrdinalIgnoreCase),
        EmailBound = user.EmailBound,
        LinuxDoBound = user.LinuxDoBound,
        OidcBound = user.OidcBound,
        WeChatBound = user.WeChatBound,
        DingTalkBound = user.DingTalkBound
        ,BalanceNotifyEnabled = user.BalanceNotifyEnabled
        ,BalanceNotifyThresholdType = user.BalanceNotifyThresholdType
        ,BalanceNotifyThreshold = user.BalanceNotifyThreshold
        ,BalanceNotifyExtraEmails = user.BalanceNotifyExtraEmails ?? []
    };
}

public sealed class NotifyEmailEntryDto
{
    public string Email { get; set; } = string.Empty;
    public bool Disabled { get; set; }
    public bool Verified { get; set; }
}

public sealed class TotpStatusDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("enabled_at")] public long? EnabledAtUnix { get; set; }
    [JsonPropertyName("feature_enabled")] public bool FeatureEnabled { get; set; }
    public DateTimeOffset? EnabledAt => EnabledAtUnix.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(EnabledAtUnix.Value)
        : null;
}

public sealed class TotpVerificationMethodDto
{
    public string Method { get; set; } = "password";
}

public sealed class TotpSetupResponseDto
{
    public string Secret { get; set; } = string.Empty;
    [JsonPropertyName("qr_code_url")] public string QrCodeUrl { get; set; } = string.Empty;
    [JsonPropertyName("setup_token")] public string SetupToken { get; set; } = string.Empty;
    public int Countdown { get; set; }
}

public sealed class StepUpVerificationDto
{
    public bool Verified { get; set; }
    [JsonPropertyName("expires_in")] public long ExpiresIn { get; set; }
}

public sealed class PasskeyCredentialDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("last_used_at")] public DateTimeOffset? LastUsedAt { get; set; }
    public bool Backup { get; set; }
}

public sealed class UserAttributeDefinitionDto
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public List<UserAttributeOptionDto> Options { get; set; } = [];
    public bool Required { get; set; }
    public JsonElement? Validation { get; set; }
    public string Placeholder { get; set; } = string.Empty;
    [JsonPropertyName("display_order")] public int DisplayOrder { get; set; }
    public bool Enabled { get; set; }
}

public sealed class UserAttributeOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class UserAttributeValueDto
{
    public long Id { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("attribute_id")] public long AttributeId { get; set; }
    public string Value { get; set; } = string.Empty;
}

public sealed class UserIdentitySummarySetDto
{
    public UserIdentitySummaryDto Email { get; set; } = new() { Provider = "email" };
    public UserIdentitySummaryDto LinuxDo { get; set; } = new() { Provider = "linuxdo" };
    public UserIdentitySummaryDto Oidc { get; set; } = new() { Provider = "oidc" };
    public UserIdentitySummaryDto WeChat { get; set; } = new() { Provider = "wechat" };
    public UserIdentitySummaryDto DingTalk { get; set; } = new() { Provider = "dingtalk" };

    public UserIdentitySummaryDto For(string provider) => provider.Trim().ToLowerInvariant() switch
    {
        "email" => Email,
        "linuxdo" => LinuxDo,
        "oidc" => Oidc,
        "wechat" => WeChat,
        "dingtalk" => DingTalk,
        _ => new() { Provider = provider }
    };
}

public sealed class UserIdentitySummaryDto
{
    [JsonPropertyName("provider")] public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("bound")] public bool Bound { get; set; }
    [JsonPropertyName("bound_count")] public int BoundCount { get; set; }
    [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    [JsonPropertyName("subject_hint")] public string? SubjectHint { get; set; }
    [JsonPropertyName("provider_key")] public string? ProviderKey { get; set; }
    [JsonPropertyName("verified_at")] public DateTimeOffset? VerifiedAt { get; set; }
    [JsonPropertyName("bind_start_path")] public string? BindStartPath { get; set; }
    [JsonPropertyName("can_bind")] public bool? CanBind { get; set; }
    [JsonPropertyName("can_unbind")] public bool CanUnbind { get; set; }
    [JsonPropertyName("note_key")] public string? NoteKey { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class ProfileUpdate
{
    [Required(ErrorMessage = "请输入显示名称")]
    [MaxLength(100, ErrorMessage = "显示名称不能超过 100 个字符")]
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool? BalanceNotifyEnabled { get; set; }
    public double? BalanceNotifyThreshold { get; set; }
}

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "请输入当前密码")]
    [JsonPropertyName("currentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入新密码")]
    [MinLength(8, ErrorMessage = "新密码至少需要 8 个字符")]
    [JsonPropertyName("newPassword")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "请再次输入新密码")]
    [Compare(nameof(NewPassword), ErrorMessage = "两次输入的新密码不一致")]
    [JsonIgnore]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class DashboardDto
{
    public long TotalUsers { get; set; }
    public long ActiveApiKeys { get; set; }
    public long TotalBalanceMicros { get; set; }
    public long UsageCostMicros { get; set; }
    public long TotalRequests { get; set; }
    public double SuccessRate { get; set; }
    public List<UsageRecordDto> RecentUsage { get; set; } = [];

    public static DashboardDto From(AdminDashboardStats stats) => new()
    {
        TotalUsers = stats.TotalUsers, ActiveApiKeys = stats.ActiveApiKeys,
        TotalRequests = stats.TotalRequests, UsageCostMicros = decimal.ToInt64(decimal.Round((decimal)stats.TotalActualCost * 1_000_000m)),
        SuccessRate = stats.TotalRequests > 0 ? Math.Max(0, 100d - (stats.ErrorRate * 100d)) : 0
    };

    public static DashboardDto From(UserDashboardStats stats) => new()
    {
        ActiveApiKeys = stats.ActiveApiKeys, TotalRequests = stats.TotalRequests,
        UsageCostMicros = decimal.ToInt64(decimal.Round((decimal)stats.TotalActualCost * 1_000_000m)),
        SuccessRate = 0
    };
}

public sealed class AdminDashboardStats
{
    [JsonPropertyName("total_users")] public long TotalUsers { get; set; }
    [JsonPropertyName("today_new_users")] public long TodayNewUsers { get; set; }
    [JsonPropertyName("active_users")] public long ActiveUsers { get; set; }
    [JsonPropertyName("hourly_active_users")] public long HourlyActiveUsers { get; set; }
    [JsonPropertyName("stats_updated_at")] public string? StatsUpdatedAt { get; set; }
    [JsonPropertyName("stats_stale")] public bool StatsStale { get; set; }
    [JsonPropertyName("total_api_keys")] public long TotalApiKeys { get; set; }
    [JsonPropertyName("active_api_keys")] public long ActiveApiKeys { get; set; }
    [JsonPropertyName("total_accounts")] public long TotalAccounts { get; set; }
    [JsonPropertyName("normal_accounts")] public long NormalAccounts { get; set; }
    [JsonPropertyName("error_accounts")] public long ErrorAccounts { get; set; }
    [JsonPropertyName("ratelimit_accounts")] public long RateLimitAccounts { get; set; }
    [JsonPropertyName("overload_accounts")] public long OverloadAccounts { get; set; }
    [JsonPropertyName("total_requests")] public long TotalRequests { get; set; }
    [JsonPropertyName("total_input_tokens")] public long TotalInputTokens { get; set; }
    [JsonPropertyName("total_output_tokens")] public long TotalOutputTokens { get; set; }
    [JsonPropertyName("total_cache_creation_tokens")] public long TotalCacheCreationTokens { get; set; }
    [JsonPropertyName("total_cache_read_tokens")] public long TotalCacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("total_cost")] public double TotalCost { get; set; }
    [JsonPropertyName("total_actual_cost")] public double TotalActualCost { get; set; }
    [JsonPropertyName("total_account_cost")] public double TotalAccountCost { get; set; }
    [JsonPropertyName("today_requests")] public long TodayRequests { get; set; }
    [JsonPropertyName("today_input_tokens")] public long TodayInputTokens { get; set; }
    [JsonPropertyName("today_output_tokens")] public long TodayOutputTokens { get; set; }
    [JsonPropertyName("today_cache_creation_tokens")] public long TodayCacheCreationTokens { get; set; }
    [JsonPropertyName("today_cache_read_tokens")] public long TodayCacheReadTokens { get; set; }
    [JsonPropertyName("today_tokens")] public long TodayTokens { get; set; }
    [JsonPropertyName("today_cost")] public double TodayCost { get; set; }
    [JsonPropertyName("today_actual_cost")] public double TodayActualCost { get; set; }
    [JsonPropertyName("today_account_cost")] public double TodayAccountCost { get; set; }
    [JsonPropertyName("average_duration_ms")] public double AverageDurationMs { get; set; }
    [JsonPropertyName("uptime")] public long Uptime { get; set; }
    [JsonPropertyName("rpm")] public long Rpm { get; set; }
    [JsonPropertyName("tpm")] public long Tpm { get; set; }
    [JsonPropertyName("error_rate")] public double ErrorRate { get; set; }
}

public sealed class AdminDashboardSnapshotDto
{
    [JsonPropertyName("generated_at")] public string GeneratedAt { get; set; } = string.Empty;
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
    [JsonPropertyName("granularity")] public string Granularity { get; set; } = "hour";
    [JsonPropertyName("stats")] public AdminDashboardStats? Stats { get; set; }
    [JsonPropertyName("trend")] public List<AdminDashboardTrendPointDto> Trend { get; set; } = [];
    [JsonPropertyName("models")] public List<AdminDashboardModelStatDto> Models { get; set; } = [];
    [JsonPropertyName("users_trend")] public List<AdminDashboardUserTrendPointDto> UsersTrend { get; set; } = [];
}

public sealed class AdminDashboardTrendPointDto
{
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_tokens")] public long CacheCreationTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("cost")] public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }

    [JsonIgnore]
    public double CacheHitRate
    {
        get
        {
            var promptTokens = InputTokens + CacheCreationTokens + CacheReadTokens;
            return promptTokens > 0 ? CacheReadTokens * 100d / promptTokens : 0d;
        }
    }
}

public sealed class AdminDashboardModelStatDto
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_tokens")] public long CacheCreationTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("cost")] public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
    [JsonPropertyName("account_cost")] public double AccountCost { get; set; }
}

public sealed class AdminDashboardUserTrendPointDto
{
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("tokens")] public long Tokens { get; set; }
    [JsonPropertyName("cost")] public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
}

public sealed class AdminDashboardRankingResponseDto
{
    [JsonPropertyName("ranking")] public List<AdminDashboardRankingItemDto> Ranking { get; set; } = [];
    [JsonPropertyName("total_actual_cost")] public double TotalActualCost { get; set; }
    [JsonPropertyName("total_requests")] public long TotalRequests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
}

public sealed class AdminDashboardRankingItemDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("tokens")] public long Tokens { get; set; }
}

public sealed class AdminDashboardUserBreakdownQueryDto
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public long? ApiKeyId { get; set; }
    public long? AccountId { get; set; }
    public long? GroupId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string ModelSource { get; set; } = "requested";
    public string Endpoint { get; set; } = string.Empty;
    public string EndpointType { get; set; } = "inbound";
    public string RequestType { get; set; } = string.Empty;
    public bool? Stream { get; set; }
    public int? BillingType { get; set; }
    public string SortBy { get; set; } = string.Empty;
    public int Limit { get; set; } = 50;
    public bool EndExclusive { get; set; }
}
public sealed class AdminDashboardUserBreakdownResponseDto
{
    [JsonPropertyName("users")] public List<AdminDashboardUserBreakdownItemDto> Users { get; set; } = [];
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
}

public sealed class AdminDashboardUserBreakdownItemDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_tokens")] public long CacheTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("cost")] public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
    [JsonPropertyName("account_cost")] public double AccountCost { get; set; }
}

public sealed class UserDashboardStats
{
    [JsonPropertyName("total_api_keys")] public long TotalApiKeys { get; set; }
    [JsonPropertyName("active_api_keys")] public long ActiveApiKeys { get; set; }
    [JsonPropertyName("total_requests")] public long TotalRequests { get; set; }
    [JsonPropertyName("total_input_tokens")] public long TotalInputTokens { get; set; }
    [JsonPropertyName("total_output_tokens")] public long TotalOutputTokens { get; set; }
    [JsonPropertyName("total_cache_creation_tokens")] public long TotalCacheCreationTokens { get; set; }
    [JsonPropertyName("total_cache_read_tokens")] public long TotalCacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("total_cost")] public double TotalCost { get; set; }
    [JsonPropertyName("total_actual_cost")] public double TotalActualCost { get; set; }
    [JsonPropertyName("today_requests")] public long TodayRequests { get; set; }
    [JsonPropertyName("today_input_tokens")] public long TodayInputTokens { get; set; }
    [JsonPropertyName("today_output_tokens")] public long TodayOutputTokens { get; set; }
    [JsonPropertyName("today_cache_creation_tokens")] public long TodayCacheCreationTokens { get; set; }
    [JsonPropertyName("today_cache_read_tokens")] public long TodayCacheReadTokens { get; set; }
    [JsonPropertyName("today_tokens")] public long TodayTokens { get; set; }
    [JsonPropertyName("today_cost")] public double TodayCost { get; set; }
    [JsonPropertyName("today_actual_cost")] public double TodayActualCost { get; set; }
    [JsonPropertyName("average_duration_ms")] public double AverageDurationMs { get; set; }
    [JsonPropertyName("rpm")] public long Rpm { get; set; }
    [JsonPropertyName("tpm")] public long Tpm { get; set; }
    [JsonPropertyName("by_platform")] public List<UserDashboardPlatformStatsDto> ByPlatform { get; set; } = [];
}

public sealed class UserDashboardPlatformStatsDto
{
    [JsonPropertyName("platform")] public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("total_requests")] public long TotalRequests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("total_actual_cost")] public double TotalActualCost { get; set; }
    [JsonPropertyName("today_requests")] public long TodayRequests { get; set; }
    [JsonPropertyName("today_tokens")] public long TodayTokens { get; set; }
    [JsonPropertyName("today_actual_cost")] public double TodayActualCost { get; set; }
}

public sealed class UserDashboardTrendResponseDto
{
    public List<UserUsageTrendPointDto> Trend { get; set; } = [];
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
    public string Granularity { get; set; } = "day";
}

public sealed class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool HasPassword { get; set; }
    public long BalanceMicros { get; set; }
    public int MaxConcurrency { get; set; } = 4;
    public int RpmLimit { get; set; } = 60;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static UserDto From(GoUser user) => new()
    {
        Id = user.Id.ToString(), Email = user.Email, DisplayName = user.Username, Role = user.Role,
        BalanceMicros = decimal.ToInt64(decimal.Round(user.Balance * 1_000_000m)),
        MaxConcurrency = user.Concurrency, RpmLimit = user.RpmLimit,
        IsActive = string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase),
        LastLoginAt = user.LastActiveAt, CreatedAt = user.CreatedAt
    };
}

public sealed class AdminBatchUpdateResultDto
{
    public int Affected { get; set; }
}

public sealed class AdminBalanceHistoryItemDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("used_by")] public long? UsedBy { get; set; }
    [JsonPropertyName("used_at")] public DateTimeOffset? UsedAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("validity_days")] public int ValidityDays { get; set; }
    public string Notes { get; set; } = string.Empty;
    public GoGroup? Group { get; set; }
}

public sealed class AdminBalanceHistoryResponseDto
{
    public List<AdminBalanceHistoryItemDto> Items { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; } = 1;
    [JsonPropertyName("page_size")] public int PageSize { get; set; } = 15;
    public int Pages { get; set; } = 1;
    [JsonPropertyName("total_recharged")] public double TotalRecharged { get; set; }
}

public sealed class AdminPlatformQuotaDto
{
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("daily_limit_usd")] public double? DailyLimitUsd { get; set; }
    [JsonPropertyName("weekly_limit_usd")] public double? WeeklyLimitUsd { get; set; }
    [JsonPropertyName("monthly_limit_usd")] public double? MonthlyLimitUsd { get; set; }
    [JsonPropertyName("daily_usage_usd")] public double DailyUsageUsd { get; set; }
    [JsonPropertyName("weekly_usage_usd")] public double WeeklyUsageUsd { get; set; }
    [JsonPropertyName("monthly_usage_usd")] public double MonthlyUsageUsd { get; set; }
    [JsonPropertyName("daily_window_start")] public DateTimeOffset? DailyWindowStart { get; set; }
    [JsonPropertyName("weekly_window_start")] public DateTimeOffset? WeeklyWindowStart { get; set; }
    [JsonPropertyName("monthly_window_start")] public DateTimeOffset? MonthlyWindowStart { get; set; }
    [JsonPropertyName("daily_window_resets_at")] public DateTimeOffset? DailyWindowResetsAt { get; set; }
    [JsonPropertyName("weekly_window_resets_at")] public DateTimeOffset? WeeklyWindowResetsAt { get; set; }
    [JsonPropertyName("monthly_window_resets_at")] public DateTimeOffset? MonthlyWindowResetsAt { get; set; }
}

public sealed class AdminPlatformQuotaResponseDto
{
    [JsonPropertyName("platform_quotas")] public List<AdminPlatformQuotaDto> PlatformQuotas { get; set; } = [];
}

public sealed class UserPlatformQuotaDto
{
    [JsonPropertyName("platform")] public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("daily_limit_usd")] public double? DailyLimitUsd { get; set; }
    [JsonPropertyName("weekly_limit_usd")] public double? WeeklyLimitUsd { get; set; }
    [JsonPropertyName("monthly_limit_usd")] public double? MonthlyLimitUsd { get; set; }
    [JsonPropertyName("daily_usage_usd")] public double DailyUsageUsd { get; set; }
    [JsonPropertyName("weekly_usage_usd")] public double WeeklyUsageUsd { get; set; }
    [JsonPropertyName("monthly_usage_usd")] public double MonthlyUsageUsd { get; set; }
    [JsonPropertyName("daily_window_resets_at")] public DateTimeOffset? DailyWindowResetsAt { get; set; }
    [JsonPropertyName("weekly_window_resets_at")] public DateTimeOffset? WeeklyWindowResetsAt { get; set; }
    [JsonPropertyName("monthly_window_resets_at")] public DateTimeOffset? MonthlyWindowResetsAt { get; set; }
}

public sealed class UserPlatformQuotaResponseDto
{
    [JsonPropertyName("platform_quotas")] public List<UserPlatformQuotaDto> PlatformQuotas { get; set; } = [];
}

public sealed class AdminPlatformUsageDto
{
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("today_actual_cost")] public double TodayActualCost { get; set; }
    [JsonPropertyName("total_actual_cost")] public double TotalActualCost { get; set; }
}

public sealed class AdminBatchUserUsageDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("today_actual_cost")] public double TodayActualCost { get; set; }
    [JsonPropertyName("total_actual_cost")] public double TotalActualCost { get; set; }
    [JsonPropertyName("by_platform")] public List<AdminPlatformUsageDto> ByPlatform { get; set; } = [];
}

public sealed class AdminBatchUsersUsageResponseDto
{
    public Dictionary<long, AdminBatchUserUsageDto> Stats { get; set; } = [];
}

public sealed class AdminBatchUserAttributesResponseDto
{
    public Dictionary<long, Dictionary<long, string>> Attributes { get; set; } = [];
}

public sealed class UserInput
{
    [Required(ErrorMessage = "请输入用户邮箱")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入显示名称")]
    [MaxLength(80, ErrorMessage = "显示名称不能超过 80 个字符")]
    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = "user";

    [MinLength(12, ErrorMessage = "密码至少需要 12 个字符")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }

    [Range(0, long.MaxValue, ErrorMessage = "余额不能为负数")]
    public long BalanceMicros { get; set; }

    [Range(1, 10000, ErrorMessage = "最大并发需要在 1 到 10000 之间")]
    public int MaxConcurrency { get; set; } = 4;

    [Range(1, 1000000, ErrorMessage = "RPM 限制需要在 1 到 1000000 之间")]
    public int RpmLimit { get; set; } = 60;

    public bool IsActive { get; set; } = true;
}

public sealed class ApiKeyDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public string Status { get; set; } = "active";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public string LastUsedIp { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
    public long? GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string GroupPlatform { get; set; } = string.Empty;
    public bool GroupAllowsBatchImageGeneration { get; set; }
    public bool GroupAllowsMessagesDispatch { get; set; }
    public GoGroup? Group { get; set; }
    public List<string> IpWhitelist { get; set; } = [];
    public List<string> IpBlacklist { get; set; } = [];
    public int CurrentConcurrency { get; set; }
    public double Quota { get; set; }
    public double QuotaUsed { get; set; }
    public double RateLimit5h { get; set; }
    public double RateLimit1d { get; set; }
    public double RateLimit7d { get; set; }
    public double Usage5h { get; set; }
    public double Usage1d { get; set; }
    public double Usage7d { get; set; }
    public DateTimeOffset? Window5hStart { get; set; }
    public DateTimeOffset? Window1dStart { get; set; }
    public DateTimeOffset? Window7dStart { get; set; }
    public DateTimeOffset? Reset5hAt { get; set; }
    public DateTimeOffset? Reset1dAt { get; set; }
    public DateTimeOffset? Reset7dAt { get; set; }

    public static ApiKeyDto From(GoApiKey key) => new()
    {
        Id = key.Id.ToString(), UserId = key.UserId.ToString(), Key = key.Key, Name = key.Name,
        Secret = string.IsNullOrWhiteSpace(key.Key) ? null : key.Key,
        Prefix = string.IsNullOrWhiteSpace(key.Key) ? string.Empty : key.Key[..Math.Min(8, key.Key.Length)],
        Status = key.Status,
        IsActive = string.Equals(key.Status, "active", StringComparison.OrdinalIgnoreCase),
        CreatedAt = key.CreatedAt, UpdatedAt = key.UpdatedAt, LastUsedAt = key.LastUsedAt,
        LastUsedIp = key.LastUsedIp ?? string.Empty, ExpiresAt = key.ExpiresAt,
        GroupId = key.GroupId, Quota = key.Quota, QuotaUsed = key.QuotaUsed,
        IpWhitelist = key.IpWhitelist ?? [], IpBlacklist = key.IpBlacklist ?? [],
        CurrentConcurrency = key.CurrentConcurrency,
        RateLimit5h = key.RateLimit5h, RateLimit1d = key.RateLimit1d, RateLimit7d = key.RateLimit7d,
        Usage5h = key.Usage5h, Usage1d = key.Usage1d, Usage7d = key.Usage7d,
        Window5hStart = key.Window5hStart, Window1dStart = key.Window1dStart, Window7dStart = key.Window7dStart,
        Reset5hAt = key.Reset5hAt, Reset1dAt = key.Reset1dAt, Reset7dAt = key.Reset7dAt,
        UserEmail = key.User?.Email ?? string.Empty,
        GroupName = key.Group?.Name ?? string.Empty,
        GroupPlatform = key.Group?.Platform ?? string.Empty,
        GroupAllowsBatchImageGeneration = key.Group?.AllowBatchImageGeneration ?? false,
        GroupAllowsMessagesDispatch = key.Group?.AllowMessagesDispatch ?? false,
        Group = key.Group
    };
}

public sealed class GoApiKey
{
    public long Id { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    [JsonPropertyName("ip_whitelist")] public List<string> IpWhitelist { get; set; } = [];
    [JsonPropertyName("ip_blacklist")] public List<string> IpBlacklist { get; set; } = [];
    [JsonPropertyName("last_used_at")] public DateTimeOffset? LastUsedAt { get; set; }
    [JsonPropertyName("last_used_ip")] public string? LastUsedIp { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("quota")] public double Quota { get; set; }
    [JsonPropertyName("quota_used")] public double QuotaUsed { get; set; }
    [JsonPropertyName("current_concurrency")] public int CurrentConcurrency { get; set; }
    [JsonPropertyName("rate_limit_5h")] public double RateLimit5h { get; set; }
    [JsonPropertyName("rate_limit_1d")] public double RateLimit1d { get; set; }
    [JsonPropertyName("rate_limit_7d")] public double RateLimit7d { get; set; }
    [JsonPropertyName("usage_5h")] public double Usage5h { get; set; }
    [JsonPropertyName("usage_1d")] public double Usage1d { get; set; }
    [JsonPropertyName("usage_7d")] public double Usage7d { get; set; }
    [JsonPropertyName("window_5h_start")] public DateTimeOffset? Window5hStart { get; set; }
    [JsonPropertyName("window_1d_start")] public DateTimeOffset? Window1dStart { get; set; }
    [JsonPropertyName("window_7d_start")] public DateTimeOffset? Window7dStart { get; set; }
    [JsonPropertyName("reset_5h_at")] public DateTimeOffset? Reset5hAt { get; set; }
    [JsonPropertyName("reset_1d_at")] public DateTimeOffset? Reset1dAt { get; set; }
    [JsonPropertyName("reset_7d_at")] public DateTimeOffset? Reset7dAt { get; set; }
    public GoUser? User { get; set; }
    public GoGroup? Group { get; set; }
}

public sealed class GatewayUsageResponse
{
    public string Mode { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PlanName { get; set; }
    public string Unit { get; set; } = "USD";
    public double? Remaining { get; set; }
    public GatewayUsageQuota? Quota { get; set; }
    public GatewayUsageSubscription? Subscription { get; set; }
    public GatewayUsageSummary? Usage { get; set; }
    [JsonPropertyName("daily_usage")] public List<ApiKeyDailyUsagePoint> DailyUsage { get; set; } = [];
    [JsonPropertyName("model_stats")] public List<ApiKeyModelUsagePoint> ModelStats { get; set; } = [];
    [JsonPropertyName("rate_limits")] public List<GatewayRateLimit> RateLimits { get; set; } = [];
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("days_until_expiry")] public int? DaysUntilExpiry { get; set; }
}

public sealed class GatewayUsageQuota
{
    public double Limit { get; set; }
    public double Used { get; set; }
    public double Remaining { get; set; }
    public string Unit { get; set; } = "USD";
}

public sealed class GatewayUsageSubscription
{
    [JsonPropertyName("daily_usage_usd")] public double DailyUsageUsd { get; set; }
    [JsonPropertyName("weekly_usage_usd")] public double WeeklyUsageUsd { get; set; }
    [JsonPropertyName("monthly_usage_usd")] public double MonthlyUsageUsd { get; set; }
    [JsonPropertyName("daily_limit_usd")] public double DailyLimitUsd { get; set; }
    [JsonPropertyName("weekly_limit_usd")] public double WeeklyLimitUsd { get; set; }
    [JsonPropertyName("monthly_limit_usd")] public double MonthlyLimitUsd { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class GatewayUsageSummary
{
    public GatewayUsagePeriod Today { get; set; } = new();
    public GatewayUsagePeriod Total { get; set; } = new();
    [JsonPropertyName("average_duration_ms")] public double AverageDurationMs { get; set; }
    public double Rpm { get; set; }
    public double Tpm { get; set; }
}

public sealed class GatewayUsagePeriod
{
    public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_tokens")] public long CacheCreationTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
}

public sealed class GatewayRateLimit
{
    public string Window { get; set; } = string.Empty;
    public double Limit { get; set; }
    public double Used { get; set; }
    public double Remaining { get; set; }
    [JsonPropertyName("reset_at")] public DateTimeOffset? ResetAt { get; set; }
}

public sealed class ApiKeyDailyUsagePoint
{
    public string Date { get; set; } = string.Empty;
    public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; }
    [JsonPropertyName("cache_write_tokens")] public long CacheWriteTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
}

public sealed class ApiKeyModelUsagePoint
{
    public string Model { get; set; } = string.Empty;
    public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_tokens")] public long CacheCreationTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
}

public sealed class ModelPlazaResponse
{
    public string Description { get; set; } = string.Empty;
    public List<ModelPlazaGroup> Groups { get; set; } = [];
}

public sealed class ModelPlazaGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("subscription_type")] public string SubscriptionType { get; set; } = string.Empty;
    [JsonPropertyName("rate_multiplier")] public double RateMultiplier { get; set; }
    [JsonPropertyName("user_rate_multiplier")] public double? UserRateMultiplier { get; set; }
    [JsonPropertyName("peak_rate_enabled")] public bool PeakRateEnabled { get; set; }
    [JsonPropertyName("peak_start")] public string PeakStart { get; set; } = string.Empty;
    [JsonPropertyName("peak_end")] public string PeakEnd { get; set; } = string.Empty;
    [JsonPropertyName("peak_rate_multiplier")] public double PeakRateMultiplier { get; set; }
    [JsonPropertyName("is_exclusive")] public bool IsExclusive { get; set; }
    [JsonPropertyName("image_rate_independent")] public bool ImageRateIndependent { get; set; }
    [JsonPropertyName("image_rate_multiplier")] public double ImageRateMultiplier { get; set; } = 1;
    public List<ModelPlazaModel> Models { get; set; } = [];
}

public sealed class ModelPlazaModel
{
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public UserSupportedModelPricingDto? Pricing { get; set; }
    [JsonPropertyName("official_pricing")] public ModelPlazaOfficialPricingDto? OfficialPricing { get; set; }
}

public sealed class ModelPlazaOfficialPricingDto
{
    [JsonPropertyName("input_price")] public double? InputPrice { get; set; }
    [JsonPropertyName("output_price")] public double? OutputPrice { get; set; }
    [JsonPropertyName("cache_write_price")] public double? CacheWritePrice { get; set; }
    [JsonPropertyName("cache_write_1h_price")] public double? CacheWrite1hPrice { get; set; }
    [JsonPropertyName("cache_read_price")] public double? CacheReadPrice { get; set; }
}

public sealed class PublicSettingsDto
{
    [JsonPropertyName("site_name")] public string SiteName { get; set; } = "ParaGateway";
    [JsonPropertyName("site_logo")] public string SiteLogo { get; set; } = string.Empty;
    [JsonPropertyName("site_subtitle")] public string SiteSubtitle { get; set; } = string.Empty;
    [JsonPropertyName("api_base_url")] public string ApiBaseUrl { get; set; } = string.Empty;
    [JsonPropertyName("contact_info")] public string ContactInfo { get; set; } = string.Empty;
    [JsonPropertyName("registration_enabled")] public bool RegistrationEnabled { get; set; } = true;
    [JsonPropertyName("email_verify_enabled")] public bool EmailVerifyEnabled { get; set; }
    [JsonPropertyName("password_reset_enabled")] public bool PasswordResetEnabled { get; set; } = true;
    [JsonPropertyName("turnstile_enabled")] public bool TurnstileEnabled { get; set; }
    [JsonPropertyName("turnstile_site_key")] public string TurnstileSiteKey { get; set; } = string.Empty;
    [JsonPropertyName("tencent_captcha_enabled")] public bool TencentCaptchaEnabled { get; set; }
    [JsonPropertyName("tencent_captcha_app_id")] public string TencentCaptchaAppId { get; set; } = string.Empty;
    [JsonPropertyName("tencent_captcha_region")] public string TencentCaptchaRegion { get; set; } = "cn";
    [JsonPropertyName("aliyun_captcha_enabled")] public bool AliyunCaptchaEnabled { get; set; }
    [JsonPropertyName("aliyun_captcha_scene_id")] public string AliyunCaptchaSceneId { get; set; } = string.Empty;
    [JsonPropertyName("aliyun_captcha_prefix")] public string AliyunCaptchaPrefix { get; set; } = string.Empty;
    [JsonPropertyName("aliyun_captcha_region")] public string AliyunCaptchaRegion { get; set; } = "cn";
    [JsonPropertyName("custom_menu_items")] public List<CustomMenuItemDto> CustomMenuItems { get; set; } = [];
    [JsonPropertyName("login_agreement_documents")] public List<LegalDocumentDto> LoginAgreementDocuments { get; set; } = [];
    [JsonPropertyName("login_agreement_enabled")] public bool LoginAgreementEnabled { get; set; }
    [JsonPropertyName("model_plaza_enabled")] public bool ModelPlazaEnabled { get; set; }
    [JsonPropertyName("model_plaza_require_auth")] public bool ModelPlazaRequireAuth { get; set; }
    [JsonPropertyName("hide_ccs_import_button")] public bool HideCcsImportButton { get; set; }
    [JsonPropertyName("table_default_page_size")] public int TableDefaultPageSize { get; set; } = 20;
    [JsonPropertyName("table_page_size_options")] public List<int> TablePageSizeOptions { get; set; } = [10, 20, 50, 100];
    [JsonPropertyName("custom_endpoints")] public List<CustomEndpointDto> CustomEndpoints { get; set; } = [];
    [JsonPropertyName("linuxdo_oauth_enabled")] public bool LinuxDoOAuthEnabled { get; set; }
    [JsonPropertyName("dingtalk_oauth_enabled")] public bool DingTalkOAuthEnabled { get; set; }
    [JsonPropertyName("wechat_oauth_enabled")] public bool WeChatOAuthEnabled { get; set; }
    [JsonPropertyName("wechat_oauth_open_enabled")] public bool? WeChatOAuthOpenEnabled { get; set; }
    [JsonPropertyName("wechat_oauth_mp_enabled")] public bool? WeChatOAuthMpEnabled { get; set; }
    [JsonPropertyName("wechat_oauth_mobile_enabled")] public bool? WeChatOAuthMobileEnabled { get; set; }
    [JsonPropertyName("oidc_oauth_enabled")] public bool OidcOAuthEnabled { get; set; }
    [JsonPropertyName("oidc_oauth_provider_name")] public string OidcOAuthProviderName { get; set; } = "OIDC";
    [JsonPropertyName("github_oauth_enabled")] public bool GitHubOAuthEnabled { get; set; }
    [JsonPropertyName("google_oauth_enabled")] public bool GoogleOAuthEnabled { get; set; }
    [JsonPropertyName("backend_mode_enabled")] public bool BackendModeEnabled { get; set; }
    [JsonPropertyName("totp_enabled")] public bool TotpEnabled { get; set; }
    [JsonPropertyName("passkey_enabled")] public bool PasskeyEnabled { get; set; }
    [JsonPropertyName("balance_low_notify_enabled")] public bool BalanceLowNotifyEnabled { get; set; }
    [JsonPropertyName("balance_low_notify_threshold")] public double BalanceLowNotifyThreshold { get; set; }
    [JsonPropertyName("balance_low_notify_recharge_url")] public string BalanceLowNotifyRechargeUrl { get; set; } = string.Empty;
    [JsonPropertyName("server_utc_offset")] public string ServerUtcOffset { get; set; } = string.Empty;
    [JsonPropertyName("channel_monitor_enabled")] public bool ChannelMonitorEnabled { get; set; }
    [JsonPropertyName("channel_monitor_mode")] public string ChannelMonitorMode { get; set; } = "v1";
    [JsonPropertyName("channel_monitor_default_interval_seconds")] public int ChannelMonitorDefaultIntervalSeconds { get; set; } = 60;
    [JsonPropertyName("channel_monitor_hide_throughput")] public bool ChannelMonitorHideThroughput { get; set; }
    [JsonPropertyName("channel_monitor_show_quota")] public bool ChannelMonitorShowQuota { get; set; }
    [JsonPropertyName("available_channels_enabled")] public bool AvailableChannelsEnabled { get; set; }
    [JsonPropertyName("allow_user_view_error_requests")] public bool AllowUserViewErrorRequests { get; set; }
}

public sealed class CustomMenuItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    [JsonPropertyName("page_slug")] public string? PageSlug { get; set; }
    [JsonPropertyName("icon_svg")] public string IconSvg { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Visibility { get; set; } = "user";
    [JsonPropertyName("sort_order")] public int SortOrder { get; set; }
}

public sealed class LegalDocumentDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    [JsonPropertyName("content_md")] public string ContentMarkdown { get; set; } = string.Empty;
}

public sealed class ApiKeyDailyUsageResponse
{
    public List<ApiKeyDailyUsagePoint> Items { get; set; } = [];
    public int Days { get; set; }
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
}

public sealed class BatchImageSubmitRequest
{
    [Required(ErrorMessage = "请输入模型")]
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("task_name")] public string TaskName { get; set; } = string.Empty;
    [JsonPropertyName("parent_batch_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentBatchId { get; set; }
    public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("response_mime_type")] public string ResponseMimeType { get; set; } = "image/png";
    [JsonPropertyName("aspect_ratio")] public string AspectRatio { get; set; } = string.Empty;
    [JsonPropertyName("image_size")] public string ImageSize { get; set; } = "1K";
    public List<BatchImageSubmitItem> Items { get; set; } = [];
}

public sealed class BatchImageSubmitItem
{
    [Required(ErrorMessage = "请输入任务标识")]
    [JsonPropertyName("custom_id")] public string CustomId { get; set; } = "item-1";
    [Required(ErrorMessage = "请输入图片提示词")]
    public string Prompt { get; set; } = string.Empty;
    [JsonPropertyName("output_count")] public int OutputCount { get; set; } = 1;
    [JsonPropertyName("reference_images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BatchImageReferenceImageDto>? ReferenceImages { get; set; }
}

public sealed class BatchImageReferenceImageDto
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    [JsonPropertyName("mime_type")] public string MimeType { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Data { get; set; }
    [JsonPropertyName("file_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileUri { get; set; }
}

public sealed class BatchImageListResponse
{
    public string Object { get; set; } = string.Empty;
    public List<BatchImageJobDto> Data { get; set; } = [];
    [JsonPropertyName("has_more")] public bool HasMore { get; set; }
}

public sealed class BatchImageModelsResponse
{
    public string Object { get; set; } = string.Empty;
    public List<BatchImageModelDto> Data { get; set; } = [];
}

public sealed class BatchImageModelDto
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}

public sealed class BatchImageJobDto
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    [JsonPropertyName("task_name")] public string TaskName { get; set; } = string.Empty;
    [JsonPropertyName("parent_batch_id")] public string? ParentBatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("item_count")] public int ItemCount { get; set; }
    [JsonPropertyName("success_count")] public int SuccessCount { get; set; }
    [JsonPropertyName("fail_count")] public int FailCount { get; set; }
    [JsonPropertyName("estimated_cost")] public double EstimatedCost { get; set; }
    [JsonPropertyName("hold_amount")] public double HoldAmount { get; set; }
    [JsonPropertyName("actual_cost")] public double? ActualCost { get; set; }
    [JsonPropertyName("created_at")] public long CreatedAtUnix { get; set; }
    [JsonPropertyName("submitted_at")] public long? SubmittedAtUnix { get; set; }
    [JsonPropertyName("settled_at")] public long? SettledAtUnix { get; set; }
    [JsonPropertyName("downloaded_at")] public long? DownloadedAtUnix { get; set; }
    [JsonPropertyName("output_deleted_at")] public long? OutputDeletedAtUnix { get; set; }
    public DateTimeOffset CreatedAt => UnixSeconds(CreatedAtUnix);
    public DateTimeOffset? SubmittedAt => SubmittedAtUnix.HasValue ? UnixSeconds(SubmittedAtUnix.Value) : null;
    public DateTimeOffset? SettledAt => SettledAtUnix.HasValue ? UnixSeconds(SettledAtUnix.Value) : null;
    public DateTimeOffset? DownloadedAt => DownloadedAtUnix.HasValue ? UnixSeconds(DownloadedAtUnix.Value) : null;
    public DateTimeOffset? OutputDeletedAt => OutputDeletedAtUnix.HasValue ? UnixSeconds(OutputDeletedAtUnix.Value) : null;
    private static DateTimeOffset UnixSeconds(long value) => value > 10_000_000_000
        ? DateTimeOffset.FromUnixTimeMilliseconds(value)
        : DateTimeOffset.FromUnixTimeSeconds(value);
}

public sealed class BatchImageItemsResponse
{
    public string Object { get; set; } = string.Empty;
    public List<BatchImageItemDto> Data { get; set; } = [];
    [JsonPropertyName("has_more")] public bool HasMore { get; set; }
}

public sealed class BatchImageItemDto
{
    [JsonPropertyName("batch_id")] public string? BatchId { get; set; }
    [JsonPropertyName("source_task_name")] public string? SourceTaskName { get; set; }
    [JsonPropertyName("custom_id")] public string CustomId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("prompt_preview")] public string? PromptPreview { get; set; }
    [JsonPropertyName("mime_type")] public string? MimeType { get; set; }
    [JsonPropertyName("file_extension")] public string? FileExtension { get; set; }
    [JsonPropertyName("image_count")] public int ImageCount { get; set; }
    public BatchImageErrorDto? Error { get; set; }
}

public sealed class BatchImageErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class ApiKeyInput
{
    [Required(ErrorMessage = "请选择所属用户")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密钥名称")]
    [MaxLength(80, ErrorMessage = "名称不能超过 80 个字符")]
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>官方 Go 后端的管理员账号 DTO。凭据字段仅保留脱敏后的状态信息。</summary>
public sealed class GoAccount
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, JsonElement>? Credentials { get; set; }
    [JsonPropertyName("credentials_status")] public Dictionary<string, bool>? CredentialsStatus { get; set; }
    public Dictionary<string, JsonElement>? Extra { get; set; }
    [JsonPropertyName("ollama_cloud_usage")] public OllamaCloudUsageStateDto? OllamaCloudUsage { get; set; }
    [JsonPropertyName("copilot_billing_usage")] public CopilotBillingUsageDto? CopilotBillingUsage { get; set; }
    [JsonPropertyName("proxy_id")] public long? ProxyId { get; set; }
    public int Concurrency { get; set; }
    [JsonPropertyName("load_factor")] public int? LoadFactor { get; set; }
    public int Priority { get; set; }
    [JsonPropertyName("rate_multiplier")] public double RateMultiplier { get; set; }
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("error_message")] public string ErrorMessage { get; set; } = string.Empty;
    public bool Schedulable { get; set; } = true;
    [JsonPropertyName("last_used_at")] public DateTimeOffset? LastUsedAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
    [JsonPropertyName("expires_at")] public long? ExpiresAt { get; set; }
    [JsonPropertyName("auto_pause_on_expired")] public bool AutoPauseOnExpired { get; set; }
    [JsonPropertyName("current_concurrency")] public int CurrentConcurrency { get; set; }
    [JsonPropertyName("group_ids")] public List<long> GroupIds { get; set; } = [];
    public List<GoGroup> Groups { get; set; } = [];
    public ProxyDto? Proxy { get; set; }
    [JsonPropertyName("rate_limited_at")] public DateTimeOffset? RateLimitedAt { get; set; }
    [JsonPropertyName("rate_limit_reset_at")] public DateTimeOffset? RateLimitResetAt { get; set; }
    [JsonPropertyName("overload_until")] public DateTimeOffset? OverloadUntil { get; set; }
    [JsonPropertyName("temp_unschedulable_until")] public DateTimeOffset? TempUnschedulableUntil { get; set; }
    [JsonPropertyName("temp_unschedulable_reason")] public string TempUnschedulableReason { get; set; } = string.Empty;
    [JsonPropertyName("quota_limit")] public double? QuotaLimit { get; set; }
    [JsonPropertyName("quota_used")] public double? QuotaUsed { get; set; }
    [JsonPropertyName("quota_daily_limit")] public double? QuotaDailyLimit { get; set; }
    [JsonPropertyName("quota_daily_used")] public double? QuotaDailyUsed { get; set; }
    [JsonPropertyName("quota_weekly_limit")] public double? QuotaWeeklyLimit { get; set; }
    [JsonPropertyName("quota_weekly_used")] public double? QuotaWeeklyUsed { get; set; }
    [JsonPropertyName("scheduler_score")] public AccountSchedulerScoreDto? SchedulerScore { get; set; }
    [JsonPropertyName("scheduler_scores")] public List<AccountSchedulerGroupScoreDto> SchedulerScores { get; set; } = [];
    [JsonPropertyName("current_window_cost")] public double? CurrentWindowCost { get; set; }
    [JsonPropertyName("active_sessions")] public int? ActiveSessions { get; set; }
    [JsonPropertyName("current_rpm")] public int? CurrentRpm { get; set; }
    [JsonPropertyName("parent_account_id")] public long? ParentAccountId { get; set; }
    [JsonPropertyName("quota_dimension")] public string QuotaDimension { get; set; } = string.Empty;
    [JsonPropertyName("parent_email")] public string ParentEmail { get; set; } = string.Empty;
    [JsonPropertyName("parent_plan_type")] public string ParentPlanType { get; set; } = string.Empty;
    [JsonPropertyName("parent_subscription_expires_at")] public string ParentSubscriptionExpiresAt { get; set; } = string.Empty;
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class AccountSchedulerScoreDto
{
    [JsonPropertyName("base_score")] public double BaseScore { get; set; }
    [JsonPropertyName("sticky_score")] public double StickyScore { get; set; }
    [JsonPropertyName("sticky_score_infinity")] public bool StickyScoreInfinity { get; set; }
    [JsonPropertyName("sticky_weighted_enabled")] public bool StickyWeightedEnabled { get; set; }
}

public sealed class AccountSchedulerGroupScoreDto : AccountSchedulerScoreDto
{
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    [JsonPropertyName("group_priority")] public int? GroupPriority { get; set; }
}

public sealed class AccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool Schedulable { get; set; }
    public int Concurrency { get; set; }
    public int? LoadFactor { get; set; }
    public int CurrentConcurrency { get; set; }
    public int Priority { get; set; }
    public double RateMultiplier { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long? ExpiresAt { get; set; }
    public bool AutoPauseOnExpired { get; set; }
    public long? ProxyId { get; set; }
    public Dictionary<string, JsonElement>? Credentials { get; set; }
    public Dictionary<string, bool>? CredentialsStatus { get; set; }
    public Dictionary<string, JsonElement>? Extra { get; set; }
    public OllamaCloudUsageStateDto? OllamaCloudUsage { get; set; }
    public CopilotBillingUsageDto? CopilotBillingUsage { get; set; }
    public List<long> GroupIds { get; set; } = [];
    public List<GoGroup> Groups { get; set; } = [];
    public ProxyDto? Proxy { get; set; }
    public DateTimeOffset? RateLimitedAt { get; set; }
    public DateTimeOffset? RateLimitResetAt { get; set; }
    public DateTimeOffset? OverloadUntil { get; set; }
    public DateTimeOffset? TempUnschedulableUntil { get; set; }
    public string TempUnschedulableReason { get; set; } = string.Empty;
    public double? QuotaLimit { get; set; }
    public double? QuotaUsed { get; set; }
    public double? QuotaDailyLimit { get; set; }
    public double? QuotaDailyUsed { get; set; }
    public double? QuotaWeeklyLimit { get; set; }
    public double? QuotaWeeklyUsed { get; set; }
    public AccountSchedulerScoreDto? SchedulerScore { get; set; }
    public List<AccountSchedulerGroupScoreDto> SchedulerScores { get; set; } = [];
    public double? CurrentWindowCost { get; set; }
    public int? ActiveSessions { get; set; }
    public int? CurrentRpm { get; set; }
    public long? ParentAccountId { get; set; }
    public string QuotaDimension { get; set; } = string.Empty;
    public string ParentEmail { get; set; } = string.Empty;
    public string ParentPlanType { get; set; } = string.Empty;
    public string ParentSubscriptionExpiresAt { get; set; } = string.Empty;

    public static AccountDto From(GoAccount account)
    {
        var hasApiKey = account.CredentialsStatus?.Any(x => x.Value && x.Key.Contains("key", StringComparison.OrdinalIgnoreCase)) == true;
        return new AccountDto
        {
            Id = account.Id.ToString(), Name = account.Name, Platform = account.Platform,
            Notes = account.Notes,
            Type = account.Type, Status = account.Status, Schedulable = account.Schedulable,
            Concurrency = account.Concurrency, LoadFactor = account.LoadFactor, CurrentConcurrency = account.CurrentConcurrency,
            Priority = account.Priority,
            RateMultiplier = account.RateMultiplier,
            ErrorMessage = account.ErrorMessage, HasApiKey = hasApiKey,
            LastUsedAt = account.LastUsedAt, CreatedAt = account.CreatedAt, ExpiresAt = account.ExpiresAt,
            AutoPauseOnExpired = account.AutoPauseOnExpired, ProxyId = account.ProxyId,
            Credentials = account.Credentials, CredentialsStatus = account.CredentialsStatus, Extra = account.Extra,
            OllamaCloudUsage = account.OllamaCloudUsage, CopilotBillingUsage = account.CopilotBillingUsage,
            GroupIds = account.GroupIds is { Count: > 0 } ? account.GroupIds : (account.Groups ?? []).Select(x => x.Id).ToList(),
            Groups = account.Groups ?? [], Proxy = account.Proxy,
            RateLimitedAt = account.RateLimitedAt, RateLimitResetAt = account.RateLimitResetAt,
            OverloadUntil = account.OverloadUntil, TempUnschedulableUntil = account.TempUnschedulableUntil,
            TempUnschedulableReason = account.TempUnschedulableReason,
            QuotaLimit = account.QuotaLimit, QuotaUsed = account.QuotaUsed,
            QuotaDailyLimit = account.QuotaDailyLimit, QuotaDailyUsed = account.QuotaDailyUsed,
            QuotaWeeklyLimit = account.QuotaWeeklyLimit, QuotaWeeklyUsed = account.QuotaWeeklyUsed,
            SchedulerScore = account.SchedulerScore, SchedulerScores = account.SchedulerScores ?? [],
            CurrentWindowCost = account.CurrentWindowCost, ActiveSessions = account.ActiveSessions,
            CurrentRpm = account.CurrentRpm, ParentAccountId = account.ParentAccountId,
            QuotaDimension = account.QuotaDimension, ParentEmail = account.ParentEmail,
            ParentPlanType = account.ParentPlanType,
            ParentSubscriptionExpiresAt = account.ParentSubscriptionExpiresAt
        };
    }
}

/// <summary>GitHub Copilot 本月 AI Credits 官方账单快照。</summary>
public sealed class CopilotBillingUsageDto
{
    public string Username { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    [JsonPropertyName("items_count")] public int ItemsCount { get; set; }
    [JsonPropertyName("gross_quantity")] public double GrossQuantity { get; set; }
    [JsonPropertyName("gross_amount")] public double GrossAmount { get; set; }
    [JsonPropertyName("net_quantity")] public double NetQuantity { get; set; }
    [JsonPropertyName("net_amount")] public double NetAmount { get; set; }
    [JsonPropertyName("fetched_at")] public string FetchedAt { get; set; } = string.Empty;
}

public sealed class AccountListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string Search { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PrivacyMode { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string SortBy { get; set; } = "name";
    public string SortOrder { get; set; } = "asc";
    public bool IncludeSchedulerScore { get; set; }
}

public sealed class AccountBatchResultDto
{
    public int Total { get; set; }
    [JsonPropertyName("success")] public int SuccessCount { get; set; }
    [JsonPropertyName("failed")] public int FailedCount { get; set; }
    [JsonPropertyName("success_ids")] public List<long> SuccessIds { get; set; } = [];
    [JsonPropertyName("failed_ids")] public List<long> FailedIds { get; set; } = [];
    public List<JsonElement> Errors { get; set; } = [];
    public List<AccountBatchItemResultDto> Results { get; set; } = [];
    [JsonPropertyName("long_context_inherited_count")] public int LongContextInheritedCount { get; set; }
}

public sealed class AccountBatchItemResultDto
{
    [JsonPropertyName("account_id")] public long AccountId { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
}

public sealed class CNQuotaTierDto
{
    public string Window { get; set; } = string.Empty;
    [JsonPropertyName("used_percent")] public double UsedPercent { get; set; }
    [JsonPropertyName("reset_at")] public string? ResetAt { get; set; }
}

public sealed class CNProviderQuotaProbeResultDto
{
    public string Provider { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool Success { get; set; }
    [JsonPropertyName("credential_valid")] public bool CredentialValid { get; set; }
    public List<CNQuotaTierDto> Tiers { get; set; } = [];
    [JsonPropertyName("plan_level")] public string PlanLevel { get; set; } = string.Empty;
    [JsonPropertyName("status_code")] public int? StatusCode { get; set; }
    [JsonPropertyName("fetched_at")] public long FetchedAt { get; set; }
    public bool Persisted { get; set; }
    public string Error { get; set; } = string.Empty;
}

public sealed class CNProviderBalanceEntryDto
{
    public string Currency { get; set; } = string.Empty;
    public double Balance { get; set; }
}

public sealed class CNProviderBalanceResultDto
{
    public string Provider { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<CNProviderBalanceEntryDto> Balances { get; set; } = [];
    public bool Available { get; set; }
    [JsonPropertyName("status_code")] public int? StatusCode { get; set; }
    [JsonPropertyName("fetched_at")] public long FetchedAt { get; set; }
    public bool Persisted { get; set; }
    public string Error { get; set; } = string.Empty;
}

public sealed class OllamaCloudUsageWindowDto
{
    [JsonPropertyName("used_percent")] public double UsedPercent { get; set; }
    [JsonPropertyName("reset_at")] public DateTimeOffset? ResetAt { get; set; }
    [JsonPropertyName("reset_text")] public string ResetText { get; set; } = string.Empty;
}

public sealed class OllamaCloudUsageModelDto
{
    public string Model { get; set; } = string.Empty;
    public string Window { get; set; } = string.Empty;
    public long Requests { get; set; }
}

public sealed class OllamaCloudUsageDataDto
{
    public string Plan { get; set; } = string.Empty;
    [JsonPropertyName("five_hour")] public OllamaCloudUsageWindowDto? FiveHour { get; set; }
    [JsonPropertyName("seven_day")] public OllamaCloudUsageWindowDto? SevenDay { get; set; }
    public string Balance { get; set; } = string.Empty;
    public List<OllamaCloudUsageModelDto> Models { get; set; } = [];
}

public sealed class OllamaCloudUsageSnapshotDto
{
    public string Status { get; set; } = string.Empty;
    public OllamaCloudUsageDataDto? Data { get; set; }
    [JsonPropertyName("fetched_at")] public DateTimeOffset? FetchedAt { get; set; }
    [JsonPropertyName("last_attempt_at")] public DateTimeOffset LastAttemptAt { get; set; }
    [JsonPropertyName("next_refresh_at")] public DateTimeOffset NextRefreshAt { get; set; }
    [JsonPropertyName("failure_count")] public int FailureCount { get; set; }
    [JsonPropertyName("http_status")] public int? HttpStatus { get; set; }
    [JsonPropertyName("last_error")] public string LastError { get; set; } = string.Empty;
}

public sealed class OllamaCloudUsageStateDto
{
    [JsonPropertyName("account_id")] public long AccountId { get; set; }
    public bool Eligible { get; set; }
    public bool Configured { get; set; }
    [JsonPropertyName("auto_refresh_enabled")] public bool AutoRefreshEnabled { get; set; }
    [JsonPropertyName("encryption_key_configured")] public bool EncryptionKeyConfigured { get; set; }
    public OllamaCloudUsageSnapshotDto? Snapshot { get; set; }
}

public sealed class AccountTodayStatsDto
{
    public long Requests { get; set; }
    public long Tokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("standard_cost")] public double StandardCost { get; set; }
    [JsonPropertyName("user_cost")] public double UserCost { get; set; }
}

public sealed class AccountTodayStatsBatchDto
{
    public Dictionary<string, AccountTodayStatsDto> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AccountUsageWindowStatsDto
{
    public long Requests { get; set; }
    public long Tokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("standard_cost")] public double StandardCost { get; set; }
    [JsonPropertyName("user_cost")] public double UserCost { get; set; }
}

public sealed class AccountUsageProgressDto
{
    public double Utilization { get; set; }
    [JsonPropertyName("resets_at")] public DateTimeOffset? ResetsAt { get; set; }
    [JsonPropertyName("remaining_seconds")] public int RemainingSeconds { get; set; }
    [JsonPropertyName("window_stats")] public AccountUsageWindowStatsDto? WindowStats { get; set; }
    [JsonPropertyName("used_requests")] public long UsedRequests { get; set; }
    [JsonPropertyName("limit_requests")] public long LimitRequests { get; set; }
}

public sealed class AntigravityModelQuotaDto
{
    public int Utilization { get; set; }
    [JsonPropertyName("reset_time")] public string ResetTime { get; set; } = string.Empty;
}

public sealed class AccountUsageInfoDto
{
    public string Source { get; set; } = string.Empty;
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("five_hour")] public AccountUsageProgressDto? FiveHour { get; set; }
    [JsonPropertyName("seven_day")] public AccountUsageProgressDto? SevenDay { get; set; }
    [JsonPropertyName("seven_day_sonnet")] public AccountUsageProgressDto? SevenDaySonnet { get; set; }
    [JsonPropertyName("seven_day_fable")] public AccountUsageProgressDto? SevenDayFable { get; set; }
    [JsonPropertyName("thirty_day")] public AccountUsageProgressDto? ThirtyDay { get; set; }
    [JsonPropertyName("gemini_shared_daily")] public AccountUsageProgressDto? GeminiSharedDaily { get; set; }
    [JsonPropertyName("gemini_pro_daily")] public AccountUsageProgressDto? GeminiProDaily { get; set; }
    [JsonPropertyName("gemini_flash_daily")] public AccountUsageProgressDto? GeminiFlashDaily { get; set; }
    [JsonPropertyName("gemini_shared_minute")] public AccountUsageProgressDto? GeminiSharedMinute { get; set; }
    [JsonPropertyName("gemini_pro_minute")] public AccountUsageProgressDto? GeminiProMinute { get; set; }
    [JsonPropertyName("gemini_flash_minute")] public AccountUsageProgressDto? GeminiFlashMinute { get; set; }
    [JsonPropertyName("antigravity_quota")] public Dictionary<string, AntigravityModelQuotaDto> AntigravityQuota { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Error { get; set; } = string.Empty;
}

public sealed class AccountUsageBatchResponseDto
{
    public Dictionary<string, AccountUsageInfoDto> Usage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Errors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class OpenAIQuotaResetCreditDto
{
    [JsonPropertyName("expires_at")] public string ExpiresAt { get; set; } = string.Empty;
}

public sealed class OpenAIQuotaResetCreditsDto
{
    [JsonPropertyName("available_count")] public int AvailableCount { get; set; }
    public List<OpenAIQuotaResetCreditDto> Credits { get; set; } = [];
}

public sealed class OpenAIQuotaUsageDto
{
    [JsonPropertyName("rate_limit_reset_credits")] public OpenAIQuotaResetCreditsDto? RateLimitResetCredits { get; set; }
    [JsonPropertyName("fetched_at")] public long FetchedAt { get; set; }
    [JsonPropertyName("cache_persisted")] public bool CachePersisted { get; set; }
}

public sealed class OpenAIQuotaResetResultDto
{
    public string Code { get; set; } = string.Empty;
    [JsonPropertyName("windows_reset")] public int WindowsReset { get; set; }
    public OpenAIQuotaUsageDto? Quota { get; set; }
    [JsonPropertyName("cache_refreshed")] public bool CacheRefreshed { get; set; }
    [JsonPropertyName("account_state_recovered")] public bool AccountStateRecovered { get; set; }
    [JsonPropertyName("warning_code")] public string WarningCode { get; set; } = string.Empty;
}

/// <summary>
/// Worker 风格的独立上游账号。它与 Go 官方 AccountDto/accounts 表没有映射关系。
/// </summary>
public sealed class UpstreamAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("provider_type")] public string ProviderType { get; set; } = "openai";
    [JsonPropertyName("base_url")] public string BaseUrl { get; set; } = string.Empty;
    [JsonPropertyName("auth_type")] public string AuthType { get; set; } = "api_key";
    [JsonPropertyName("masked_credential")] public string MaskedCredential { get; set; } = string.Empty;
    [JsonPropertyName("oauth_profile")] public string? OAuthProfile { get; set; }
    [JsonPropertyName("oauth_account_id")] public string? OAuthAccountId { get; set; }
    [JsonPropertyName("oauth_email")] public string? OAuthEmail { get; set; }
    [JsonPropertyName("oauth_expires_at")] public DateTimeOffset? OAuthExpiresAt { get; set; }
    [JsonPropertyName("wif_subject_token_url")] public string? WifSubjectTokenUrl { get; set; }
    [JsonPropertyName("wif_client_id")] public string? WifClientId { get; set; }
    [JsonPropertyName("wif_client_auth_method")] public string? WifClientAuthMethod { get; set; }
    [JsonPropertyName("wif_audience")] public string? WifAudience { get; set; }
    [JsonPropertyName("wif_scope")] public string? WifScope { get; set; }
    [JsonPropertyName("wif_identity_provider_id")] public string? WifIdentityProviderId { get; set; }
    [JsonPropertyName("wif_service_account_id")] public string? WifServiceAccountId { get; set; }
    [JsonPropertyName("wif_federation_rule_id")] public string? WifFederationRuleId { get; set; }
    [JsonPropertyName("wif_organization_id")] public string? WifOrganizationId { get; set; }
    [JsonPropertyName("wif_workspace_id")] public string? WifWorkspaceId { get; set; }
    [JsonPropertyName("is_active")] public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 100;
    public int Weight { get; set; } = 100;
    [JsonPropertyName("max_concurrency")] public int MaxConcurrency { get; set; } = 8;
    [JsonPropertyName("rpm_limit")] public int RpmLimit { get; set; } = 120;
    [JsonPropertyName("circuit_breaker_threshold")] public int CircuitBreakerThreshold { get; set; } = 3;
    [JsonPropertyName("circuit_breaker_cooldown_seconds")] public int CircuitBreakerCooldownSeconds { get; set; } = 60;
    [JsonPropertyName("quota_status")] public string QuotaStatus { get; set; } = "unknown";
    [JsonPropertyName("quota_utilization")] public double? QuotaUtilization { get; set; }
    [JsonPropertyName("quota_resets_at")] public DateTimeOffset? QuotaResetsAt { get; set; }
    [JsonPropertyName("quota_checked_at")] public DateTimeOffset? QuotaCheckedAt { get; set; }
    [JsonPropertyName("usage_windows")] public UpstreamUsageWindowsDto UsageWindows { get; set; } = new();
    [JsonPropertyName("cooldown_until")] public DateTimeOffset? CooldownUntil { get; set; }
    [JsonPropertyName("cooldown_reason")] public string? CooldownReason { get; set; }
    [JsonPropertyName("last_upstream_status")] public int? LastUpstreamStatus { get; set; }
    [JsonPropertyName("last_success_at")] public DateTimeOffset? LastSuccessAt { get; set; }
    [JsonPropertyName("last_failure_at")] public DateTimeOffset? LastFailureAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class UpstreamUsageWindowsDto
{
    [JsonPropertyName("five_hour")] public UpstreamUsageWindowDto? FiveHour { get; set; }
    [JsonPropertyName("seven_day")] public UpstreamUsageWindowDto? SevenDay { get; set; }
    [JsonPropertyName("seven_day_sonnet")] public UpstreamUsageWindowDto? SevenDaySonnet { get; set; }
}

public sealed class UpstreamUsageWindowDto
{
    public double Utilization { get; set; }
    [JsonPropertyName("resets_at")] public DateTimeOffset? ResetsAt { get; set; }
}

public sealed class UpstreamAccountInput : IValidatableObject
{
    [Required(ErrorMessage = "请输入账号名称"), MaxLength(80, ErrorMessage = "名称不能超过 80 个字符")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("provider_type")] public string ProviderType { get; set; } = "openai";
    [Required(ErrorMessage = "请输入 API 地址"), Url(ErrorMessage = "API 地址格式不正确")]
    [JsonPropertyName("base_url")] public string BaseUrl { get; set; } = "https://api.openai.com";
    [JsonPropertyName("auth_type")] public string AuthType { get; set; } = "api_key";
    [JsonPropertyName("api_key"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ApiKey { get; set; }
    [JsonPropertyName("wif_client_secret"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifClientSecret { get; set; }
    [JsonPropertyName("wif_subject_token_url"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifSubjectTokenUrl { get; set; }
    [JsonPropertyName("wif_client_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifClientId { get; set; }
    [JsonPropertyName("wif_client_auth_method")] public string WifClientAuthMethod { get; set; } = "client_secret_basic";
    [JsonPropertyName("wif_audience"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifAudience { get; set; }
    [JsonPropertyName("wif_scope"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifScope { get; set; }
    [JsonPropertyName("wif_identity_provider_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifIdentityProviderId { get; set; }
    [JsonPropertyName("wif_service_account_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifServiceAccountId { get; set; }
    [JsonPropertyName("wif_federation_rule_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifFederationRuleId { get; set; }
    [JsonPropertyName("wif_organization_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifOrganizationId { get; set; }
    [JsonPropertyName("wif_workspace_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WifWorkspaceId { get; set; }
    [JsonPropertyName("is_active")] public bool IsActive { get; set; } = true;
    [Range(1, 9999, ErrorMessage = "优先级需要在 1 到 9999 之间")] public int Priority { get; set; } = 100;
    [Range(1, 10000, ErrorMessage = "权重需要在 1 到 10000 之间")] public int Weight { get; set; } = 100;
    [Range(1, 10000, ErrorMessage = "最大并发需要在 1 到 10000 之间")]
    [JsonPropertyName("max_concurrency")] public int MaxConcurrency { get; set; } = 8;
    [Range(1, 1_000_000, ErrorMessage = "RPM 限制需要在 1 到 1000000 之间")]
    [JsonPropertyName("rpm_limit")] public int RpmLimit { get; set; } = 120;
    [Range(1, 1000, ErrorMessage = "熔断阈值需要在 1 到 1000 之间")]
    [JsonPropertyName("circuit_breaker_threshold")] public int CircuitBreakerThreshold { get; set; } = 3;
    [Range(1, 86400, ErrorMessage = "冷却时间需要在 1 到 86400 秒之间")]
    [JsonPropertyName("circuit_breaker_cooldown_seconds")] public int CircuitBreakerCooldownSeconds { get; set; } = 60;

    [JsonIgnore] public bool IsEditing { get; set; }
    [JsonIgnore] public string OriginalProviderType { get; set; } = "openai";
    [JsonIgnore] public string OriginalBaseUrl { get; set; } = string.Empty;
    [JsonIgnore] public string OriginalAuthType { get; set; } = "api_key";
    [JsonIgnore] public string OriginalWifSubjectTokenUrl { get; set; } = string.Empty;
    [JsonIgnore] public string OriginalWifClientId { get; set; } = string.Empty;
    [JsonIgnore] public string OriginalWifClientAuthMethod { get; set; } = "client_secret_basic";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProviderType is not ("openai" or "claude"))
            yield return new ValidationResult("请选择有效的上游类型", [nameof(ProviderType)]);
        if (AuthType is not ("api_key" or "wif"))
            yield return new ValidationResult("请选择有效的认证方式", [nameof(AuthType)]);

        var credentialRequired = !IsEditing
            || !string.Equals(OriginalProviderType, ProviderType, StringComparison.Ordinal)
            || !string.Equals(OriginalAuthType, AuthType, StringComparison.Ordinal)
            || !SameOrigin(OriginalBaseUrl, BaseUrl);
        if (AuthType == "api_key")
        {
            if (credentialRequired && string.IsNullOrWhiteSpace(ApiKey))
                yield return new ValidationResult("请输入 API Key", [nameof(ApiKey)]);
            yield break;
        }

        if (!Uri.TryCreate(WifSubjectTokenUrl, UriKind.Absolute, out var tokenUri) || tokenUri.Scheme != Uri.UriSchemeHttps)
            yield return new ValidationResult("请输入有效的 HTTPS 外部 IdP Token URL", [nameof(WifSubjectTokenUrl)]);
        if (string.IsNullOrWhiteSpace(WifClientId))
            yield return new ValidationResult("请输入外部 IdP Client ID", [nameof(WifClientId)]);
        if (WifClientAuthMethod is not ("client_secret_basic" or "client_secret_post"))
            yield return new ValidationResult("请选择有效的客户端认证方式", [nameof(WifClientAuthMethod)]);
        if (string.IsNullOrWhiteSpace(WifServiceAccountId))
            yield return new ValidationResult("请输入 Service Account ID", [nameof(WifServiceAccountId)]);
        if (ProviderType == "openai" && string.IsNullOrWhiteSpace(WifIdentityProviderId))
            yield return new ValidationResult("请输入 Identity Provider ID", [nameof(WifIdentityProviderId)]);
        if (ProviderType == "claude" && string.IsNullOrWhiteSpace(WifFederationRuleId))
            yield return new ValidationResult("请输入 Federation Rule ID", [nameof(WifFederationRuleId)]);
        if (ProviderType == "claude" && string.IsNullOrWhiteSpace(WifOrganizationId))
            yield return new ValidationResult("请输入 Organization ID", [nameof(WifOrganizationId)]);

        credentialRequired = credentialRequired
            || !string.Equals(OriginalWifSubjectTokenUrl?.TrimEnd('/'), WifSubjectTokenUrl?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(OriginalWifClientId?.Trim(), WifClientId?.Trim(), StringComparison.Ordinal)
            || !string.Equals(OriginalWifClientAuthMethod, WifClientAuthMethod, StringComparison.Ordinal);
        if (credentialRequired && string.IsNullOrWhiteSpace(WifClientSecret))
            yield return new ValidationResult("请输入 WIF Client Secret", [nameof(WifClientSecret)]);
    }

    private static bool SameOrigin(string? left, string? right)
    {
        if (!Uri.TryCreate(left, UriKind.Absolute, out var leftUri) || !Uri.TryCreate(right, UriKind.Absolute, out var rightUri)) return false;
        return string.Equals(leftUri.Scheme, rightUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(leftUri.Host, rightUri.Host, StringComparison.OrdinalIgnoreCase)
            && leftUri.Port == rightUri.Port;
    }
}

public sealed class UpstreamConnectionTestResultDto
{
    public bool Success { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("latency_ms")] public long LatencyMs { get; set; }
    [JsonPropertyName("status_code")] public int? StatusCode { get; set; }
    [JsonPropertyName("model_count")] public int? ModelCount { get; set; }
}

public sealed class CrsSyncInput
{
    [Required(ErrorMessage = "请输入 CRS 服务地址")] public string BaseUrl { get; set; } = string.Empty;
    [Required(ErrorMessage = "请输入 CRS 用户名")] public string Username { get; set; } = string.Empty;
    [Required(ErrorMessage = "请输入 CRS 密码")] public string Password { get; set; } = string.Empty;
    public bool SyncProxies { get; set; } = true;
}

public sealed class AccountInput : IValidatableObject
{
    [Required(ErrorMessage = "请输入账号名称"), MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required] public string Platform { get; set; } = "openai";
    [Required] public string Type { get; set; } = "apikey";
    public string ApiKey { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string CredentialsJson { get; set; } = string.Empty;
    public string ExtraJson { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string AccountMode { get; set; } = "payg";
    public string ApiProtocol { get; set; } = "adaptive";
    [JsonIgnore] public string AdaptiveChatCompletionsBaseUrl { get; set; } = string.Empty;
    [JsonIgnore] public string AdaptiveAnthropicBaseUrl { get; set; } = string.Empty;
    [JsonIgnore] public string AdaptiveResponsesBaseUrl { get; set; } = string.Empty;
    [Range(1, 10_000, ErrorMessage = "并发上限必须在 1 到 10000 之间")] public int Concurrency { get; set; } = 8;
    [Range(0, 1_000_000, ErrorMessage = "优先级必须在 0 到 1000000 之间")] public int Priority { get; set; } = 100;
    [Range(0, 100)] public double RateMultiplier { get; set; } = 1;
    public string Notes { get; set; } = string.Empty;
    public long? ProxyId { get; set; }
    [Range(1, 10000, ErrorMessage = "调度负载因子必须在 1 到 10000 之间")]
    public int? LoadFactor { get; set; }
    public long? ExpiresAt { get; set; }
    public bool AutoPauseOnExpired { get; set; }
    [JsonIgnore] public bool Schedulable { get; set; } = true;
    // 只有 openai/anthropic/gemini/antigravity/grok 的 API Key 账号支持
    // Go 后端的 upstream billing probe；其它类型默认关闭，避免提交非法字段。
    public bool ProbeEnabled { get; set; }
    public bool RateSyncEnabled { get; set; }
    public List<long> GroupIds { get; set; } = [];
    [JsonIgnore] public string BillingUsername { get; set; } = string.Empty;
    [JsonIgnore] public string BillingPat { get; set; } = string.Empty;
    [JsonIgnore] public bool HasBillingPat { get; set; }
    [JsonIgnore, Range(0.000001, double.MaxValue, ErrorMessage = "AI Credits 月额度必须大于 0")]
    public double BillingCreditLimit { get; set; } = 20_000;
    [JsonIgnore, Range(0, double.MaxValue, ErrorMessage = "安全余量不能小于 0")]
    public double BillingSafetyMargin { get; set; } = 200;
    [JsonIgnore] public bool BillingAutoPauseDisabled { get; set; }
    [JsonIgnore] public bool IsCopilotProfile { get; set; }
    [JsonIgnore] public bool IsEditing { get; set; }
    [JsonIgnore] public string ModelRestrictionMode { get; set; } = "whitelist";
    [JsonIgnore] public List<string> AllowedModels { get; set; } = [];
    [JsonIgnore] public List<ModelMappingInput> ModelMappings { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl) && !new UrlAttribute().IsValid(BaseUrl))
        {
            yield return new ValidationResult("Base URL 格式不正确", [nameof(BaseUrl)]);
        }

        var requiresAdaptiveBaseUrls = Platform is "kimi" or "zhipu" or "deepseek"
            && string.Equals(Type, "apikey", StringComparison.OrdinalIgnoreCase)
            && string.Equals(ApiProtocol, "adaptive", StringComparison.OrdinalIgnoreCase);
        if (requiresAdaptiveBaseUrls)
        {
            if (string.IsNullOrWhiteSpace(AdaptiveChatCompletionsBaseUrl))
                yield return new ValidationResult("请输入 Chat Completions Base URL", [nameof(AdaptiveChatCompletionsBaseUrl)]);
            if (string.IsNullOrWhiteSpace(AdaptiveAnthropicBaseUrl))
                yield return new ValidationResult("请输入 Anthropic Base URL", [nameof(AdaptiveAnthropicBaseUrl)]);
            if (Platform == "deepseek" && string.IsNullOrWhiteSpace(AdaptiveResponsesBaseUrl))
                yield return new ValidationResult("请输入 Responses Base URL", [nameof(AdaptiveResponsesBaseUrl)]);
        }

        foreach (var (value, member, label) in new[]
        {
            (AdaptiveChatCompletionsBaseUrl, nameof(AdaptiveChatCompletionsBaseUrl), "Chat Completions Base URL"),
            (AdaptiveAnthropicBaseUrl, nameof(AdaptiveAnthropicBaseUrl), "Anthropic Base URL"),
            (AdaptiveResponsesBaseUrl, nameof(AdaptiveResponsesBaseUrl), "Responses Base URL")
        })
        {
            if (!string.IsNullOrWhiteSpace(value) && !new UrlAttribute().IsValid(value))
            {
                yield return new ValidationResult($"{label} 格式不正确", [member]);
            }
        }

        var modelRestrictionError = AccountModelRestrictions.Validate(this);
        if (modelRestrictionError is not null)
        {
            yield return new ValidationResult(modelRestrictionError, [nameof(AllowedModels), nameof(ModelMappings)]);
        }

        if (IsEditing && string.IsNullOrWhiteSpace(ApiKey) && string.IsNullOrWhiteSpace(AccessToken)
            && string.IsNullOrWhiteSpace(RefreshToken) && string.IsNullOrWhiteSpace(CredentialsJson))
        {
            yield break;
        }

        var hasJson = !string.IsNullOrWhiteSpace(CredentialsJson);
        var hasApiKey = !string.IsNullOrWhiteSpace(ApiKey);
        var hasAccessToken = !string.IsNullOrWhiteSpace(AccessToken) || !string.IsNullOrWhiteSpace(RefreshToken);
        var message = Type switch
        {
            "apikey" or "upstream" when !hasJson && !hasApiKey => "请输入 API Key，或填写凭据 JSON。",
            "oauth" or "setup-token" when !hasJson && !hasAccessToken => "请输入 access token/refresh token，或填写凭据 JSON。",
            "bedrock" when !hasJson && !hasApiKey => "请填写 Bedrock 凭据 JSON（AWS 区域、访问密钥等）。",
            "service_account" when !hasJson => "请填写 Vertex Service Account JSON。",
            _ => null
        };
        if (message is not null)
        {
            yield return new ValidationResult(message, [nameof(ApiKey), nameof(AccessToken), nameof(CredentialsJson)]);
        }
    }

}

public sealed class GoGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("rate_multiplier")] public double RateMultiplier { get; set; }
    [JsonPropertyName("is_exclusive")] public bool IsExclusive { get; set; }
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("account_count")] public long AccountCount { get; set; }
    [JsonPropertyName("active_account_count")] public long ActiveAccountCount { get; set; }
    [JsonPropertyName("rate_limited_account_count")] public long RateLimitedAccountCount { get; set; }
    [JsonPropertyName("sort_order")] public int SortOrder { get; set; }
    [JsonPropertyName("subscription_type")] public string SubscriptionType { get; set; } = "standard";
    [JsonPropertyName("daily_limit_usd")] public double? DailyLimitUsd { get; set; }
    [JsonPropertyName("weekly_limit_usd")] public double? WeeklyLimitUsd { get; set; }
    [JsonPropertyName("monthly_limit_usd")] public double? MonthlyLimitUsd { get; set; }
    [JsonPropertyName("peak_rate_enabled")] public bool PeakRateEnabled { get; set; }
    [JsonPropertyName("peak_start")] public string PeakStart { get; set; } = string.Empty;
    [JsonPropertyName("peak_end")] public string PeakEnd { get; set; } = string.Empty;
    [JsonPropertyName("peak_rate_multiplier")] public double PeakRateMultiplier { get; set; }
    [JsonPropertyName("long_context_pricing_enabled")] public bool LongContextPricingEnabled { get; set; }
    [JsonPropertyName("allow_batch_image_generation")] public bool AllowBatchImageGeneration { get; set; }
    [JsonPropertyName("model_pricing")] public List<JsonElement> ModelPricing { get; set; } = [];
    [JsonPropertyName("model_routing")] public Dictionary<string, List<long>> ModelRouting { get; set; } = [];
    [JsonPropertyName("model_routing_enabled")] public bool ModelRoutingEnabled { get; set; }
    [JsonPropertyName("supported_model_scopes")] public List<string> SupportedModelScopes { get; set; } = [];
    [JsonPropertyName("allow_messages_dispatch")] public bool AllowMessagesDispatch { get; set; }
    [JsonPropertyName("github_copilot_only")] public bool GitHubCopilotOnly { get; set; }
    [JsonPropertyName("allow_live")] public bool AllowLive { get; set; }
    [JsonPropertyName("require_oauth_only")] public bool RequireOAuthOnly { get; set; }
    [JsonPropertyName("require_privacy_set")] public bool RequirePrivacySet { get; set; }
    [JsonPropertyName("default_mapped_model")] public string DefaultMappedModel { get; set; } = string.Empty;
    // These fields are optional in older Go responses. Nullable prevents an
    // absent JSON value from becoming JsonValueKind.Undefined, which cannot be
    // serialized back through the advanced editor.
    [JsonPropertyName("models_list_config")] public JsonElement? ModelsListConfig { get; set; }
    [JsonPropertyName("messages_dispatch_model_config")] public JsonElement? MessagesDispatchModelConfig { get; set; }
    [JsonPropertyName("rpm_limit")] public int RpmLimit { get; set; }
    [JsonPropertyName("max_reasoning_effort")] public string MaxReasoningEffort { get; set; } = string.Empty;
    [JsonPropertyName("reasoning_effort_mappings")] public List<JsonElement> ReasoningEffortMappings { get; set; } = [];
    [JsonPropertyName("profit_control_enabled")] public bool ProfitControlEnabled { get; set; }
    [JsonPropertyName("profit_min_margin")] public double ProfitMinMargin { get; set; }
    [JsonPropertyName("profit_safety_buffer")] public double ProfitSafetyBuffer { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class GroupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public double RateMultiplier { get; set; }
    public bool IsExclusive { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SubscriptionType { get; set; } = "standard";
    public string AdvancedJson { get; set; } = string.Empty;
    public long AccountCount { get; set; }
    public long ActiveAccountCount { get; set; }
    public long RateLimitedAccountCount { get; set; }
    public int SortOrder { get; set; }
    public double? DailyLimitUsd { get; set; }
    public double? WeeklyLimitUsd { get; set; }
    public double? MonthlyLimitUsd { get; set; }
    public bool PeakRateEnabled { get; set; }
    public string PeakStart { get; set; } = string.Empty;
    public string PeakEnd { get; set; } = string.Empty;
    public double PeakRateMultiplier { get; set; }
    public bool LongContextPricingEnabled { get; set; }
    public bool GitHubCopilotOnly { get; set; }
    public int RpmLimit { get; set; }
    public static GroupDto From(GoGroup group) => new()
    {
        Id = group.Id.ToString(), Name = group.Name, Description = group.Description,
        Platform = group.Platform, RateMultiplier = group.RateMultiplier,
        IsExclusive = group.IsExclusive, Status = group.Status,
        AccountCount = group.AccountCount, ActiveAccountCount = group.ActiveAccountCount,
        RateLimitedAccountCount = group.RateLimitedAccountCount, SortOrder = group.SortOrder,
        DailyLimitUsd = group.DailyLimitUsd, WeeklyLimitUsd = group.WeeklyLimitUsd,
        MonthlyLimitUsd = group.MonthlyLimitUsd, RpmLimit = group.RpmLimit,
        PeakRateEnabled = group.PeakRateEnabled, PeakStart = group.PeakStart,
        PeakEnd = group.PeakEnd, PeakRateMultiplier = group.PeakRateMultiplier,
        LongContextPricingEnabled = group.LongContextPricingEnabled,
        GitHubCopilotOnly = group.GitHubCopilotOnly,
        SubscriptionType = group.SubscriptionType,
        AdvancedJson = JsonSerializer.Serialize(group, JsonDefaults.Options)
    };
}

public sealed class GroupUsageSummaryDto
{
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("today_cost")] public double TodayCost { get; set; }
    [JsonPropertyName("yesterday_cost")] public double YesterdayCost { get; set; }
    [JsonPropertyName("total_cost")] public double TotalCost { get; set; }
}

public sealed class GroupCapacitySummaryDto
{
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("concurrency_used")] public int ConcurrencyUsed { get; set; }
    [JsonPropertyName("concurrency_max")] public int ConcurrencyMax { get; set; }
    [JsonPropertyName("sessions_used")] public int SessionsUsed { get; set; }
    [JsonPropertyName("sessions_max")] public int SessionsMax { get; set; }
    [JsonPropertyName("rpm_used")] public int RpmUsed { get; set; }
    [JsonPropertyName("rpm_max")] public int RpmMax { get; set; }
}

public sealed class GroupUserOverrideDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("user_name")] public string UserName { get; set; } = string.Empty;
    [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;
    [JsonPropertyName("user_notes")] public string UserNotes { get; set; } = string.Empty;
    [JsonPropertyName("user_status")] public string UserStatus { get; set; } = string.Empty;
    [JsonPropertyName("rate_multiplier")] public double? RateMultiplier { get; set; }
    [JsonPropertyName("rpm_override")] public int? RpmOverride { get; set; }
}

public sealed class GroupRateMultiplierInputDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("rate_multiplier")] public double RateMultiplier { get; set; } = 1;
}

public sealed class GroupRpmOverrideInputDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("rpm_override")] public int? RpmOverride { get; set; }
}

public sealed class GroupSortOrderUpdateDto
{
    public long Id { get; set; }
    [JsonPropertyName("sort_order")] public int SortOrder { get; set; }
}

public sealed class CompositeModelRouteDto
{
    public long Id { get; set; }
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("public_model")] public string PublicModel { get; set; } = string.Empty;
    [JsonPropertyName("match_type")] public string MatchType { get; set; } = "exact";
    [JsonPropertyName("target_platform")] public string TargetPlatform { get; set; } = "openai";
    [JsonPropertyName("upstream_model")] public string UpstreamModel { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "any";
    public int Priority { get; set; } = 100;
    public bool Enabled { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class CompositeModelRouteInput
{
    [Required(ErrorMessage = "请输入公开模型名称")]
    [JsonPropertyName("public_model")]
    public string PublicModel { get; set; } = string.Empty;

    [JsonPropertyName("match_type")] public string MatchType { get; set; } = "exact";
    [JsonPropertyName("target_platform")] public string TargetPlatform { get; set; } = "openai";
    [JsonPropertyName("upstream_model")] public string UpstreamModel { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "any";
    [Range(0, 100000)] public int Priority { get; set; } = 100;
    public bool Enabled { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
}

public sealed class CompositeRouteDecisionDto
{
    public bool Matched { get; set; }
    public string Source { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("public_model")] public string PublicModel { get; set; } = string.Empty;
    [JsonPropertyName("target_platform")] public string TargetPlatform { get; set; } = string.Empty;
    [JsonPropertyName("upstream_model")] public string UpstreamModel { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public CompositeModelRouteDto? Route { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class GroupInput
{
    [Required(ErrorMessage = "请输入分组名称"), MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Required] public string Platform { get; set; } = "anthropic";
    [Range(0, 100)] public double RateMultiplier { get; set; } = 1;
    public bool IsExclusive { get; set; }
    public string SubscriptionType { get; set; } = "standard";
    [Range(0, double.MaxValue)] public double? DailyLimitUsd { get; set; }
    [Range(0, double.MaxValue)] public double? WeeklyLimitUsd { get; set; }
    [Range(0, double.MaxValue)] public double? MonthlyLimitUsd { get; set; }
    [Range(0, 1_000_000)] public int RpmLimit { get; set; }
    public bool LongContextPricingEnabled { get; set; } = true;
    public string AdvancedJson { get; set; } = string.Empty;
}

public sealed class GoChannel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("billing_model_source")] public string BillingModelSource { get; set; } = string.Empty;
    [JsonPropertyName("restrict_models")] public bool RestrictModels { get; set; }
    [JsonPropertyName("group_ids")] public List<long> GroupIds { get; set; } = [];
    [JsonPropertyName("model_pricing")] public List<JsonElement> ModelPricing { get; set; } = [];
    [JsonPropertyName("model_mapping")] public Dictionary<string, Dictionary<string, string>> ModelMapping { get; set; } = [];
    public string Features { get; set; } = string.Empty;
    [JsonPropertyName("features_config")] public Dictionary<string, JsonElement> FeaturesConfig { get; set; } = [];
    [JsonPropertyName("apply_pricing_to_account_stats")] public bool ApplyPricingToAccountStats { get; set; }
    [JsonPropertyName("account_stats_pricing_rules")] public List<JsonElement> AccountStatsPricingRules { get; set; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = string.Empty;
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = string.Empty;
}

public sealed class ChannelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BillingModelSource { get; set; } = string.Empty;
    public bool RestrictModels { get; set; }
    public int GroupCount { get; set; }
    public List<long> GroupIds { get; set; } = [];
    public string Features { get; set; } = string.Empty;
    public string FeaturesConfigJson { get; set; } = string.Empty;
    public string ModelPricingJson { get; set; } = string.Empty;
    public string ModelMappingJson { get; set; } = string.Empty;
    public bool ApplyPricingToAccountStats { get; set; }
    public string AccountStatsPricingRulesJson { get; set; } = string.Empty;
    public string AdvancedJson { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public static ChannelDto From(GoChannel channel) => new()
    {
        Id = channel.Id.ToString(), Name = channel.Name, Description = channel.Description,
        Status = channel.Status, BillingModelSource = channel.BillingModelSource,
        RestrictModels = channel.RestrictModels, GroupCount = channel.GroupIds?.Count ?? 0,
        GroupIds = channel.GroupIds ?? [], Features = channel.Features ?? string.Empty,
        FeaturesConfigJson = SerializeJsonValue(channel.FeaturesConfig),
        ModelPricingJson = SerializeJsonValue(channel.ModelPricing),
        ModelMappingJson = SerializeJsonValue(channel.ModelMapping),
        ApplyPricingToAccountStats = channel.ApplyPricingToAccountStats,
        AccountStatsPricingRulesJson = SerializeJsonValue(channel.AccountStatsPricingRules),
        CreatedAt = channel.CreatedAt, UpdatedAt = channel.UpdatedAt,
        AdvancedJson = JsonSerializer.Serialize(channel, JsonDefaults.Options)
    };

    private static string SerializeJsonValue(object? value) =>
        value is null ? string.Empty : JsonSerializer.Serialize(value, JsonDefaults.Options);
}

public sealed class ChannelInput
{
    [Required(ErrorMessage = "请输入渠道名称"), MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<long> GroupIds { get; set; } = [];
    public bool RestrictModels { get; set; }
    public string BillingModelSource { get; set; } = "requested";
    public string Features { get; set; } = string.Empty;
    public string FeaturesConfigJson { get; set; } = string.Empty;
    public string ModelPricingJson { get; set; } = string.Empty;
    public string ModelMappingJson { get; set; } = string.Empty;
    public bool ApplyPricingToAccountStats { get; set; }
    public string AccountStatsPricingRulesJson { get; set; } = string.Empty;
    public string AdvancedJson { get; set; } = string.Empty;
}

public sealed class UserAvailableChannelDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<UserChannelPlatformSectionDto> Platforms { get; set; } = [];
}

public sealed class UserChannelPlatformSectionDto
{
    public string Platform { get; set; } = string.Empty;
    public List<UserAvailableGroupDto> Groups { get; set; } = [];
    [JsonPropertyName("supported_models")] public List<UserSupportedModelDto> SupportedModels { get; set; } = [];
}

public sealed class UserAvailableGroupDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("subscription_type")] public string SubscriptionType { get; set; } = "standard";
    [JsonPropertyName("rate_multiplier")] public double RateMultiplier { get; set; }
    [JsonPropertyName("peak_rate_enabled")] public bool PeakRateEnabled { get; set; }
    [JsonPropertyName("peak_start")] public string PeakStart { get; set; } = string.Empty;
    [JsonPropertyName("peak_end")] public string PeakEnd { get; set; } = string.Empty;
    [JsonPropertyName("peak_rate_multiplier")] public double PeakRateMultiplier { get; set; }
    [JsonPropertyName("is_exclusive")] public bool IsExclusive { get; set; }
}

public sealed class UserSupportedModelDto
{
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public UserSupportedModelPricingDto? Pricing { get; set; }
}

public sealed class UserSupportedModelPricingDto
{
    [JsonPropertyName("billing_mode")] public string BillingMode { get; set; } = "token";
    [JsonPropertyName("input_price")] public double? InputPrice { get; set; }
    [JsonPropertyName("output_price")] public double? OutputPrice { get; set; }
    [JsonPropertyName("cache_write_price")] public double? CacheWritePrice { get; set; }
    [JsonPropertyName("cache_read_price")] public double? CacheReadPrice { get; set; }
    [JsonPropertyName("image_input_price")] public double? ImageInputPrice { get; set; }
    [JsonPropertyName("image_output_price")] public double? ImageOutputPrice { get; set; }
    [JsonPropertyName("per_request_price")] public double? PerRequestPrice { get; set; }
    public List<UserPricingIntervalDto> Intervals { get; set; } = [];
}

public sealed class UserPricingIntervalDto
{
    [JsonPropertyName("min_tokens")] public int MinTokens { get; set; }
    [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }
    [JsonPropertyName("tier_label")] public string? TierLabel { get; set; }
    [JsonPropertyName("input_price")] public double? InputPrice { get; set; }
    [JsonPropertyName("output_price")] public double? OutputPrice { get; set; }
    [JsonPropertyName("cache_write_price")] public double? CacheWritePrice { get; set; }
    [JsonPropertyName("cache_read_price")] public double? CacheReadPrice { get; set; }
    [JsonPropertyName("per_request_price")] public double? PerRequestPrice { get; set; }
}

public sealed class UserMonitorListResponseDto
{
    public List<UserMonitorViewDto> Items { get; set; } = [];
}

public sealed class UserMonitorViewDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    [JsonPropertyName("primary_model")] public string PrimaryModel { get; set; } = string.Empty;
    [JsonPropertyName("primary_status")] public string PrimaryStatus { get; set; } = "unknown";
    [JsonPropertyName("primary_latency_ms")] public int? PrimaryLatencyMs { get; set; }
    [JsonPropertyName("primary_ping_latency_ms")] public int? PrimaryPingLatencyMs { get; set; }
    [JsonPropertyName("availability_7d")] public double Availability7d { get; set; }
    [JsonPropertyName("extra_models")] public List<UserMonitorExtraModelDto> ExtraModels { get; set; } = [];
    public List<UserMonitorTimelinePointDto> Timeline { get; set; } = [];
    [JsonPropertyName("latest_quota")] public MonitorQuotaSnapshotDto? LatestQuota { get; set; }
}

public sealed class UserMonitorExtraModelDto
{
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown";
    [JsonPropertyName("latency_ms")] public int? LatencyMs { get; set; }
}

public sealed class UserMonitorTimelinePointDto
{
    public string Status { get; set; } = "unknown";
    [JsonPropertyName("latency_ms")] public int? LatencyMs { get; set; }
    [JsonPropertyName("ping_latency_ms")] public int? PingLatencyMs { get; set; }
    [JsonPropertyName("checked_at")] public DateTimeOffset CheckedAt { get; set; }
}

public sealed class UserMonitorDetailDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    public List<UserMonitorModelDetailDto> Models { get; set; } = [];
}

public sealed class UserMonitorModelDetailDto
{
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("latest_status")] public string LatestStatus { get; set; } = "unknown";
    [JsonPropertyName("latest_latency_ms")] public int? LatestLatencyMs { get; set; }
    [JsonPropertyName("availability_7d")] public double Availability7d { get; set; }
    [JsonPropertyName("availability_15d")] public double Availability15d { get; set; }
    [JsonPropertyName("availability_30d")] public double Availability30d { get; set; }
    [JsonPropertyName("avg_latency_7d_ms")] public int? AvgLatency7dMs { get; set; }
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

public sealed class GoAvailableModel
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "model";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    public string OwnedBy { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class AvailableModelDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string OwnedBy { get; set; } = string.Empty;
    public static AvailableModelDto From(GoAvailableModel model) => new()
    {
        Id = model.Id, DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName,
        OwnedBy = model.OwnedBy
    };
}

public sealed class AccountTestResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("latency_ms")] public int? LatencyMs { get; set; }
}

public sealed class AccountTestEventDto
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
}

public sealed class ProxyDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = "http";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Username { get; set; }
    public string Status { get; set; } = "active";
    [JsonPropertyName("account_count")] public int AccountCount { get; set; }
    [JsonPropertyName("latency_ms")] public int? LatencyMs { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ProxyInput
{
    [Required(ErrorMessage = "请输入代理名称"), MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required] public string Protocol { get; set; } = "http";
    [Required] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; } = 8080;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class AnnouncementDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    [JsonPropertyName("notify_mode")] public string NotifyMode { get; set; } = "silent";
    public JsonElement? Targeting { get; set; }
    [JsonPropertyName("starts_at")] public DateTimeOffset? StartsAt { get; set; }
    [JsonPropertyName("ends_at")] public DateTimeOffset? EndsAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AnnouncementInput
{
    [Required(ErrorMessage = "请输入公告标题"), MaxLength(200)] public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "请输入公告内容")] public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public string NotifyMode { get; set; } = "silent";
    public string TargetingJson { get; set; } = "{}";
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
}

public sealed class AuditLogDto
{
    public long Id { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("actor_user_id")] public long? ActorUserId { get; set; }
    [JsonPropertyName("actor_email")] public string ActorEmail { get; set; } = string.Empty;
    [JsonPropertyName("actor_role")] public string ActorRole { get; set; } = string.Empty;
    [JsonPropertyName("auth_method")] public string AuthMethod { get; set; } = string.Empty;
    [JsonPropertyName("credential_masked")] public string CredentialMasked { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty;
    [JsonPropertyName("client_ip")] public string ClientIp { get; set; } = string.Empty;
    [JsonPropertyName("user_agent")] public string UserAgent { get; set; } = string.Empty;
    [JsonPropertyName("request_body")] public string RequestBody { get; set; } = string.Empty;
    [JsonPropertyName("status_code")] public int StatusCode { get; set; }
    [JsonPropertyName("latency_ms")] public long LatencyMs { get; set; }
    public Dictionary<string, JsonElement> Extra { get; set; } = [];
}

public sealed class AuditLogClearResultDto
{
    public long Deleted { get; set; }
}

public sealed class UserAnnouncementDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    [JsonPropertyName("notify_mode")] public string NotifyMode { get; set; } = "silent";
    [JsonPropertyName("read_at")] public DateTimeOffset? ReadAt { get; set; }
    [JsonPropertyName("starts_at")] public DateTimeOffset? StartsAt { get; set; }
    [JsonPropertyName("ends_at")] public DateTimeOffset? EndsAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RedeemInput
{
    [Required(ErrorMessage = "请输入兑换码"), MaxLength(200)] public string Code { get; set; } = string.Empty;
}

/// <summary>用户兑换结果及兑换历史中的单条记录。</summary>
public sealed class RedeemCodeDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("used_by")] public long? UsedBy { get; set; }
    [JsonPropertyName("used_at")] public DateTimeOffset? UsedAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("validity_days")] public int ValidityDays { get; set; }
    public string? Notes { get; set; }
    public GoGroup? Group { get; set; }
}

/// <summary>当前用户的邀请返利详情，与官方 /api/v1/user/aff 响应一致。</summary>
public sealed class UserAffiliateDetailDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("aff_code")] public string AffCode { get; set; } = string.Empty;
    [JsonPropertyName("inviter_id")] public long? InviterId { get; set; }
    [JsonPropertyName("aff_count")] public int AffCount { get; set; }
    [JsonPropertyName("aff_quota")] public double AffQuota { get; set; }
    [JsonPropertyName("aff_frozen_quota")] public double AffFrozenQuota { get; set; }
    [JsonPropertyName("aff_history_quota")] public double AffHistoryQuota { get; set; }
    [JsonPropertyName("effective_rebate_rate_percent")] public double EffectiveRebateRatePercent { get; set; }
    public List<AffiliateInviteeDto> Invitees { get; set; } = [];
}

public sealed class AffiliateInviteeDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("total_rebate")] public double TotalRebate { get; set; }
}

public sealed class AffiliateTransferResponseDto
{
    [JsonPropertyName("transferred_quota")] public double TransferredQuota { get; set; }
    public double Balance { get; set; }
}

/// <summary>官方 Go 后端的注册优惠码及使用记录。</summary>
public sealed class PromoCodeDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    [JsonPropertyName("bonus_amount")] public double BonusAmount { get; set; }
    [JsonPropertyName("max_uses")] public int MaxUses { get; set; }
    [JsonPropertyName("used_count")] public int UsedCount { get; set; }
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PromoCodeUsageDto
{
    public long Id { get; set; }
    [JsonPropertyName("promo_code_id")] public long PromoCodeId { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("bonus_amount")] public double BonusAmount { get; set; }
    [JsonPropertyName("used_at")] public DateTimeOffset UsedAt { get; set; }
    public GoUser? User { get; set; }
}

public sealed class PromoCodeForm
{
    [MaxLength(100, ErrorMessage = "优惠码不能超过 100 个字符")]
    public string Code { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "赠送余额不能为负数")]
    public double BonusAmount { get; set; } = 1;

    [Range(0, int.MaxValue, ErrorMessage = "最大使用次数不能为负数")]
    public int MaxUses { get; set; }

    public string ExpiresAtLocal { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}

public sealed class SubscriptionDto
{
    public long Id { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("starts_at")] public DateTimeOffset? StartsAt { get; set; }
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("daily_window_start")] public DateTimeOffset? DailyWindowStart { get; set; }
    [JsonPropertyName("weekly_window_start")] public DateTimeOffset? WeeklyWindowStart { get; set; }
    [JsonPropertyName("monthly_window_start")] public DateTimeOffset? MonthlyWindowStart { get; set; }
    [JsonPropertyName("daily_usage_usd")] public double DailyUsageUsd { get; set; }
    [JsonPropertyName("weekly_usage_usd")] public double WeeklyUsageUsd { get; set; }
    [JsonPropertyName("monthly_usage_usd")] public double MonthlyUsageUsd { get; set; }
    [JsonPropertyName("quota_used")] public double QuotaUsed { get; set; }
    [JsonPropertyName("quota_limit")] public double QuotaLimit { get; set; }
    [JsonPropertyName("assigned_by")] public long? AssignedBy { get; set; }
    [JsonPropertyName("assigned_at")] public DateTimeOffset? AssignedAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("revoked_at")] public DateTimeOffset? RevokedAt { get; set; }
    public GoUser? User { get; set; }
    public GoGroup? Group { get; set; }
    [JsonPropertyName("assigned_by_user")] public GoUser? AssignedByUser { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class SubscriptionAssignInput
{
    [Required(ErrorMessage = "请选择用户")]
    [Range(1, long.MaxValue, ErrorMessage = "请选择用户")]
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    [Required(ErrorMessage = "请选择分组")]
    [Range(1, long.MaxValue, ErrorMessage = "请选择分组")]
    [JsonPropertyName("group_id")]
    public long? GroupId { get; set; }

    [Range(1, 36500, ErrorMessage = "有效期必须在 1 到 36500 天之间")]
    [JsonPropertyName("validity_days")]
    public int ValidityDays { get; set; } = 30;

    public string Notes { get; set; } = string.Empty;
}

public sealed class SubscriptionAdjustInput
{
    [Range(-36500, 36500, ErrorMessage = "调整天数必须在 -36500 到 36500 之间")]
    public int Days { get; set; } = 30;
}

public sealed class SubscriptionQuotaResetInput
{
    public bool Daily { get; set; } = true;
    public bool Weekly { get; set; } = true;
    public bool Monthly { get; set; } = true;
}

public sealed class SubscriptionProgressDto
{
    public long Id { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("expires_in_days")] public int ExpiresInDays { get; set; }
    public SubscriptionUsageWindowDto? Daily { get; set; }
    public SubscriptionUsageWindowDto? Weekly { get; set; }
    public SubscriptionUsageWindowDto? Monthly { get; set; }
}

public sealed class SubscriptionUsageWindowDto
{
    [JsonPropertyName("limit_usd")] public double LimitUsd { get; set; }
    [JsonPropertyName("used_usd")] public double UsedUsd { get; set; }
    [JsonPropertyName("remaining_usd")] public double RemainingUsd { get; set; }
    public double Percentage { get; set; }
    [JsonPropertyName("window_start")] public DateTimeOffset? WindowStart { get; set; }
    [JsonPropertyName("resets_at")] public DateTimeOffset? ResetsAt { get; set; }
    [JsonPropertyName("resets_in_seconds")] public long ResetsInSeconds { get; set; }
}

public sealed class BulkAssignSubscriptionResultDto
{
    [JsonPropertyName("success_count")] public int SuccessCount { get; set; }
    [JsonPropertyName("created_count")] public int CreatedCount { get; set; }
    [JsonPropertyName("reused_count")] public int ReusedCount { get; set; }
    [JsonPropertyName("failed_count")] public int FailedCount { get; set; }
    public List<SubscriptionDto> Subscriptions { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public Dictionary<string, string> Statuses { get; set; } = [];
}

public sealed class ApiKeyListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string Search { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string SortBy { get; set; } = "created_at";
    public string SortOrder { get; set; } = "desc";
}

public sealed class ApiKeyUsageStatDto
{
    [JsonPropertyName("api_key_id")] public long ApiKeyId { get; set; }
    [JsonPropertyName("today_actual_cost")] public double TodayActualCost { get; set; }
    [JsonPropertyName("total_actual_cost")] public double TotalActualCost { get; set; }
}

public sealed class ApiKeyUsageBatchDto
{
    public Dictionary<string, ApiKeyUsageStatDto> Stats { get; set; } = [];
}

public sealed class SelfApiKeyInput : IValidatableObject
{
    [Required(ErrorMessage = "请输入密钥名称")]
    [MaxLength(80, ErrorMessage = "名称不能超过 80 个字符")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "请选择分组")]
    [Range(1, long.MaxValue, ErrorMessage = "请选择分组")]
    public long? GroupId { get; set; }

    public string Status { get; set; } = "active";
    public bool UseCustomKey { get; set; }
    public string CustomKey { get; set; } = string.Empty;
    public bool EnableIpRestriction { get; set; }
    public string IpWhitelistText { get; set; } = string.Empty;
    public string IpBlacklistText { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "额度不能小于 0")]
    public double Quota { get; set; }

    public bool EnableRateLimit { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "5 小时限额不能小于 0")] public double RateLimit5h { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "日限额不能小于 0")] public double RateLimit1d { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "7 天限额不能小于 0")] public double RateLimit7d { get; set; }

    public bool EnableExpiration { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsEditing { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsEditing && UseCustomKey)
        {
            var value = CustomKey.Trim();
            if (value.Length < 16)
            {
                yield return new ValidationResult("自定义密钥至少需要 16 个字符", [nameof(CustomKey)]);
            }
            else if (value.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            {
                yield return new ValidationResult("自定义密钥只能包含字母、数字、下划线和连字符", [nameof(CustomKey)]);
            }
        }

        if (EnableExpiration && !ExpiresAt.HasValue)
        {
            yield return new ValidationResult("请选择过期时间", [nameof(ExpiresAt)]);
        }
        else if (EnableExpiration && !IsEditing && ExpiresAt <= DateTimeOffset.Now)
        {
            yield return new ValidationResult("过期时间必须晚于当前时间", [nameof(ExpiresAt)]);
        }
    }
}

public sealed class OfficialOAuthStartDto
{
    [JsonPropertyName("auth_url")] public string AuthorizationUrl { get; set; } = string.Empty;
    [JsonPropertyName("session_id")] public string SessionId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

/// <summary>Claude/Anthropic 官方 OAuth 开始请求，与 Go accounts 路由保持同一契约。</summary>
public sealed class AnthropicOAuthStartInput
{
    [JsonPropertyName("proxy_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? ProxyId { get; set; }
}

/// <summary>Claude/Anthropic 官方 OAuth 授权码兑换请求。</summary>
public sealed class AnthropicOAuthExchangeInput
{
    [JsonPropertyName("session_id")] public string SessionId { get; set; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("proxy_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? ProxyId { get; set; }
}

public sealed class OAuthBindStartDto
{
    [JsonPropertyName("provider")] public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("authorize_url")] public string AuthorizeUrl { get; set; } = string.Empty;
    [JsonPropertyName("method")] public string Method { get; set; } = "GET";
    [JsonPropertyName("use_browser_redirect")] public bool UseBrowserRedirect { get; set; } = true;
}

public sealed class OAuthLoginStartDto
{
    [JsonPropertyName("authorize_url")] public string AuthorizeUrl { get; set; } = string.Empty;
}

public sealed class OAuthExchangeInput
{
    [JsonPropertyName("session_id")] public string SessionId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    [JsonPropertyName("redirect_uri")] public string? RedirectUri { get; set; }
}

public sealed class OAuthPendingState
{
    public string Platform { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string? OAuthType { get; set; }
    public string? TierId { get; set; }
}

public sealed class ProviderDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "openai";
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthType { get; set; } = "api_key";
    public string MaskedCredential { get; set; } = string.Empty;
    public string MaskedApiKey { get; set; } = string.Empty;
    public string? OauthProfile { get; set; }
    public string? OauthAccountId { get; set; }
    public string? OauthEmail { get; set; }
    public DateTimeOffset? OauthExpiresAt { get; set; }
    public string? WifSubjectTokenUrl { get; set; }
    public string? WifClientId { get; set; }
    public string WifClientAuthMethod { get; set; } = "client_secret_basic";
    public string? WifAudience { get; set; }
    public string? WifScope { get; set; }
    public string? WifIdentityProviderId { get; set; }
    public string? WifServiceAccountId { get; set; }
    public string? WifFederationRuleId { get; set; }
    public string? WifOrganizationId { get; set; }
    public string? WifWorkspaceId { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 100;
    public int Weight { get; set; } = 100;
    public int MaxConcurrency { get; set; } = 8;
    public int RpmLimit { get; set; } = 120;
    public int CircuitBreakerThreshold { get; set; } = 3;
    public int CircuitBreakerCooldownSeconds { get; set; } = 60;
    public string QuotaStatus { get; set; } = "unknown";
    public double? QuotaUtilization { get; set; }
    public DateTimeOffset? QuotaResetsAt { get; set; }
    public DateTimeOffset? QuotaCheckedAt { get; set; }
    public ProviderUsageWindowsDto UsageWindows { get; set; } = new();
    public DateTimeOffset? CooldownUntil { get; set; }
    public string? CooldownReason { get; set; }
    public int? LastUpstreamStatus { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ProviderUsageWindowsDto
{
    public ProviderUsageWindowDto? FiveHour { get; set; }
    public ProviderUsageWindowDto? SevenDay { get; set; }
    public ProviderUsageWindowDto? SevenDaySonnet { get; set; }
}

public sealed class ProviderUsageWindowDto
{
    public double Utilization { get; set; }
    public DateTimeOffset? ResetsAt { get; set; }
}

public sealed class ProviderInput : IValidatableObject
{
    [Required(ErrorMessage = "请输入账号名称")]
    [MaxLength(80, ErrorMessage = "名称不能超过 80 个字符")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "openai";

    [Required(ErrorMessage = "请输入 API 地址")]
    [Url(ErrorMessage = "API 地址格式不正确")]
    public string BaseUrl { get; set; } = "https://api.openai.com";

    [Required(ErrorMessage = "请选择认证方式")]
    public string AuthType { get; set; } = "api_key";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifSubjectTokenUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifClientId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifClientSecret { get; set; }

    public string WifClientAuthMethod { get; set; } = "client_secret_basic";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifAudience { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifScope { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifIdentityProviderId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifServiceAccountId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifFederationRuleId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifOrganizationId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifWorkspaceId { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(1, 9999, ErrorMessage = "优先级需要在 1 到 9999 之间")]
    public int Priority { get; set; } = 100;

    [Range(1, 10000, ErrorMessage = "权重需要在 1 到 10000 之间")]
    public int Weight { get; set; } = 100;

    [Range(1, 10000, ErrorMessage = "最大并发需要在 1 到 10000 之间")]
    public int MaxConcurrency { get; set; } = 8;

    [Range(1, 1000000, ErrorMessage = "RPM 限制需要在 1 到 1000000 之间")]
    public int RpmLimit { get; set; } = 120;

    [Range(1, 1000, ErrorMessage = "熔断阈值需要在 1 到 1000 之间")]
    public int CircuitBreakerThreshold { get; set; } = 3;

    [Range(1, 86400, ErrorMessage = "冷却时间需要在 1 到 86400 秒之间")]
    public int CircuitBreakerCooldownSeconds { get; set; } = 60;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ProviderAuthenticationValidation.Validate(
            AuthType,
            Type,
            WifSubjectTokenUrl,
            WifClientId,
            WifClientAuthMethod,
            WifIdentityProviderId,
            WifServiceAccountId,
            WifFederationRuleId,
            WifOrganizationId);
}

public sealed class ProviderConnectionTestInput : IValidatableObject
{
    public string Type { get; set; } = "openai";
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthType { get; set; } = "api_key";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifSubjectTokenUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifClientId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifClientSecret { get; set; }

    public string WifClientAuthMethod { get; set; } = "client_secret_basic";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifAudience { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifScope { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifIdentityProviderId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifServiceAccountId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifFederationRuleId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifOrganizationId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WifWorkspaceId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ProviderAuthenticationValidation.Validate(
            AuthType,
            Type,
            WifSubjectTokenUrl,
            WifClientId,
            WifClientAuthMethod,
            WifIdentityProviderId,
            WifServiceAccountId,
            WifFederationRuleId,
            WifOrganizationId);
}

internal static class ProviderAuthenticationValidation
{
    public static IEnumerable<ValidationResult> Validate(
        string? authType,
        string? providerType,
        string? subjectTokenUrl,
        string? clientId,
        string? clientAuthMethod,
        string? identityProviderId,
        string? serviceAccountId,
        string? federationRuleId,
        string? organizationId)
    {
        if (authType == "api_key")
        {
            yield break;
        }

        if (authType != "wif")
        {
            yield return new ValidationResult("请选择有效的认证方式", [nameof(ProviderInput.AuthType)]);
            yield break;
        }

        if (!Uri.TryCreate(subjectTokenUrl, UriKind.Absolute, out var tokenUri)
            || tokenUri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult(
                "请输入有效的 HTTPS 外部 IdP Token URL",
                [nameof(ProviderInput.WifSubjectTokenUrl)]);
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            yield return new ValidationResult("请输入外部 IdP Client ID", [nameof(ProviderInput.WifClientId)]);
        }

        if (clientAuthMethod is not ("client_secret_basic" or "client_secret_post"))
        {
            yield return new ValidationResult(
                "请选择有效的外部 IdP 客户端认证方式",
                [nameof(ProviderInput.WifClientAuthMethod)]);
        }

        if (string.IsNullOrWhiteSpace(serviceAccountId))
        {
            yield return new ValidationResult(
                "请输入 Service Account ID",
                [nameof(ProviderInput.WifServiceAccountId)]);
        }

        if (providerType == "openai")
        {
            if (string.IsNullOrWhiteSpace(identityProviderId))
            {
                yield return new ValidationResult(
                    "请输入 Identity Provider ID",
                    [nameof(ProviderInput.WifIdentityProviderId)]);
            }

            yield break;
        }

        if (providerType == "claude")
        {
            if (string.IsNullOrWhiteSpace(federationRuleId))
            {
                yield return new ValidationResult(
                    "请输入 Federation Rule ID",
                    [nameof(ProviderInput.WifFederationRuleId)]);
            }

            if (string.IsNullOrWhiteSpace(organizationId))
            {
                yield return new ValidationResult(
                    "请输入 Organization ID",
                    [nameof(ProviderInput.WifOrganizationId)]);
            }
        }
    }
}

public sealed class ProviderConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int LatencyMs { get; set; }
}

public sealed class OAuthLabAvailabilityDto
{
    public bool Enabled { get; set; }
    public string? CallbackUrl { get; set; }
}

public sealed class OAuthLabStartRequest
{
    [Required]
    public string Profile { get; set; } = "codex_cli";
}

public sealed class OAuthLabStartDto
{
    public string FlowId { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class OAuthLabFlowDto
{
    public string FlowId { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AuthorizedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? MockTokenHint { get; set; }
}

public sealed class ProviderOAuthAvailabilityDto
{
    public bool Enabled { get; set; }
}

public sealed class ProviderOAuthStartRequest
{
    [Required]
    public string Profile { get; set; } = "codex_cli";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProviderAccountId { get; set; }
}

public sealed class ProviderOAuthStartDto
{
    public string FlowId { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class ProviderOAuthExchangeRequest
{
    [Required]
    public string FlowId { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入账号名称")]
    [MaxLength(100, ErrorMessage = "账号名称不能超过 100 个字符")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "请粘贴完整回调地址或 code#state")]
    public string AuthorizationResponse { get; set; } = string.Empty;
}

public sealed class ProviderOAuthExchangeDto
{
    public ProviderDto Provider { get; set; } = new();
    public ProviderOAuthFlowDto Flow { get; set; } = new();
}

public sealed class ProviderOAuthFlowDto
{
    public string FlowId { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProviderAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class CopilotAccountSettingsRequest
{
    [Required(ErrorMessage = "请输入账号名称")]
    [MaxLength(100, ErrorMessage = "账号名称不能超过 100 个字符")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("notes")] public string? Notes { get; set; }
    [JsonPropertyName("proxy_id")] public long? ProxyId { get; set; }
    [JsonPropertyName("concurrency")] public int Concurrency { get; set; } = 8;
    [JsonPropertyName("load_factor")] public int? LoadFactor { get; set; }
    [JsonPropertyName("priority")] public int Priority { get; set; } = 100;
    [JsonPropertyName("rate_multiplier")] public double RateMultiplier { get; set; } = 1;
    [JsonPropertyName("group_ids")] public List<long> GroupIds { get; set; } = [];
    [JsonPropertyName("expires_at")] public long? ExpiresAt { get; set; }
    [JsonPropertyName("auto_pause_on_expired")] public bool AutoPauseOnExpired { get; set; } = true;
    [JsonPropertyName("schedulable")] public bool? Schedulable { get; set; }
    [JsonPropertyName("model_mapping")] public Dictionary<string, string> ModelMapping { get; set; } = [];
    [JsonPropertyName("billing_username")] public string BillingUsername { get; set; } = string.Empty;
    [JsonPropertyName("billing_pat")] public string BillingPat { get; set; } = string.Empty;
    [JsonPropertyName("billing_credit_limit")] public double BillingCreditLimit { get; set; } = 20_000;
    [JsonPropertyName("billing_safety_margin")] public double BillingSafetyMargin { get; set; } = 200;
    [JsonPropertyName("billing_auto_pause_disabled")] public bool BillingAutoPauseDisabled { get; set; }
}

public sealed class CopilotOAuthStartRequest : CopilotAccountSettingsRequest;

public sealed class CopilotManualCreateRequest : CopilotAccountSettingsRequest
{
    [Required(ErrorMessage = "请输入 GitHub Token")]
    [JsonPropertyName("github_token")]
    public string GithubToken { get; set; } = string.Empty;
}

public sealed class CopilotBillingPatValidationRequest
{
    [Required(ErrorMessage = "请输入 GitHub Billing 用户名")]
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入 Billing PAT")]
    [JsonPropertyName("token")]
    public string BillingPat { get; set; } = string.Empty;

    [JsonPropertyName("proxy_id")]
    public long? ProxyId { get; set; }
}

public sealed class CopilotBillingPatValidationDto
{
    public bool Valid { get; set; }
    public string Username { get; set; } = string.Empty;
    public Dictionary<string, JsonElement>? Period { get; set; }
    [JsonPropertyName("items_count")] public int ItemsCount { get; set; }
    [JsonPropertyName("gross_quantity")] public double GrossQuantity { get; set; }
    [JsonPropertyName("gross_amount")] public double GrossAmount { get; set; }
    [JsonPropertyName("net_amount")] public double NetAmount { get; set; }
}

public sealed class OfficialOAuthStartRequest
{
    [Required(ErrorMessage = "请输入账号名称")]
    [MaxLength(100, ErrorMessage = "账号名称不能超过 100 个字符")]
    public string Name { get; set; } = string.Empty;
}

public sealed class CopilotOAuthFlowDto
{
    [JsonPropertyName("flow_id")] public string FlowId { get; set; } = string.Empty;
    public string Profile { get; set; } = "github_copilot";
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("user_code")] public string UserCode { get; set; } = string.Empty;
    [JsonPropertyName("verification_uri")] public string VerificationUri { get; set; } = string.Empty;
    [JsonPropertyName("expires_at")] public DateTimeOffset ExpiresAt { get; set; }
    [JsonPropertyName("interval_seconds")] public int IntervalSeconds { get; set; }
    [JsonPropertyName("next_poll_at")] public DateTimeOffset NextPollAt { get; set; }
    [JsonPropertyName("provider_account_id")] public long? ProviderAccountId { get; set; }
    [JsonPropertyName("completed_at")] public DateTimeOffset? CompletedAt { get; set; }
    // The Go admin endpoint returns the numeric-ID account DTO. Convert it for
    // the Blazor account UI only after the redacted response is deserialized.
    [JsonPropertyName("provider")] public GoAccount? ProviderAccount { get; set; }
    [JsonIgnore] public AccountDto? Provider => ProviderAccount is null ? null : AccountDto.From(ProviderAccount);
}

public sealed class ModelDto
{
    public string Id { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UpstreamModel { get; set; } = string.Empty;
    public long InputPriceMicrosPerMillionTokens { get; set; }
    public long CacheWritePriceMicrosPerMillionTokens { get; set; }
    public long CacheReadPriceMicrosPerMillionTokens { get; set; }
    public long OutputPriceMicrosPerMillionTokens { get; set; }
    public string PriceSource { get; set; } = "manual";
    public string? PriceCatalogVersion { get; set; }
    public DateTimeOffset? PriceSyncedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ModelSyncPreviewRequest
{
    [Required]
    public string ProviderId { get; set; } = string.Empty;
}

public sealed class ModelSyncApplyRequest
{
    [Required]
    public string ProviderId { get; set; } = string.Empty;

    [Required]
    public List<string> ModelIds { get; set; } = [];

    [Required]
    public string CatalogVersion { get; set; } = string.Empty;
}

public sealed class ModelSyncPreviewDto
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string DiscoverySource { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public bool? CatalogHashMatches { get; set; }
    public string? Warning { get; set; }
    public List<ModelSyncItemDto> Models { get; set; } = [];
}

public sealed class ModelSyncItemDto
{
    public string UpstreamModel { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ExistingModelId { get; set; }
    public bool IsActive { get; set; }
    public bool HasPrice { get; set; }
    public long InputPriceMicrosPerMillionTokens { get; set; }
    public long CacheWritePriceMicrosPerMillionTokens { get; set; }
    public long CacheReadPriceMicrosPerMillionTokens { get; set; }
    public long OutputPriceMicrosPerMillionTokens { get; set; }
    public string PriceSource { get; set; } = string.Empty;
    public bool PriceProtected { get; set; }
}

public sealed class ModelSyncResultDto
{
    public string ProviderId { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int DisabledNoPrice { get; set; }
    public string CatalogVersion { get; set; } = string.Empty;
}

public sealed class ModelInput
{
    [Required(ErrorMessage = "请选择上游账号")]
    public string ProviderId { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入对外模型名称")]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入显示名称")]
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入上游模型名称")]
    [MaxLength(160)]
    public string UpstreamModel { get; set; } = string.Empty;

    [Range(0, long.MaxValue, ErrorMessage = "输入价格不能为负数")]
    public long InputPriceMicrosPerMillionTokens { get; set; }

    [Range(0, long.MaxValue, ErrorMessage = "缓存写入价格不能为负数")]
    public long CacheWritePriceMicrosPerMillionTokens { get; set; }

    [Range(0, long.MaxValue, ErrorMessage = "缓存读取价格不能为负数")]
    public long CacheReadPriceMicrosPerMillionTokens { get; set; }

    [Range(0, long.MaxValue, ErrorMessage = "输出价格不能为负数")]
    public long OutputPriceMicrosPerMillionTokens { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class UsageRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ApiKeyId { get; set; } = string.Empty;
    public string ApiKeyName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string PublicModel { get; set; } = string.Empty;
    public string UpstreamModel { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public long PromptTokens { get; set; }
    public long CacheWriteTokens { get; set; }
    public long CacheReadTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public long CostMicros { get; set; }
    public int DurationMs { get; set; }
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static UsageRecordDto From(GoUsageLog log) => new()
    {
        Id = log.Id.ToString(), UserEmail = log.User?.Email ?? string.Empty, ApiKeyId = log.ApiKeyId.ToString(),
        ApiKeyName = log.ApiKey?.Name ?? string.Empty, Department = log.Department ?? string.Empty,
        Endpoint = log.InboundEndpoint ?? string.Empty,
        PublicModel = log.Model, IpAddress = log.IpAddress, PromptTokens = log.InputTokens, CompletionTokens = log.OutputTokens,
        CacheWriteTokens = log.CacheCreationTokens, CacheReadTokens = log.CacheReadTokens,
        TotalTokens = log.InputTokens + log.OutputTokens + log.CacheCreationTokens + log.CacheReadTokens,
        CostMicros = decimal.ToInt64(decimal.Round((decimal)log.ActualCost * 1_000_000m)),
        DurationMs = log.DurationMs ?? 0, StatusCode = log.Success ? 200 : 500, Success = log.Success,
        CreatedAt = log.CreatedAt
    };
}

public sealed class GoUsageLog
{
    public long Id { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("api_key_id")] public long ApiKeyId { get; set; }
    [JsonPropertyName("account_id")] public long? AccountId { get; set; }
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty;
    [JsonPropertyName("department")] public string? Department { get; set; }
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("service_tier")] public string? ServiceTier { get; set; }
    [JsonPropertyName("reasoning_effort")] public string? ReasoningEffort { get; set; }
    [JsonPropertyName("inbound_endpoint")] public string? InboundEndpoint { get; set; }
    [JsonPropertyName("upstream_endpoint")] public string? UpstreamEndpoint { get; set; }
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("subscription_id")] public long? SubscriptionId { get; set; }
    [JsonPropertyName("input_tokens")] public int InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public int OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_tokens")] public int CacheCreationTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public int CacheReadTokens { get; set; }
    [JsonPropertyName("cache_creation_5m_tokens")] public int CacheCreation5mTokens { get; set; }
    [JsonPropertyName("cache_creation_1h_tokens")] public int CacheCreation1hTokens { get; set; }
    [JsonPropertyName("input_cost")] public double InputCost { get; set; }
    [JsonPropertyName("output_cost")] public double OutputCost { get; set; }
    [JsonPropertyName("cache_creation_cost")] public double CacheCreationCost { get; set; }
    [JsonPropertyName("cache_read_cost")] public double CacheReadCost { get; set; }
    [JsonPropertyName("total_cost")] public double TotalCost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
    [JsonPropertyName("rate_multiplier")] public double RateMultiplier { get; set; }
    [JsonPropertyName("long_context_billing_applied")] public bool LongContextBillingApplied { get; set; }
    [JsonPropertyName("billing_type")] public int BillingType { get; set; }
    [JsonPropertyName("duration_ms")] public int? DurationMs { get; set; }
    [JsonPropertyName("first_token_ms")] public int? FirstTokenMs { get; set; }
    [JsonPropertyName("request_type")] public string? RequestType { get; set; }
    public bool Stream { get; set; }
    [JsonPropertyName("openai_ws_mode")] public bool OpenAiWsMode { get; set; }
    [JsonPropertyName("image_count")] public int ImageCount { get; set; }
    [JsonPropertyName("image_size")] public string? ImageSize { get; set; }
    [JsonPropertyName("image_input_size")] public string? ImageInputSize { get; set; }
    [JsonPropertyName("image_output_size")] public string? ImageOutputSize { get; set; }
    [JsonPropertyName("image_size_source")] public string? ImageSizeSource { get; set; }
    [JsonPropertyName("image_size_breakdown")] public Dictionary<string, int>? ImageSizeBreakdown { get; set; }
    [JsonPropertyName("image_input_tokens")] public int ImageInputTokens { get; set; }
    [JsonPropertyName("image_input_cost")] public double ImageInputCost { get; set; }
    [JsonPropertyName("image_output_tokens")] public int ImageOutputTokens { get; set; }
    [JsonPropertyName("image_output_cost")] public double ImageOutputCost { get; set; }
    [JsonPropertyName("user_agent")] public string? UserAgent { get; set; }
    [JsonPropertyName("ip_address")] public string? IpAddress { get; set; }
    [JsonPropertyName("cache_ttl_overridden")] public bool CacheTtlOverridden { get; set; }
    [JsonPropertyName("billing_mode")] public string? BillingMode { get; set; }
    public bool Success => string.IsNullOrWhiteSpace(RequestType) || !RequestType.Contains("error", StringComparison.OrdinalIgnoreCase);
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    public GoUser? User { get; set; }
    [JsonPropertyName("api_key")] public GoApiKey? ApiKey { get; set; }
    public GoGroup? Group { get; set; }
}

public sealed class UserUsageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? ApiKeyId { get; set; }
    public long? GroupId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public int? BillingType { get; set; }
    public string BillingMode { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string SortBy { get; set; } = "created_at";
    public string SortOrder { get; set; } = "desc";
}

public sealed class AdminUsageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? UserId { get; set; }
    public long? ApiKeyId { get; set; }
    public long? AccountId { get; set; }
    public long? GroupId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public int? BillingType { get; set; }
    public string BillingMode { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string SortBy { get; set; } = "created_at";
    public string SortOrder { get; set; } = "desc";
}

public sealed class AdminUsageUserOptionDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool Deleted { get; set; }
}
public sealed class UserUsageStatsDto
{
    public string? Period { get; set; }
    [JsonPropertyName("total_requests")] public long TotalRequests { get; set; }
    [JsonPropertyName("total_input_tokens")] public long TotalInputTokens { get; set; }
    [JsonPropertyName("total_output_tokens")] public long TotalOutputTokens { get; set; }
    [JsonPropertyName("total_cache_tokens")] public long TotalCacheTokens { get; set; }
    [JsonPropertyName("total_cache_read_tokens")] public long TotalCacheReadTokens { get; set; }
    [JsonPropertyName("total_cache_creation_tokens")] public long TotalCacheCreationTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("total_cost")] public double TotalCost { get; set; }
    [JsonPropertyName("total_actual_cost")] public double TotalActualCost { get; set; }
    [JsonPropertyName("average_duration_ms")] public double AverageDurationMs { get; set; }
    public Dictionary<string, long>? Models { get; set; }
    public List<UserUsageEndpointStatDto> Endpoints { get; set; } = [];
}

public sealed class UserUsageTrendPointDto
{
    public string Date { get; set; } = string.Empty;
    public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_tokens")] public long CacheCreationTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }

    [JsonIgnore]
    public double CacheHitRate
    {
        get
        {
            var promptTokens = InputTokens + CacheCreationTokens + CacheReadTokens;
            return promptTokens > 0 ? CacheReadTokens * 100d / promptTokens : 0d;
        }
    }
}

public sealed class UserUsageModelStatDto
{
    public string Model { get; set; } = string.Empty;
    public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_tokens")] public long CacheCreationTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
}

public sealed class UserUsageGroupStatDto
{
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    public long Requests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
}

public sealed class UserUsageEndpointStatDto
{
    public string Endpoint { get; set; } = string.Empty;
    public long Requests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
}

public sealed class UserUsageModelsResponseDto
{
    public List<UserUsageModelStatDto> Models { get; set; } = [];
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
}

public sealed class UserUsageSnapshotDto
{
    [JsonPropertyName("generated_at")] public DateTimeOffset? GeneratedAt { get; set; }
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
    public string Granularity { get; set; } = "day";
    public List<UserUsageTrendPointDto> Trend { get; set; } = [];
    public List<UserUsageModelStatDto> Models { get; set; } = [];
    public List<UserUsageGroupStatDto> Groups { get; set; } = [];
}

public sealed class UserErrorRequestQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string Category { get; set; } = string.Empty;
    public long? ApiKeyId { get; set; }
    public string SortBy { get; set; } = "created_at";
    public string SortOrder { get; set; } = "desc";
}

public class UserErrorRequestDto
{
    public long Id { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("inbound_endpoint")] public string InboundEndpoint { get; set; } = string.Empty;
    [JsonPropertyName("status_code")] public int StatusCode { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("key_name")] public string KeyName { get; set; } = string.Empty;
    [JsonPropertyName("key_deleted")] public bool KeyDeleted { get; set; }
    [JsonPropertyName("client_ip")] public string ClientIp { get; set; } = string.Empty;
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    [JsonPropertyName("request_type")] public int? RequestType { get; set; }
    public bool Stream { get; set; }
    [JsonPropertyName("user_agent")] public string UserAgent { get; set; } = string.Empty;
}

public sealed class UserErrorRequestDetailDto : UserErrorRequestDto
{
    [JsonPropertyName("error_body")] public string ErrorBody { get; set; } = string.Empty;
    [JsonPropertyName("upstream_status_code")] public int? UpstreamStatusCode { get; set; }
}

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int Total { get; set; }
    public int TotalPages { get; set; }

    public static PagedResult<UsageRecordDto> From(PagedEnvelope<GoUsageLog> page) => new()
    {
        Items = page.Items.Select(UsageRecordDto.From).ToList(), Page = page.Page,
        PageSize = page.PageSize, Total = (int)page.Total, TotalPages = page.Pages
    };
}

public sealed class RequestAuditPolicyDto
{
    public short Id { get; set; } = 1;
    public bool Enabled { get; set; }
    [JsonPropertyName("capture_mode")] public string CaptureMode { get; set; } = "all";
    [JsonPropertyName("sample_rate")] public double SampleRate { get; set; } = 100;
    [JsonPropertyName("retention_days")] public int RetentionDays { get; set; } = 30;
    [JsonPropertyName("capture_request_body")] public bool CaptureRequestBody { get; set; } = true;
    [JsonPropertyName("capture_response_body")] public bool CaptureResponseBody { get; set; } = true;
    [JsonPropertyName("store_encrypted_content")] public bool StoreEncryptedContent { get; set; }
    [JsonPropertyName("redaction_level")] public string RedactionLevel { get; set; } = "standard";
    [JsonPropertyName("max_body_bytes")] public int MaxBodyBytes { get; set; } = 1024 * 1024;
    public long Version { get; set; } = 1;
    [JsonPropertyName("updated_by")] public long? UpdatedBy { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("encryption_configured")] public bool EncryptionConfigured { get; set; }
}

public sealed class RequestAuditRuntimeDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("queue_depth")] public int QueueDepth { get; set; }
    [JsonPropertyName("queue_capacity")] public int QueueCapacity { get; set; }
    [JsonPropertyName("enqueued_total")] public long EnqueuedTotal { get; set; }
    [JsonPropertyName("persisted_total")] public long PersistedTotal { get; set; }
    [JsonPropertyName("dropped_total")] public long DroppedTotal { get; set; }
    [JsonPropertyName("failed_total")] public long FailedTotal { get; set; }
    [JsonPropertyName("last_persisted_at")] public DateTimeOffset? LastPersistedAt { get; set; }
    [JsonPropertyName("last_cleanup_at")] public DateTimeOffset? LastCleanupAt { get; set; }
    [JsonPropertyName("last_cleanup_count")] public long LastCleanupCount { get; set; }
}

public sealed class RequestAuditRecordDto
{
    public long Id { get; set; }
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty;
    [JsonPropertyName("user_id")] public long? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;
    [JsonPropertyName("api_key_id")] public long? ApiKeyId { get; set; }
    [JsonPropertyName("api_key_name")] public string ApiKeyName { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("client_ip")] public string ClientIp { get; set; } = string.Empty;
    [JsonPropertyName("status_code")] public int StatusCode { get; set; }
    [JsonPropertyName("latency_ms")] public long LatencyMs { get; set; }
    [JsonPropertyName("is_stream")] public bool IsStream { get; set; }
    [JsonPropertyName("capture_reason")] public string CaptureReason { get; set; } = string.Empty;
    [JsonPropertyName("policy_version")] public long PolicyVersion { get; set; }
    [JsonPropertyName("request_content_type")] public string RequestContentType { get; set; } = string.Empty;
    [JsonPropertyName("response_content_type")] public string ResponseContentType { get; set; } = string.Empty;
    [JsonPropertyName("request_preview")] public string RequestPreview { get; set; } = string.Empty;
    [JsonPropertyName("response_preview")] public string ResponsePreview { get; set; } = string.Empty;
    [JsonPropertyName("encryption_version")] public string EncryptionVersion { get; set; } = string.Empty;
    [JsonPropertyName("request_bytes")] public long RequestBytes { get; set; }
    [JsonPropertyName("response_bytes")] public long ResponseBytes { get; set; }
    [JsonPropertyName("request_truncated")] public bool RequestTruncated { get; set; }
    [JsonPropertyName("response_truncated")] public bool ResponseTruncated { get; set; }
    [JsonPropertyName("request_body_omitted")] public bool RequestBodyOmitted { get; set; }
    [JsonPropertyName("response_body_omitted")] public bool ResponseBodyOmitted { get; set; }
    [JsonPropertyName("content_error")] public string ContentError { get; set; } = string.Empty;
    [JsonPropertyName("expires_at")] public DateTimeOffset? ExpiresAt { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("raw_content_available")] public bool RawContentAvailable { get; set; }
}

public sealed class RequestAuditContentDto
{
    [JsonPropertyName("record_id")] public long RecordId { get; set; }
    [JsonPropertyName("request_body")] public string RequestBody { get; set; } = string.Empty;
    [JsonPropertyName("response_body")] public string ResponseBody { get; set; } = string.Empty;
    [JsonPropertyName("request_available")] public bool RequestAvailable { get; set; }
    [JsonPropertyName("response_available")] public bool ResponseAvailable { get; set; }
}

public sealed class RequestAuditFilterDto
{
    [JsonPropertyName("user_id")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("api_key_id")] public string ApiKeyId { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public string GroupId { get; set; } = string.Empty;
    [JsonPropertyName("status_code")] public string StatusCode { get; set; } = string.Empty;
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    [JsonPropertyName("start_at")] public string StartAt { get; set; } = string.Empty;
    [JsonPropertyName("end_at")] public string EndAt { get; set; } = string.Empty;
}
