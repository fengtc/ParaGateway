using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParaGateway.Frontend.Models;

// DTOs for the administrative surfaces.  They intentionally model only the
// values that are safe and useful to render in the browser; the Go API remains
// the source of truth for validation and for fields not exposed by this UI.
public sealed class AdminSettingsDto
{
    [JsonPropertyName("registration_enabled")] public bool RegistrationEnabled { get; set; }
    [JsonPropertyName("email_verify_enabled")] public bool EmailVerifyEnabled { get; set; }
    [JsonPropertyName("registration_email_suffix_whitelist")] public List<string> RegistrationEmailSuffixWhitelist { get; set; } = [];
    [JsonPropertyName("registration_email_domain_quota_enabled")] public bool RegistrationEmailDomainQuotaEnabled { get; set; }
    [JsonPropertyName("promo_code_enabled")] public bool PromoCodeEnabled { get; set; }
    [JsonPropertyName("password_reset_enabled")] public bool PasswordResetEnabled { get; set; }
    [JsonPropertyName("frontend_url")] public string FrontendUrl { get; set; } = string.Empty;
    [JsonPropertyName("invitation_code_enabled")] public bool InvitationCodeEnabled { get; set; }
    [JsonPropertyName("totp_enabled")] public bool TotpEnabled { get; set; }
    [JsonPropertyName("totp_encryption_key_configured")] public bool TotpEncryptionKeyConfigured { get; set; }
    [JsonPropertyName("passkey_enabled")] public bool PasskeyEnabled { get; set; }
    [JsonPropertyName("passkey_configured")] public bool PasskeyConfigured { get; set; }
    [JsonPropertyName("passkey_rp_id")] public string PasskeyRpId { get; set; } = string.Empty;
    [JsonPropertyName("passkey_rp_origins")] public List<string> PasskeyRpOrigins { get; set; } = [];
    [JsonPropertyName("session_binding_enabled")] public bool SessionBindingEnabled { get; set; }
    [JsonPropertyName("step_up_enabled")] public bool StepUpEnabled { get; set; }
    [JsonPropertyName("audit_log_retention_days")] public int AuditLogRetentionDays { get; set; }
    [JsonPropertyName("login_agreement_enabled")] public bool LoginAgreementEnabled { get; set; }
    [JsonPropertyName("login_agreement_mode")] public string LoginAgreementMode { get; set; } = string.Empty;
    [JsonPropertyName("login_agreement_updated_at")] public string LoginAgreementUpdatedAt { get; set; } = string.Empty;
    [JsonPropertyName("login_agreement_documents")] public List<LegalDocumentDto> LoginAgreementDocuments { get; set; } = [];

    [JsonPropertyName("site_name")] public string SiteName { get; set; } = string.Empty;
    [JsonPropertyName("site_logo")] public string SiteLogo { get; set; } = string.Empty;
    [JsonPropertyName("site_subtitle")] public string SiteSubtitle { get; set; } = string.Empty;
    [JsonPropertyName("api_base_url")] public string ApiBaseUrl { get; set; } = string.Empty;
    [JsonPropertyName("contact_info")] public string ContactInfo { get; set; } = string.Empty;
    [JsonPropertyName("doc_url")] public string DocUrl { get; set; } = string.Empty;
    [JsonPropertyName("home_content")] public string HomeContent { get; set; } = string.Empty;
    [JsonPropertyName("compact_home_enabled")] public bool CompactHomeEnabled { get; set; }
    [JsonPropertyName("hide_ccs_import_button")] public bool HideCcsImportButton { get; set; }
    [JsonPropertyName("table_default_page_size")] public int TableDefaultPageSize { get; set; } = 20;
    [JsonPropertyName("table_page_size_options")] public List<int> TablePageSizeOptions { get; set; } = [];
    [JsonPropertyName("custom_menu_items")] public List<CustomMenuItemDto> CustomMenuItems { get; set; } = [];
    [JsonPropertyName("custom_endpoints")] public List<CustomEndpointDto> CustomEndpoints { get; set; } = [];

    [JsonPropertyName("default_balance")] public decimal DefaultBalance { get; set; }
    [JsonPropertyName("default_concurrency")] public int DefaultConcurrency { get; set; }
    [JsonPropertyName("default_user_rpm_limit")] public int DefaultUserRpmLimit { get; set; }
    [JsonPropertyName("default_subscriptions")] public List<DefaultSubscriptionSettingDto> DefaultSubscriptions { get; set; } = [];
    [JsonPropertyName("default_platform_quotas")] public Dictionary<string, PlatformQuotaLimitsDto> DefaultPlatformQuotas { get; set; } = [];
    [JsonPropertyName("force_email_on_third_party_signup")] public bool ForceEmailOnThirdPartySignup { get; set; }
    [JsonIgnore] public Dictionary<string, AuthSourceDefaultsDto> AuthSourceDefaults { get; set; } = [];
    [JsonPropertyName("affiliate_enabled")] public bool AffiliateEnabled { get; set; }
    [JsonPropertyName("affiliate_rebate_rate")] public decimal AffiliateRebateRate { get; set; }
    [JsonPropertyName("affiliate_rebate_freeze_hours")] public int AffiliateRebateFreezeHours { get; set; }
    [JsonPropertyName("affiliate_rebate_duration_days")] public int AffiliateRebateDurationDays { get; set; }
    [JsonPropertyName("affiliate_rebate_per_invitee_cap")] public decimal AffiliateRebatePerInviteeCap { get; set; }
    [JsonPropertyName("affiliate_admin_recharge_enabled")] public bool AffiliateAdminRechargeEnabled { get; set; }
    [JsonPropertyName("risk_control_enabled")] public bool RiskControlEnabled { get; set; }
    [JsonPropertyName("cyber_session_block_enabled")] public bool CyberSessionBlockEnabled { get; set; }
    [JsonPropertyName("cyber_session_block_ttl_seconds")] public int CyberSessionBlockTtlSeconds { get; set; }
    [JsonPropertyName("backend_mode_enabled")] public bool BackendModeEnabled { get; set; }

    [JsonPropertyName("ops_monitoring_enabled")] public bool OpsMonitoringEnabled { get; set; }
    [JsonPropertyName("ops_realtime_monitoring_enabled")] public bool OpsRealtimeMonitoringEnabled { get; set; }
    [JsonPropertyName("ops_query_mode_default")] public string OpsQueryModeDefault { get; set; } = "auto";
    [JsonPropertyName("ops_metrics_interval_seconds")] public int OpsMetricsIntervalSeconds { get; set; } = 60;
    [JsonPropertyName("channel_monitor_enabled")] public bool ChannelMonitorEnabled { get; set; }
    [JsonPropertyName("channel_monitor_mode")] public string ChannelMonitorMode { get; set; } = "v1";
    [JsonPropertyName("channel_monitor_default_interval_seconds")] public int ChannelMonitorDefaultIntervalSeconds { get; set; } = 300;
    [JsonPropertyName("channel_monitor_hide_throughput")] public bool ChannelMonitorHideThroughput { get; set; }
    [JsonPropertyName("channel_monitor_show_quota")] public bool ChannelMonitorShowQuota { get; set; }
    [JsonPropertyName("available_channels_enabled")] public bool AvailableChannelsEnabled { get; set; }
    [JsonPropertyName("model_plaza_enabled")] public bool ModelPlazaEnabled { get; set; }
    [JsonPropertyName("model_plaza_require_auth")] public bool ModelPlazaRequireAuth { get; set; }
    [JsonPropertyName("model_plaza_description")] public string ModelPlazaDescription { get; set; } = string.Empty;

    [JsonPropertyName("enable_model_fallback")] public bool EnableModelFallback { get; set; }
    [JsonPropertyName("fallback_model_anthropic")] public string FallbackModelAnthropic { get; set; } = string.Empty;
    [JsonPropertyName("fallback_model_openai")] public string FallbackModelOpenAI { get; set; } = string.Empty;
    [JsonPropertyName("fallback_model_gemini")] public string FallbackModelGemini { get; set; } = string.Empty;
    [JsonPropertyName("fallback_model_antigravity")] public string FallbackModelAntigravity { get; set; } = string.Empty;
    [JsonPropertyName("grok_default_text_model")] public string GrokDefaultTextModel { get; set; } = string.Empty;
    [JsonPropertyName("grok_cross_client_model_map_enabled")] public bool GrokCrossClientModelMapEnabled { get; set; }
    [JsonPropertyName("grok_default_base_url_mode")] public string GrokDefaultBaseUrlMode { get; set; } = string.Empty;
    [JsonPropertyName("enable_identity_patch")] public bool EnableIdentityPatch { get; set; }
    [JsonPropertyName("identity_patch_prompt")] public string IdentityPatchPrompt { get; set; } = string.Empty;

    [JsonPropertyName("min_claude_code_version")] public string MinClaudeCodeVersion { get; set; } = string.Empty;
    [JsonPropertyName("max_claude_code_version")] public string MaxClaudeCodeVersion { get; set; } = string.Empty;
    [JsonPropertyName("allow_ungrouped_key_scheduling")] public bool AllowUngroupedKeyScheduling { get; set; }
    [JsonPropertyName("enable_fingerprint_unification")] public bool EnableFingerprintUnification { get; set; }
    [JsonPropertyName("enable_metadata_passthrough")] public bool EnableMetadataPassthrough { get; set; }
    [JsonPropertyName("enable_cch_signing")] public bool EnableCchSigning { get; set; }
    [JsonPropertyName("enable_claude_oauth_system_prompt_injection")] public bool EnableClaudeOAuthSystemPromptInjection { get; set; }
    [JsonPropertyName("claude_oauth_system_prompt")] public string ClaudeOAuthSystemPrompt { get; set; } = string.Empty;
    [JsonPropertyName("claude_oauth_system_prompt_blocks")] public string ClaudeOAuthSystemPromptBlocks { get; set; } = string.Empty;
    [JsonPropertyName("enable_anthropic_cache_ttl_1h_injection")] public bool EnableAnthropicCacheTtl1hInjection { get; set; }
    [JsonPropertyName("rewrite_message_cache_control")] public bool RewriteMessageCacheControl { get; set; }
    [JsonPropertyName("enable_client_dateline_normalization")] public bool EnableClientDatelineNormalization { get; set; }
    [JsonPropertyName("antigravity_user_agent_version")] public string AntigravityUserAgentVersion { get; set; } = string.Empty;
    [JsonPropertyName("openai_codex_user_agent")] public string OpenAICodexUserAgent { get; set; } = string.Empty;
    [JsonPropertyName("openai_codex_client_version")] public string OpenAICodexClientVersion { get; set; } = string.Empty;
    [JsonPropertyName("openai_codex_client_version_synced")] public string OpenAICodexClientVersionSynced { get; set; } = string.Empty;
    [JsonPropertyName("openai_codex_version_auto_sync_enabled")] public bool OpenAICodexVersionAutoSyncEnabled { get; set; }
    [JsonPropertyName("min_codex_version")] public string MinCodexVersion { get; set; } = string.Empty;
    [JsonPropertyName("max_codex_version")] public string MaxCodexVersion { get; set; } = string.Empty;
    [JsonPropertyName("codex_cli_only_blacklist")] public string CodexCliOnlyBlacklist { get; set; } = string.Empty;
    [JsonPropertyName("codex_cli_only_whitelist")] public string CodexCliOnlyWhitelist { get; set; } = string.Empty;
    [JsonPropertyName("codex_cli_only_allow_app_server_clients")] public bool CodexCliOnlyAllowAppServerClients { get; set; }
    [JsonPropertyName("codex_cli_only_engine_fingerprint_signals")] public string CodexCliOnlyEngineFingerprintSignals { get; set; } = string.Empty;

    [JsonPropertyName("openai_low_upstream_rate_priority_enabled")] public bool OpenAILowUpstreamRatePriorityEnabled { get; set; }
    [JsonPropertyName("openai_oauth_scheduling_rate_multiplier")] public double OpenAIOAuthSchedulingRateMultiplier { get; set; } = 1;
    [JsonPropertyName("openai_advanced_scheduler_enabled")] public bool OpenAIAdvancedSchedulerEnabled { get; set; }
    [JsonPropertyName("openai_advanced_scheduler_sticky_weighted_enabled")] public bool OpenAIAdvancedSchedulerStickyWeightedEnabled { get; set; }
    [JsonPropertyName("openai_advanced_scheduler_subscription_priority_enabled")] public bool OpenAIAdvancedSchedulerSubscriptionPriorityEnabled { get; set; }
    [JsonPropertyName("openai_advanced_scheduler_lb_top_k")] public string OpenAIAdvancedSchedulerLbTopK { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_priority")] public string OpenAIAdvancedSchedulerWeightPriority { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_load")] public string OpenAIAdvancedSchedulerWeightLoad { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_queue")] public string OpenAIAdvancedSchedulerWeightQueue { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_error_rate")] public string OpenAIAdvancedSchedulerWeightErrorRate { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_ttft")] public string OpenAIAdvancedSchedulerWeightTtft { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_reset")] public string OpenAIAdvancedSchedulerWeightReset { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_quota_headroom")] public string OpenAIAdvancedSchedulerWeightQuotaHeadroom { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_upstream_cost")] public string OpenAIAdvancedSchedulerWeightUpstreamCost { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_previous_response")] public string OpenAIAdvancedSchedulerWeightPreviousResponse { get; set; } = string.Empty;
    [JsonPropertyName("openai_advanced_scheduler_weight_session_sticky")] public string OpenAIAdvancedSchedulerWeightSessionSticky { get; set; } = string.Empty;
    [JsonPropertyName("account_scheduling_thresholds")] public Dictionary<string, int> AccountSchedulingThresholds { get; set; } = [];
    [JsonPropertyName("openai_fast_policy_settings")] public OpenAIFastPolicySettingsDto OpenAIFastPolicySettings { get; set; } = new();
    [JsonPropertyName("allow_user_view_error_requests")] public bool AllowUserViewErrorRequests { get; set; }

    [JsonPropertyName("smtp_host")] public string SmtpHost { get; set; } = string.Empty;
    [JsonPropertyName("smtp_port")] public int SmtpPort { get; set; }
    [JsonPropertyName("smtp_username")] public string SmtpUsername { get; set; } = string.Empty;
    [JsonPropertyName("smtp_password_configured")] public bool SmtpPasswordConfigured { get; set; }
    [JsonPropertyName("smtp_from_email")] public string SmtpFromEmail { get; set; } = string.Empty;
    [JsonPropertyName("smtp_from_name")] public string SmtpFromName { get; set; } = string.Empty;
    [JsonPropertyName("smtp_use_tls")] public bool SmtpUseTls { get; set; }
    [JsonPropertyName("balance_low_notify_enabled")] public bool BalanceLowNotifyEnabled { get; set; }
    [JsonPropertyName("balance_low_notify_threshold")] public decimal BalanceLowNotifyThreshold { get; set; }
    [JsonPropertyName("balance_low_notify_recharge_url")] public string BalanceLowNotifyRechargeUrl { get; set; } = string.Empty;
    [JsonPropertyName("subscription_expiry_notify_enabled")] public bool SubscriptionExpiryNotifyEnabled { get; set; } = true;
    [JsonPropertyName("account_quota_notify_enabled")] public bool AccountQuotaNotifyEnabled { get; set; }
    [JsonPropertyName("account_quota_notify_emails")] public List<NotifyEmailEntryDto> AccountQuotaNotifyEmails { get; set; } = [];

    [JsonPropertyName("turnstile_enabled")] public bool TurnstileEnabled { get; set; }
    [JsonPropertyName("turnstile_site_key")] public string TurnstileSiteKey { get; set; } = string.Empty;
    [JsonPropertyName("turnstile_secret_key_configured")] public bool TurnstileSecretKeyConfigured { get; set; }
    [JsonPropertyName("tencent_captcha_enabled")] public bool TencentCaptchaEnabled { get; set; }
    [JsonPropertyName("tencent_captcha_app_id")] public string TencentCaptchaAppId { get; set; } = string.Empty;
    [JsonPropertyName("tencent_captcha_app_secret_key_configured")] public bool TencentCaptchaAppSecretKeyConfigured { get; set; }
    [JsonPropertyName("tencent_captcha_cloud_secret_id_configured")] public bool TencentCaptchaCloudSecretIdConfigured { get; set; }
    [JsonPropertyName("tencent_captcha_cloud_secret_key_configured")] public bool TencentCaptchaCloudSecretKeyConfigured { get; set; }
    [JsonPropertyName("tencent_captcha_region")] public string TencentCaptchaRegion { get; set; } = "cn";
    [JsonPropertyName("aliyun_captcha_enabled")] public bool AliyunCaptchaEnabled { get; set; }
    [JsonPropertyName("aliyun_captcha_access_key_id")] public string AliyunCaptchaAccessKeyId { get; set; } = string.Empty;
    [JsonPropertyName("aliyun_captcha_access_key_secret_configured")] public bool AliyunCaptchaAccessKeySecretConfigured { get; set; }
    [JsonPropertyName("aliyun_captcha_scene_id")] public string AliyunCaptchaSceneId { get; set; } = string.Empty;
    [JsonPropertyName("aliyun_captcha_prefix")] public string AliyunCaptchaPrefix { get; set; } = string.Empty;
    [JsonPropertyName("aliyun_captcha_region")] public string AliyunCaptchaRegion { get; set; } = "cn";
    [JsonPropertyName("api_key_acl_trust_forwarded_ip")] public bool ApiKeyAclTrustForwardedIp { get; set; } = true;
    [JsonPropertyName("forwarded_client_ip_headers")] public List<string> ForwardedClientIpHeaders { get; set; } = [];

    [JsonPropertyName("linuxdo_connect_enabled")] public bool LinuxDoConnectEnabled { get; set; }
    [JsonPropertyName("linuxdo_connect_client_id")] public string LinuxDoConnectClientId { get; set; } = string.Empty;
    [JsonPropertyName("linuxdo_connect_client_secret_configured")] public bool LinuxDoConnectClientSecretConfigured { get; set; }
    [JsonPropertyName("linuxdo_connect_redirect_url")] public string LinuxDoConnectRedirectUrl { get; set; } = string.Empty;

    [JsonPropertyName("dingtalk_connect_enabled")] public bool DingTalkConnectEnabled { get; set; }
    [JsonPropertyName("dingtalk_connect_client_id")] public string DingTalkConnectClientId { get; set; } = string.Empty;
    [JsonPropertyName("dingtalk_connect_client_secret_configured")] public bool DingTalkConnectClientSecretConfigured { get; set; }
    [JsonPropertyName("dingtalk_connect_redirect_url")] public string DingTalkConnectRedirectUrl { get; set; } = string.Empty;
    [JsonPropertyName("dingtalk_connect_corp_restriction_policy")] public string DingTalkConnectCorpRestrictionPolicy { get; set; } = "none";
    [JsonPropertyName("dingtalk_connect_internal_corp_id")] public string DingTalkConnectInternalCorpId { get; set; } = string.Empty;
    [JsonPropertyName("dingtalk_connect_bypass_registration")] public bool DingTalkConnectBypassRegistration { get; set; }
    [JsonPropertyName("dingtalk_connect_sync_corp_email")] public bool DingTalkConnectSyncCorpEmail { get; set; }
    [JsonPropertyName("dingtalk_connect_sync_display_name")] public bool DingTalkConnectSyncDisplayName { get; set; }
    [JsonPropertyName("dingtalk_connect_sync_dept")] public bool DingTalkConnectSyncDept { get; set; }
    [JsonPropertyName("dingtalk_connect_sync_corp_email_attr_key")] public string DingTalkConnectSyncCorpEmailAttrKey { get; set; } = "dingtalk_email";
    [JsonPropertyName("dingtalk_connect_sync_display_name_attr_key")] public string DingTalkConnectSyncDisplayNameAttrKey { get; set; } = "dingtalk_name";
    [JsonPropertyName("dingtalk_connect_sync_dept_attr_key")] public string DingTalkConnectSyncDeptAttrKey { get; set; } = "dingtalk_department";
    [JsonPropertyName("dingtalk_connect_sync_corp_email_attr_name")] public string DingTalkConnectSyncCorpEmailAttrName { get; set; } = "钉钉企业邮箱";
    [JsonPropertyName("dingtalk_connect_sync_display_name_attr_name")] public string DingTalkConnectSyncDisplayNameAttrName { get; set; } = "钉钉姓名";
    [JsonPropertyName("dingtalk_connect_sync_dept_attr_name")] public string DingTalkConnectSyncDeptAttrName { get; set; } = "钉钉部门";

    [JsonPropertyName("wechat_connect_enabled")] public bool WeChatConnectEnabled { get; set; }
    [JsonPropertyName("wechat_connect_app_id")] public string WeChatConnectAppId { get; set; } = string.Empty;
    [JsonPropertyName("wechat_connect_app_secret_configured")] public bool WeChatConnectAppSecretConfigured { get; set; }
    [JsonPropertyName("wechat_connect_open_app_id")] public string WeChatConnectOpenAppId { get; set; } = string.Empty;
    [JsonPropertyName("wechat_connect_open_app_secret_configured")] public bool WeChatConnectOpenAppSecretConfigured { get; set; }
    [JsonPropertyName("wechat_connect_mp_app_id")] public string WeChatConnectMpAppId { get; set; } = string.Empty;
    [JsonPropertyName("wechat_connect_mp_app_secret_configured")] public bool WeChatConnectMpAppSecretConfigured { get; set; }
    [JsonPropertyName("wechat_connect_mobile_app_id")] public string WeChatConnectMobileAppId { get; set; } = string.Empty;
    [JsonPropertyName("wechat_connect_mobile_app_secret_configured")] public bool WeChatConnectMobileAppSecretConfigured { get; set; }
    [JsonPropertyName("wechat_connect_open_enabled")] public bool WeChatConnectOpenEnabled { get; set; }
    [JsonPropertyName("wechat_connect_mp_enabled")] public bool WeChatConnectMpEnabled { get; set; }
    [JsonPropertyName("wechat_connect_mobile_enabled")] public bool WeChatConnectMobileEnabled { get; set; }
    [JsonPropertyName("wechat_connect_mode")] public string WeChatConnectMode { get; set; } = "open";
    [JsonPropertyName("wechat_connect_scopes")] public string WeChatConnectScopes { get; set; } = "snsapi_login";
    [JsonPropertyName("wechat_connect_redirect_url")] public string WeChatConnectRedirectUrl { get; set; } = string.Empty;
    [JsonPropertyName("wechat_connect_frontend_redirect_url")] public string WeChatConnectFrontendRedirectUrl { get; set; } = "/auth/wechat/callback";

    [JsonPropertyName("github_oauth_enabled")] public bool GitHubOAuthEnabled { get; set; }
    [JsonPropertyName("github_oauth_client_id")] public string GitHubOAuthClientId { get; set; } = string.Empty;
    [JsonPropertyName("github_oauth_client_secret_configured")] public bool GitHubOAuthClientSecretConfigured { get; set; }
    [JsonPropertyName("github_oauth_redirect_url")] public string GitHubOAuthRedirectUrl { get; set; } = string.Empty;
    [JsonPropertyName("github_oauth_frontend_redirect_url")] public string GitHubOAuthFrontendRedirectUrl { get; set; } = string.Empty;
    [JsonPropertyName("google_oauth_enabled")] public bool GoogleOAuthEnabled { get; set; }
    [JsonPropertyName("google_oauth_client_id")] public string GoogleOAuthClientId { get; set; } = string.Empty;
    [JsonPropertyName("google_oauth_client_secret_configured")] public bool GoogleOAuthClientSecretConfigured { get; set; }
    [JsonPropertyName("google_oauth_redirect_url")] public string GoogleOAuthRedirectUrl { get; set; } = string.Empty;
    [JsonPropertyName("google_oauth_frontend_redirect_url")] public string GoogleOAuthFrontendRedirectUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_enabled")] public bool OidcConnectEnabled { get; set; }
    [JsonPropertyName("oidc_connect_provider_name")] public string OidcConnectProviderName { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_client_id")] public string OidcConnectClientId { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_client_secret_configured")] public bool OidcConnectClientSecretConfigured { get; set; }
    [JsonPropertyName("oidc_connect_issuer_url")] public string OidcConnectIssuerUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_discovery_url")] public string OidcConnectDiscoveryUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_authorize_url")] public string OidcConnectAuthorizeUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_token_url")] public string OidcConnectTokenUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_userinfo_url")] public string OidcConnectUserinfoUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_jwks_url")] public string OidcConnectJwksUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_scopes")] public string OidcConnectScopes { get; set; } = "openid profile email";
    [JsonPropertyName("oidc_connect_redirect_url")] public string OidcConnectRedirectUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_frontend_redirect_url")] public string OidcConnectFrontendRedirectUrl { get; set; } = string.Empty;
    [JsonPropertyName("oidc_connect_token_auth_method")] public string OidcConnectTokenAuthMethod { get; set; } = "client_secret_post";
    [JsonPropertyName("oidc_connect_use_pkce")] public bool OidcConnectUsePkce { get; set; }
    [JsonPropertyName("oidc_connect_validate_id_token")] public bool OidcConnectValidateIdToken { get; set; } = true;
    [JsonPropertyName("oidc_connect_allowed_signing_algs")] public string OidcConnectAllowedSigningAlgs { get; set; } = "RS256";
    [JsonPropertyName("oidc_connect_clock_skew_seconds")] public int OidcConnectClockSkewSeconds { get; set; } = 60;
    [JsonPropertyName("oidc_connect_require_email_verified")] public bool OidcConnectRequireEmailVerified { get; set; }
    [JsonPropertyName("oidc_connect_userinfo_email_path")] public string OidcConnectUserinfoEmailPath { get; set; } = "email";
    [JsonPropertyName("oidc_connect_userinfo_id_path")] public string OidcConnectUserinfoIdPath { get; set; } = "sub";
    [JsonPropertyName("oidc_connect_userinfo_username_path")] public string OidcConnectUserinfoUsernamePath { get; set; } = "preferred_username";

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class AdminSecuritySecretInputs
{
    public string TurnstileSecretKey { get; set; } = string.Empty;
    public string TencentCaptchaAppSecretKey { get; set; } = string.Empty;
    public string TencentCaptchaCloudSecretId { get; set; } = string.Empty;
    public string TencentCaptchaCloudSecretKey { get; set; } = string.Empty;
    public string AliyunCaptchaAccessKeySecret { get; set; } = string.Empty;
    public string LinuxDoConnectClientSecret { get; set; } = string.Empty;
    public string DingTalkConnectClientSecret { get; set; } = string.Empty;
    public string WeChatConnectAppSecret { get; set; } = string.Empty;
    public string WeChatConnectOpenAppSecret { get; set; } = string.Empty;
    public string WeChatConnectMpAppSecret { get; set; } = string.Empty;
    public string WeChatConnectMobileAppSecret { get; set; } = string.Empty;
    public string GitHubOAuthClientSecret { get; set; } = string.Empty;
    public string GoogleOAuthClientSecret { get; set; } = string.Empty;
    public string OidcConnectClientSecret { get; set; } = string.Empty;
}

public sealed class DefaultSubscriptionSettingDto
{
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("validity_days")] public int ValidityDays { get; set; } = 30;
}

public sealed class PlatformQuotaLimitsDto
{
    public decimal? Daily { get; set; }
    public decimal? Weekly { get; set; }
    public decimal? Monthly { get; set; }
}

public sealed class AuthSourceDefaultsDto
{
    public decimal Balance { get; set; }
    public int Concurrency { get; set; } = 5;
    public List<DefaultSubscriptionSettingDto> Subscriptions { get; set; } = [];
    public bool GrantOnSignup { get; set; }
    public bool GrantOnFirstBind { get; set; }
    public Dictionary<string, PlatformQuotaLimitsDto> PlatformQuotas { get; set; } = [];
}

public sealed class CustomEndpointDto
{
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class OpenAIFastPolicySettingsDto
{
    public List<OpenAIFastPolicyRuleDto> Rules { get; set; } = [];
}

public sealed class OpenAIFastPolicyRuleDto
{
    [JsonPropertyName("service_tier")] public string ServiceTier { get; set; } = "fast";
    public string Action { get; set; } = "pass";
    public string Scope { get; set; } = "all";
    [JsonPropertyName("user_ids")] public List<long> UserIds { get; set; } = [];
    [JsonPropertyName("error_message")] public string ErrorMessage { get; set; } = string.Empty;
    [JsonPropertyName("model_whitelist")] public List<string> ModelWhitelist { get; set; } = [];
    [JsonPropertyName("fallback_action")] public string FallbackAction { get; set; } = "pass";
    [JsonPropertyName("fallback_error_message")] public string FallbackErrorMessage { get; set; } = string.Empty;
}

public sealed class AdminApiKeyStatusDto
{
    public bool Exists { get; set; }
    [JsonPropertyName("masked_key")] public string MaskedKey { get; set; } = string.Empty;
}

public sealed class AdminApiKeyGeneratedDto
{
    public string Key { get; set; } = string.Empty;
}

public sealed class OverloadCooldownSettingsDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("cooldown_minutes")] public int CooldownMinutes { get; set; }
}

public sealed class RateLimit429CooldownSettingsDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("cooldown_seconds")] public int CooldownSeconds { get; set; }
}

public sealed class PanelRateLimitSettingsDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("user_rpm")] public int UserRpm { get; set; }
    [JsonPropertyName("heavy_rpm")] public int HeavyRpm { get; set; }
    [JsonPropertyName("public_ip_rpm")] public int PublicIpRpm { get; set; }
    [JsonPropertyName("exempt_admin")] public bool ExemptAdmin { get; set; }
}

public sealed class StreamTimeoutSettingsDto
{
    public bool Enabled { get; set; }
    public string Action { get; set; } = "temp_unsched";
    [JsonPropertyName("temp_unsched_minutes")] public int TempUnschedMinutes { get; set; }
    [JsonPropertyName("threshold_count")] public int ThresholdCount { get; set; }
    [JsonPropertyName("threshold_window_minutes")] public int ThresholdWindowMinutes { get; set; }
}

public sealed class RectifierSettingsDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("thinking_signature_enabled")] public bool ThinkingSignatureEnabled { get; set; }
    [JsonPropertyName("thinking_budget_enabled")] public bool ThinkingBudgetEnabled { get; set; }
    [JsonPropertyName("apikey_signature_enabled")] public bool ApiKeySignatureEnabled { get; set; }
    [JsonPropertyName("apikey_signature_patterns")] public List<string> ApiKeySignaturePatterns { get; set; } = [];
}

public sealed class BetaPolicySettingsDto
{
    public List<BetaPolicyRuleDto> Rules { get; set; } = [];
}

public sealed class BetaPolicyRuleDto
{
    [JsonPropertyName("beta_token")] public string BetaToken { get; set; } = string.Empty;
    public string Action { get; set; } = "pass";
    public string Scope { get; set; } = "all";
    [JsonPropertyName("error_message")] public string ErrorMessage { get; set; } = string.Empty;
    [JsonPropertyName("model_whitelist")] public List<string> ModelWhitelist { get; set; } = [];
    [JsonPropertyName("fallback_action")] public string FallbackAction { get; set; } = "pass";
    [JsonPropertyName("fallback_error_message")] public string FallbackErrorMessage { get; set; } = string.Empty;
}

public sealed class WebSearchEmulationConfigDto
{
    public bool Enabled { get; set; }
    public List<WebSearchProviderConfigDto> Providers { get; set; } = [];
}

public sealed class WebSearchProviderConfigDto
{
    public string Type { get; set; } = "brave";
    [JsonPropertyName("api_key")] public string ApiKey { get; set; } = string.Empty;
    [JsonPropertyName("api_key_configured")] public bool ApiKeyConfigured { get; set; }
    [JsonPropertyName("quota_limit")] public long? QuotaLimit { get; set; }
    [JsonPropertyName("subscribed_at")] public long? SubscribedAt { get; set; }
    [JsonPropertyName("quota_used")] public long QuotaUsed { get; set; }
    [JsonPropertyName("proxy_id")] public long? ProxyId { get; set; }
    [JsonPropertyName("expires_at")] public long? ExpiresAt { get; set; }
}

public sealed class WebSearchTestResultDto
{
    public string Provider { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public List<WebSearchResultItemDto> Results { get; set; } = [];
}

public sealed class WebSearchResultItemDto
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    [JsonPropertyName("page_age")] public string PageAge { get; set; } = string.Empty;
}

public class SmtpTestRequestDto
{
    [JsonPropertyName("smtp_host")] public string SmtpHost { get; set; } = string.Empty;
    [JsonPropertyName("smtp_port")] public int SmtpPort { get; set; }
    [JsonPropertyName("smtp_username")] public string SmtpUsername { get; set; } = string.Empty;
    [JsonPropertyName("smtp_password")] public string SmtpPassword { get; set; } = string.Empty;
    [JsonPropertyName("smtp_use_tls")] public bool SmtpUseTls { get; set; }
}

public sealed class SendTestEmailRequestDto : SmtpTestRequestDto
{
    public string Email { get; set; } = string.Empty;
    [JsonPropertyName("smtp_from_email")] public string SmtpFromEmail { get; set; } = string.Empty;
    [JsonPropertyName("smtp_from_name")] public string SmtpFromName { get; set; } = string.Empty;
}

public sealed class ApiMessageDto
{
    public string Message { get; set; } = string.Empty;
}

public sealed class EmailTemplateEventOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Optional { get; set; }
}

public sealed class EmailTemplateSummaryDto
{
    public string Event { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    [JsonPropertyName("is_custom")] public bool IsCustom { get; set; }
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = string.Empty;
}

public sealed class EmailTemplateListDto
{
    public List<EmailTemplateEventOptionDto> Events { get; set; } = [];
    public List<string> Locales { get; set; } = [];
    public List<EmailTemplateSummaryDto> Templates { get; set; } = [];
    public List<string> Placeholders { get; set; } = [];
}

public sealed class EmailTemplateDetailDto
{
    public string Event { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    [JsonPropertyName("is_custom")] public bool IsCustom { get; set; }
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = string.Empty;
    public List<string> Placeholders { get; set; } = [];
}

public sealed class EmailTemplatePreviewDto
{
    public string Subject { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
}

public sealed class UpstreamBillingProbeSettingsDto
{
    public bool Enabled { get; set; } = true;
    [JsonPropertyName("interval_minutes")] public int IntervalMinutes { get; set; } = 30;
}

public sealed class OllamaCloudUsageSettingsDto
{
    public bool Enabled { get; set; }
    [JsonPropertyName("interval_minutes")] public int IntervalMinutes { get; set; } = 60;
    [JsonPropertyName("debounce_minutes")] public int DebounceMinutes { get; set; } = 1;
}

public sealed class OpsDashboardSnapshotDto
{
    [JsonPropertyName("generated_at")] public DateTimeOffset? GeneratedAt { get; set; }
    public OpsDashboardOverviewDto Overview { get; set; } = new();
    [JsonPropertyName("throughput_trend")] public OpsThroughputTrendDto ThroughputTrend { get; set; } = new();
    [JsonPropertyName("error_trend")] public OpsErrorTrendDto ErrorTrend { get; set; } = new();
}

public sealed class OpsDashboardOverviewDto
{
    [JsonPropertyName("start_time")] public DateTimeOffset? StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTimeOffset? EndTime { get; set; }
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("health_score")] public int HealthScore { get; set; }
    [JsonPropertyName("success_count")] public long SuccessCount { get; set; }
    [JsonPropertyName("error_count_total")] public long ErrorCountTotal { get; set; }
    [JsonPropertyName("business_limited_count")] public long BusinessLimitedCount { get; set; }
    [JsonPropertyName("error_count_sla")] public long ErrorCountSla { get; set; }
    [JsonPropertyName("request_count_total")] public long RequestCountTotal { get; set; }
    [JsonPropertyName("request_count_sla")] public long RequestCountSla { get; set; }
    [JsonPropertyName("token_consumed")] public long TokenConsumed { get; set; }
    public double Sla { get; set; }
    [JsonPropertyName("error_rate")] public double ErrorRate { get; set; }
    [JsonPropertyName("upstream_error_rate")] public double UpstreamErrorRate { get; set; }
    [JsonPropertyName("upstream_error_count_excl_429_529")] public long UpstreamErrorCountExcl429529 { get; set; }
    [JsonPropertyName("upstream_429_count")] public long Upstream429Count { get; set; }
    [JsonPropertyName("upstream_529_count")] public long Upstream529Count { get; set; }
    public OpsRateSummaryDto Qps { get; set; } = new();
    public OpsRateSummaryDto Tps { get; set; } = new();
    public OpsPercentilesDto Duration { get; set; } = new();
    public OpsPercentilesDto Ttft { get; set; } = new();
    [JsonPropertyName("system_metrics")] public OpsSystemMetricsDto? SystemMetrics { get; set; }
    [JsonPropertyName("job_heartbeats")] public List<OpsJobHeartbeatDto> JobHeartbeats { get; set; } = [];
}

public sealed class OpsRateSummaryDto { public double Current { get; set; } public double Peak { get; set; } public double Avg { get; set; } }
public sealed class OpsPercentilesDto
{
    [JsonPropertyName("p50_ms")] public int? P50Ms { get; set; }
    [JsonPropertyName("p90_ms")] public int? P90Ms { get; set; }
    [JsonPropertyName("p95_ms")] public int? P95Ms { get; set; }
    [JsonPropertyName("p99_ms")] public int? P99Ms { get; set; }
    [JsonPropertyName("avg_ms")] public int? AvgMs { get; set; }
    [JsonPropertyName("max_ms")] public int? MaxMs { get; set; }
}
public sealed class OpsSystemMetricsDto
{
    public long Id { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("window_minutes")] public int WindowMinutes { get; set; }
    [JsonPropertyName("cpu_usage_percent")] public double? CpuUsagePercent { get; set; }
    [JsonPropertyName("memory_used_mb")] public double? MemoryUsedMb { get; set; }
    [JsonPropertyName("memory_total_mb")] public double? MemoryTotalMb { get; set; }
    [JsonPropertyName("memory_usage_percent")] public double? MemoryUsagePercent { get; set; }
    [JsonPropertyName("db_ok")] public bool? DbOk { get; set; }
    [JsonPropertyName("redis_ok")] public bool? RedisOk { get; set; }
    [JsonPropertyName("db_max_open_conns")] public int? DbMaxOpenConns { get; set; }
    [JsonPropertyName("redis_pool_size")] public int? RedisPoolSize { get; set; }
    [JsonPropertyName("redis_conn_total")] public int? RedisConnTotal { get; set; }
    [JsonPropertyName("redis_conn_idle")] public int? RedisConnIdle { get; set; }
    [JsonPropertyName("db_conn_active")] public int? DbConnActive { get; set; }
    [JsonPropertyName("db_conn_idle")] public int? DbConnIdle { get; set; }
    [JsonPropertyName("db_conn_waiting")] public int? DbConnWaiting { get; set; }
    [JsonPropertyName("goroutine_count")] public int? GoroutineCount { get; set; }
    [JsonPropertyName("concurrency_queue_depth")] public int? ConcurrencyQueueDepth { get; set; }
    [JsonPropertyName("account_switch_count")] public long? AccountSwitchCount { get; set; }
}
public sealed class OpsJobHeartbeatDto
{
    [JsonPropertyName("job_name")] public string JobName { get; set; } = string.Empty;
    [JsonPropertyName("last_run_at")] public DateTimeOffset? LastRunAt { get; set; }
    [JsonPropertyName("last_success_at")] public DateTimeOffset? LastSuccessAt { get; set; }
    [JsonPropertyName("last_error_at")] public DateTimeOffset? LastErrorAt { get; set; }
    [JsonPropertyName("last_error")] public string? LastError { get; set; }
    [JsonPropertyName("last_duration_ms")] public long? LastDurationMs { get; set; }
    [JsonPropertyName("last_result")] public string? LastResult { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}
public sealed class OpsThroughputTrendDto
{
    public string Bucket { get; set; } = string.Empty;
    public List<OpsThroughputPointDto> Points { get; set; } = [];
    [JsonPropertyName("by_platform")] public List<OpsBreakdownDto> ByPlatform { get; set; } = [];
    [JsonPropertyName("top_groups")] public List<OpsBreakdownDto> TopGroups { get; set; } = [];
}
public sealed class OpsThroughputPointDto
{
    [JsonPropertyName("bucket_start")] public DateTimeOffset? BucketStart { get; set; }
    [JsonPropertyName("request_count")] public long RequestCount { get; set; }
    [JsonPropertyName("token_consumed")] public long TokenConsumed { get; set; }
    [JsonPropertyName("switch_count")] public long SwitchCount { get; set; }
    public double Qps { get; set; }
    public double Tps { get; set; }
}
public sealed class OpsBreakdownDto
{
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    [JsonPropertyName("request_count")] public long RequestCount { get; set; }
    [JsonPropertyName("token_consumed")] public long TokenConsumed { get; set; }
}
public sealed class OpsErrorTrendDto
{
    public string Bucket { get; set; } = string.Empty;
    public List<OpsErrorPointDto> Points { get; set; } = [];
}
public sealed class OpsErrorPointDto
{
    [JsonPropertyName("bucket_start")] public DateTimeOffset? BucketStart { get; set; }
    [JsonPropertyName("error_count_total")] public long ErrorCountTotal { get; set; }
    [JsonPropertyName("business_limited_count")] public long BusinessLimitedCount { get; set; }
    [JsonPropertyName("error_count_sla")] public long ErrorCountSla { get; set; }
    [JsonPropertyName("upstream_error_count_excl_429_529")] public long UpstreamErrorCountExcl429529 { get; set; }
    [JsonPropertyName("upstream_429_count")] public long Upstream429Count { get; set; }
    [JsonPropertyName("upstream_529_count")] public long Upstream529Count { get; set; }
}

public sealed class OpsLatencyHistogramDto
{
    [JsonPropertyName("start_time")] public DateTimeOffset? StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTimeOffset? EndTime { get; set; }
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("total_requests")] public long TotalRequests { get; set; }
    [JsonPropertyName("duration_total_requests")] public long DurationTotalRequests { get; set; }
    [JsonPropertyName("duration_buckets")] public List<OpsLatencyBucketDto>? DurationBuckets { get; set; } = [];
    [JsonPropertyName("ttft_total_requests")] public long TtftTotalRequests { get; set; }
    [JsonPropertyName("ttft_buckets")] public List<OpsLatencyBucketDto>? TtftBuckets { get; set; } = [];
    // Older backend responses only contain total_requests/buckets. Keep those
    // values usable while the candidate and production services are upgraded.
    public List<OpsLatencyBucketDto>? Buckets { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<OpsLatencyBucketDto> EffectiveDurationBuckets => DurationBuckets is { Count: > 0 } ? DurationBuckets : Buckets ?? [];

    [JsonIgnore]
    public IReadOnlyList<OpsLatencyBucketDto> EffectiveTtftBuckets => TtftBuckets ?? [];

    [JsonIgnore]
    public long EffectiveDurationTotalRequests => DurationTotalRequests > 0 ? DurationTotalRequests : TotalRequests;
}

public sealed class OpsLatencyBucketDto
{
    public string Range { get; set; } = string.Empty;
    public long Count { get; set; }
}

public sealed class OpsErrorDistributionDto
{
    public long Total { get; set; }
    public List<OpsErrorDistributionItemDto> Items { get; set; } = [];
}

public sealed class OpsErrorDistributionItemDto
{
    [JsonPropertyName("status_code")] public int StatusCode { get; set; }
    public long Total { get; set; }
    public long Sla { get; set; }
    [JsonPropertyName("business_limited")] public long BusinessLimited { get; set; }
}

public class OpsPlatformConcurrencyDto
{
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("current_in_use")] public int CurrentInUse { get; set; }
    [JsonPropertyName("max_capacity")] public int MaxCapacity { get; set; }
    [JsonPropertyName("load_percentage")] public double LoadPercentage { get; set; }
    [JsonPropertyName("waiting_in_queue")] public int WaitingInQueue { get; set; }
}

public class OpsGroupConcurrencyDto : OpsPlatformConcurrencyDto
{
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
}

public sealed class OpsAccountConcurrencyDto : OpsGroupConcurrencyDto
{
    [JsonPropertyName("account_id")] public long AccountId { get; set; }
    [JsonPropertyName("account_name")] public string AccountName { get; set; } = string.Empty;
}

public sealed class OpsConcurrencyStatsDto
{
    public bool Enabled { get; set; } = true;
    public Dictionary<string, OpsPlatformConcurrencyDto> Platform { get; set; } = [];
    public Dictionary<string, OpsGroupConcurrencyDto> Group { get; set; } = [];
    public Dictionary<string, OpsAccountConcurrencyDto> Account { get; set; } = [];
    public DateTimeOffset? Timestamp { get; set; }
}

public sealed class OpsUserConcurrencyDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    [JsonPropertyName("current_in_use")] public int CurrentInUse { get; set; }
    [JsonPropertyName("max_capacity")] public int MaxCapacity { get; set; }
    [JsonPropertyName("load_percentage")] public double LoadPercentage { get; set; }
    [JsonPropertyName("waiting_in_queue")] public int WaitingInQueue { get; set; }
}

public sealed class OpsUserConcurrencyStatsDto
{
    public bool Enabled { get; set; } = true;
    public Dictionary<string, OpsUserConcurrencyDto> User { get; set; } = [];
    public DateTimeOffset? Timestamp { get; set; }
}

public class OpsPlatformAvailabilityDto
{
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("total_accounts")] public int TotalAccounts { get; set; }
    [JsonPropertyName("available_count")] public int AvailableCount { get; set; }
    [JsonPropertyName("rate_limit_count")] public int RateLimitCount { get; set; }
    [JsonPropertyName("error_count")] public int ErrorCount { get; set; }
}

public sealed class OpsGroupAvailabilityDto : OpsPlatformAvailabilityDto
{
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
}

public sealed class OpsAccountAvailabilityDto
{
    [JsonPropertyName("account_id")] public long AccountId { get; set; }
    [JsonPropertyName("account_name")] public string AccountName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("is_available")] public bool IsAvailable { get; set; }
    [JsonPropertyName("is_rate_limited")] public bool IsRateLimited { get; set; }
    [JsonPropertyName("rate_limit_remaining_sec")] public long? RateLimitRemainingSeconds { get; set; }
    [JsonPropertyName("is_overloaded")] public bool IsOverloaded { get; set; }
    [JsonPropertyName("overload_remaining_sec")] public long? OverloadRemainingSeconds { get; set; }
    [JsonPropertyName("has_error")] public bool HasError { get; set; }
    [JsonPropertyName("error_message")] public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class OpsAccountAvailabilityStatsDto
{
    public bool Enabled { get; set; } = true;
    public Dictionary<string, OpsPlatformAvailabilityDto> Platform { get; set; } = [];
    public Dictionary<string, OpsGroupAvailabilityDto> Group { get; set; } = [];
    public Dictionary<string, OpsAccountAvailabilityDto> Account { get; set; } = [];
    public DateTimeOffset? Timestamp { get; set; }
}

public sealed class OpsRealtimeTrafficDto
{
    public string Window { get; set; } = string.Empty;
    [JsonPropertyName("start_time")] public DateTimeOffset? StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTimeOffset? EndTime { get; set; }
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("account_id")] public long? AccountId { get; set; }
    public OpsRateSummaryDto Qps { get; set; } = new();
    public OpsRateSummaryDto Tps { get; set; } = new();
}

public sealed class OpsRealtimeTrafficResponseDto
{
    public bool Enabled { get; set; } = true;
    public OpsRealtimeTrafficDto? Summary { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
}

public sealed class OpsOpenAiTokenStatsItemDto
{
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("request_count")] public long RequestCount { get; set; }
    [JsonPropertyName("avg_tokens_per_sec")] public double? AverageTokensPerSecond { get; set; }
    [JsonPropertyName("avg_first_token_ms")] public double? AverageFirstTokenMs { get; set; }
    [JsonPropertyName("total_output_tokens")] public long TotalOutputTokens { get; set; }
    [JsonPropertyName("avg_duration_ms")] public double AverageDurationMs { get; set; }
    [JsonPropertyName("requests_with_first_token")] public long RequestsWithFirstToken { get; set; }
}

public sealed class OpsOpenAiTokenStatsDto
{
    [JsonPropertyName("time_range")] public string TimeRange { get; set; } = string.Empty;
    [JsonPropertyName("start_time")] public DateTimeOffset? StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTimeOffset? EndTime { get; set; }
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    public List<OpsOpenAiTokenStatsItemDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int? Page { get; set; }
    [JsonPropertyName("page_size")] public int? PageSize { get; set; }
    [JsonPropertyName("top_n")] public int? TopN { get; set; }
}

public sealed class OpsAlertRuleDto
{
    public long? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    [JsonPropertyName("metric_type")] public string MetricType { get; set; } = "error_rate";
    public string Operator { get; set; } = ">";
    public double Threshold { get; set; } = 5;
    [JsonPropertyName("window_minutes")] public int WindowMinutes { get; set; } = 5;
    [JsonPropertyName("sustained_minutes")] public int SustainedMinutes { get; set; } = 1;
    public string Severity { get; set; } = "P1";
    [JsonPropertyName("cooldown_minutes")] public int CooldownMinutes { get; set; } = 15;
    [JsonPropertyName("notify_email")] public bool NotifyEmail { get; set; }
    public Dictionary<string, JsonElement>? Filters { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("last_triggered_at")] public DateTimeOffset? LastTriggeredAt { get; set; }
}

public sealed class OpsAlertEventDto
{
    public long Id { get; set; }
    [JsonPropertyName("rule_id")] public long RuleId { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("metric_value")] public double? MetricValue { get; set; }
    [JsonPropertyName("threshold_value")] public double? ThresholdValue { get; set; }
    public Dictionary<string, JsonElement>? Dimensions { get; set; }
    [JsonPropertyName("fired_at")] public DateTimeOffset? FiredAt { get; set; }
    [JsonPropertyName("resolved_at")] public DateTimeOffset? ResolvedAt { get; set; }
    [JsonPropertyName("email_sent")] public bool EmailSent { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
}

public sealed class OpsMetricThresholdsDto
{
    [JsonPropertyName("sla_percent_min")] public double? SlaPercentMin { get; set; } = 99.5;
    [JsonPropertyName("ttft_p99_ms_max")] public double? TtftP99MsMax { get; set; } = 500;
    [JsonPropertyName("request_error_rate_percent_max")] public double? RequestErrorRatePercentMax { get; set; } = 5;
    [JsonPropertyName("upstream_error_rate_percent_max")] public double? UpstreamErrorRatePercentMax { get; set; } = 5;
}

public sealed class OpsDataRetentionSettingsDto
{
    [JsonPropertyName("cleanup_enabled")] public bool CleanupEnabled { get; set; }
    [JsonPropertyName("cleanup_schedule")] public string CleanupSchedule { get; set; } = string.Empty;
    [JsonPropertyName("error_log_retention_days")] public int ErrorLogRetentionDays { get; set; } = 30;
    [JsonPropertyName("minute_metrics_retention_days")] public int MinuteMetricsRetentionDays { get; set; } = 7;
    [JsonPropertyName("hourly_metrics_retention_days")] public int HourlyMetricsRetentionDays { get; set; } = 90;
}

public sealed class OpsAggregationSettingsDto
{
    [JsonPropertyName("aggregation_enabled")] public bool AggregationEnabled { get; set; }
}

public sealed class OpsQuotaAutoPauseSettingsDto
{
    [JsonPropertyName("default_threshold_5h")] public double DefaultThreshold5H { get; set; }
    [JsonPropertyName("default_threshold_7d")] public double DefaultThreshold7D { get; set; }
}

public sealed class OpsAdvancedSettingsDto
{
    [JsonPropertyName("data_retention")] public OpsDataRetentionSettingsDto DataRetention { get; set; } = new();
    public OpsAggregationSettingsDto Aggregation { get; set; } = new();
    [JsonPropertyName("openai_account_quota_auto_pause")] public OpsQuotaAutoPauseSettingsDto OpenAiAccountQuotaAutoPause { get; set; } = new();
    [JsonPropertyName("ignore_count_tokens_errors")] public bool IgnoreCountTokensErrors { get; set; }
    [JsonPropertyName("ignore_context_canceled")] public bool IgnoreContextCanceled { get; set; }
    [JsonPropertyName("ignore_no_available_accounts")] public bool IgnoreNoAvailableAccounts { get; set; }
    [JsonPropertyName("ignore_invalid_api_key_errors")] public bool IgnoreInvalidApiKeyErrors { get; set; }
    [JsonPropertyName("ignore_insufficient_balance_errors")] public bool IgnoreInsufficientBalanceErrors { get; set; }
    [JsonPropertyName("display_openai_token_stats")] public bool DisplayOpenAiTokenStats { get; set; }
    [JsonPropertyName("display_alert_events")] public bool DisplayAlertEvents { get; set; } = true;
    [JsonPropertyName("auto_refresh_enabled")] public bool AutoRefreshEnabled { get; set; }
    [JsonPropertyName("auto_refresh_interval_seconds")] public int AutoRefreshIntervalSeconds { get; set; } = 30;
}

public sealed class OpsRuntimeAlertSettingsDto
{
    [JsonPropertyName("evaluation_interval_seconds")] public int EvaluationIntervalSeconds { get; set; } = 60;
    [JsonPropertyName("distributed_lock")] public JsonElement? DistributedLock { get; set; }
    public JsonElement? Silencing { get; set; }
    public OpsMetricThresholdsDto Thresholds { get; set; } = new();
}

public sealed class OpsEmailNotificationConfigDto
{
    public OpsAlertEmailConfigDto Alert { get; set; } = new();
    public OpsReportEmailConfigDto Report { get; set; } = new();
}

public sealed class OpsAlertEmailConfigDto
{
    public bool Enabled { get; set; }
    public List<string> Recipients { get; set; } = [];
    [JsonPropertyName("min_severity")] public string MinSeverity { get; set; } = string.Empty;
    [JsonPropertyName("rate_limit_per_hour")] public int RateLimitPerHour { get; set; }
    [JsonPropertyName("batching_window_seconds")] public int BatchingWindowSeconds { get; set; }
    [JsonPropertyName("include_resolved_alerts")] public bool IncludeResolvedAlerts { get; set; }
}

public sealed class OpsReportEmailConfigDto
{
    public bool Enabled { get; set; }
    public List<string> Recipients { get; set; } = [];
    [JsonPropertyName("daily_summary_enabled")] public bool DailySummaryEnabled { get; set; }
    [JsonPropertyName("daily_summary_schedule")] public string DailySummarySchedule { get; set; } = string.Empty;
    [JsonPropertyName("weekly_summary_enabled")] public bool WeeklySummaryEnabled { get; set; }
    [JsonPropertyName("weekly_summary_schedule")] public string WeeklySummarySchedule { get; set; } = string.Empty;
    [JsonPropertyName("error_digest_enabled")] public bool ErrorDigestEnabled { get; set; }
    [JsonPropertyName("error_digest_schedule")] public string ErrorDigestSchedule { get; set; } = string.Empty;
    [JsonPropertyName("error_digest_min_count")] public int ErrorDigestMinCount { get; set; }
    [JsonPropertyName("account_health_enabled")] public bool AccountHealthEnabled { get; set; }
    [JsonPropertyName("account_health_schedule")] public string AccountHealthSchedule { get; set; } = string.Empty;
    [JsonPropertyName("account_health_error_rate_threshold")] public double AccountHealthErrorRateThreshold { get; set; }
}

public sealed class OpsRuntimeLogConfigDto
{
    public string Level { get; set; } = "info";
    [JsonPropertyName("enable_sampling")] public bool EnableSampling { get; set; }
    [JsonPropertyName("sampling_initial")] public int SamplingInitial { get; set; } = 100;
    [JsonPropertyName("sampling_thereafter")] public int SamplingThereafter { get; set; } = 100;
    public bool Caller { get; set; } = true;
    [JsonPropertyName("stacktrace_level")] public string StacktraceLevel { get; set; } = "error";
    [JsonPropertyName("retention_days")] public int RetentionDays { get; set; } = 30;
    public string Source { get; set; } = string.Empty;
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("updated_by_user_id")] public long? UpdatedByUserId { get; set; }
}

public sealed class OpsSystemLogDto
{
    public long Id { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    public string Host { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty;
    [JsonPropertyName("client_request_id")] public string ClientRequestId { get; set; } = string.Empty;
    [JsonPropertyName("user_id")] public long? UserId { get; set; }
    [JsonPropertyName("api_key_id")] public long? ApiKeyId { get; set; }
    [JsonPropertyName("account_id")] public long? AccountId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class OpsSystemLogSinkHealthDto
{
    [JsonPropertyName("queue_depth")] public long QueueDepth { get; set; }
    [JsonPropertyName("queue_capacity")] public long QueueCapacity { get; set; }
    [JsonPropertyName("dropped_count")] public long DroppedCount { get; set; }
    [JsonPropertyName("write_failed_count")] public long WriteFailedCount { get; set; }
    [JsonPropertyName("written_count")] public long WrittenCount { get; set; }
    [JsonPropertyName("avg_write_delay_ms")] public double AverageWriteDelayMs { get; set; }
    [JsonPropertyName("last_error")] public string LastError { get; set; } = string.Empty;
}

public sealed class OpsRequestDetailDto
{
    public string Kind { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("duration_ms")] public int? DurationMs { get; set; }
    [JsonPropertyName("first_token_ms")] public int? FirstTokenMs { get; set; }
    [JsonPropertyName("status_code")] public int? StatusCode { get; set; }
    [JsonPropertyName("error_id")] public long? ErrorId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("user_id")] public long? UserId { get; set; }
    [JsonPropertyName("api_key_id")] public long? ApiKeyId { get; set; }
    [JsonPropertyName("account_id")] public long? AccountId { get; set; }
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    public bool Stream { get; set; }
}

public class OpsErrorLogDto
{
    public long Id { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("error_owner")] public string ErrorOwner { get; set; } = string.Empty;
    [JsonPropertyName("error_source")] public string ErrorSource { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    [JsonPropertyName("status_code")] public int StatusCode { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool Resolved { get; set; }
    [JsonPropertyName("resolved_at")] public DateTimeOffset? ResolvedAt { get; set; }
    [JsonPropertyName("resolved_by_user_id")] public long? ResolvedByUserId { get; set; }
    [JsonPropertyName("client_request_id")] public string ClientRequestId { get; set; } = string.Empty;
    [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("user_id")] public long? UserId { get; set; }
    [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;
    [JsonPropertyName("api_key_id")] public long? ApiKeyId { get; set; }
    [JsonPropertyName("api_key_name")] public string ApiKeyName { get; set; } = string.Empty;
    [JsonPropertyName("api_key_deleted")] public bool ApiKeyDeleted { get; set; }
    [JsonPropertyName("account_id")] public long? AccountId { get; set; }
    [JsonPropertyName("account_name")] public string AccountName { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    [JsonPropertyName("requested_model")] public string RequestedModel { get; set; } = string.Empty;
    [JsonPropertyName("upstream_model")] public string UpstreamModel { get; set; } = string.Empty;
    [JsonPropertyName("client_ip")] public string ClientIp { get; set; } = string.Empty;
    [JsonPropertyName("request_path")] public string RequestPath { get; set; } = string.Empty;
    public bool Stream { get; set; }
    [JsonPropertyName("inbound_endpoint")] public string InboundEndpoint { get; set; } = string.Empty;
    [JsonPropertyName("upstream_endpoint")] public string UpstreamEndpoint { get; set; } = string.Empty;
    [JsonPropertyName("request_type")] public int? RequestType { get; set; }
    [JsonPropertyName("user_agent")] public string UserAgent { get; set; } = string.Empty;
}

public sealed class OpsErrorDetailDto : OpsErrorLogDto
{
    [JsonPropertyName("error_body")] public string ErrorBody { get; set; } = string.Empty;
    [JsonPropertyName("upstream_status_code")] public int? UpstreamStatusCode { get; set; }
    [JsonPropertyName("upstream_error_message")] public string UpstreamErrorMessage { get; set; } = string.Empty;
    [JsonPropertyName("upstream_error_detail")] public string UpstreamErrorDetail { get; set; } = string.Empty;
    [JsonPropertyName("upstream_errors")] public string UpstreamErrors { get; set; } = string.Empty;
    [JsonPropertyName("auth_latency_ms")] public int? AuthLatencyMs { get; set; }
    [JsonPropertyName("routing_latency_ms")] public int? RoutingLatencyMs { get; set; }
    [JsonPropertyName("upstream_latency_ms")] public int? UpstreamLatencyMs { get; set; }
    [JsonPropertyName("response_latency_ms")] public int? ResponseLatencyMs { get; set; }
    [JsonPropertyName("time_to_first_token_ms")] public int? TimeToFirstTokenMs { get; set; }
    [JsonPropertyName("is_business_limited")] public bool IsBusinessLimited { get; set; }
    [JsonPropertyName("api_key_prefix")] public string ApiKeyPrefix { get; set; } = string.Empty;
}

public sealed class OpsRequestDetailsQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string TimeRange { get; set; } = "1h";
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string Kind { get; set; } = "all";
    public string Platform { get; set; } = string.Empty;
    public long? GroupId { get; set; }
    public long? UserId { get; set; }
    public long? ApiKeyId { get; set; }
    public long? AccountId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Search { get; set; } = string.Empty;
    public int? MinDurationMs { get; set; }
    public int? MaxDurationMs { get; set; }
    public string Sort { get; set; } = "created_at_desc";
}

public sealed class OpsErrorListQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string TimeRange { get; set; } = "1h";
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string Platform { get; set; } = string.Empty;
    public long? GroupId { get; set; }
    public long? AccountId { get; set; }
    public long? UserId { get; set; }
    public long? ApiKeyId { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ErrorOwner { get; set; } = string.Empty;
    public string ErrorSource { get; set; } = string.Empty;
    public string Resolved { get; set; } = string.Empty;
    public string View { get; set; } = "errors";
    public string Search { get; set; } = string.Empty;
    public string StatusCodes { get; set; } = string.Empty;
    public bool StatusCodesOther { get; set; }
    public string SortBy { get; set; } = "created_at";
    public string SortOrder { get; set; } = "desc";
}

public sealed class OpsSystemLogQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string TimeRange { get; set; } = "1h";
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string Host { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string ClientRequestId { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public long? ApiKeyId { get; set; }
    public long? AccountId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Search { get; set; } = string.Empty;
}

public sealed class OpsAlertEventsQueryDto
{
    public int Limit { get; set; } = 20;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool? EmailSent { get; set; }
    public string TimeRange { get; set; } = string.Empty;
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public DateTimeOffset? BeforeFiredAt { get; set; }
    public long? BeforeId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public long? GroupId { get; set; }
}

public sealed class OpsAlertSilenceRequestDto
{
    [JsonPropertyName("rule_id")] public long RuleId { get; set; }
    public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("group_id")] public long? GroupId { get; set; }
    public string? Region { get; set; }
    public DateTimeOffset Until { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class DataManagementConfigDto
{
    [JsonPropertyName("source_mode")] public string SourceMode { get; set; } = "direct";
    [JsonPropertyName("backup_root")] public string BackupRoot { get; set; } = string.Empty;
    [JsonPropertyName("sqlite_path")] public string? SqlitePath { get; set; }
    [JsonPropertyName("retention_days")] public int RetentionDays { get; set; } = 14;
    [JsonPropertyName("keep_last")] public int KeepLast { get; set; } = 10;
    [JsonPropertyName("active_postgres_profile_id")] public string? ActivePostgresProfileId { get; set; }
    [JsonPropertyName("active_redis_profile_id")] public string? ActiveRedisProfileId { get; set; }
    [JsonPropertyName("active_s3_profile_id")] public string? ActiveS3ProfileId { get; set; }
    public DataManagementPostgresDto Postgres { get; set; } = new();
    public DataManagementRedisDto Redis { get; set; } = new();
    public DataManagementS3Dto S3 { get; set; } = new();
}
public sealed class DataManagementPostgresDto
{
    public string Host { get; set; } = string.Empty; public int Port { get; set; } = 5432; public string User { get; set; } = string.Empty;
    public string? Password { get; set; } [JsonPropertyName("password_configured")] public bool PasswordConfigured { get; set; }
    public string Database { get; set; } = string.Empty; [JsonPropertyName("ssl_mode")] public string SslMode { get; set; } = "prefer"; [JsonPropertyName("container_name")] public string ContainerName { get; set; } = string.Empty;
}
public sealed class DataManagementRedisDto
{
    public string Addr { get; set; } = string.Empty; public string Username { get; set; } = string.Empty; public string? Password { get; set; }
    [JsonPropertyName("password_configured")] public bool PasswordConfigured { get; set; } public int Db { get; set; } [JsonPropertyName("container_name")] public string ContainerName { get; set; } = string.Empty;
}
public sealed class DataManagementS3Dto
{
    public bool Enabled { get; set; } public string Endpoint { get; set; } = string.Empty; public string Region { get; set; } = "auto"; public string Bucket { get; set; } = string.Empty;
    [JsonPropertyName("access_key_id")] public string AccessKeyId { get; set; } = string.Empty; [JsonPropertyName("secret_access_key")] public string? SecretAccessKey { get; set; }
    [JsonPropertyName("secret_access_key_configured")] public bool SecretAccessKeyConfigured { get; set; } public string Prefix { get; set; } = "backups/";
    [JsonPropertyName("force_path_style")] public bool ForcePathStyle { get; set; } [JsonPropertyName("use_ssl")] public bool UseSsl { get; set; }
}
public sealed class DataManagementHealthDto
{
    public bool Enabled { get; set; } public string Reason { get; set; } = string.Empty; [JsonPropertyName("socket_path")] public string SocketPath { get; set; } = string.Empty;
    public DataManagementAgentDto? Agent { get; set; }
}
public sealed class DataManagementAgentDto { public string Status { get; set; } = string.Empty; public string Version { get; set; } = string.Empty; [JsonPropertyName("uptime_seconds")] public long UptimeSeconds { get; set; } }
public sealed class DataManagementSourceProfileDto
{
    [JsonPropertyName("source_type")] public string SourceType { get; set; } = string.Empty; [JsonPropertyName("profile_id")] public string ProfileId { get; set; } = string.Empty; public string Name { get; set; } = string.Empty;
    [JsonPropertyName("is_active")] public bool IsActive { get; set; } [JsonPropertyName("password_configured")] public bool PasswordConfigured { get; set; } public DataManagementSourceConfigDto Config { get; set; } = new();
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; } [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}
public sealed class DataManagementSourceConfigDto
{
    public string Host { get; set; } = string.Empty; public int Port { get; set; } = 5432; public string User { get; set; } = string.Empty; public string? Password { get; set; }
    public string Database { get; set; } = string.Empty; [JsonPropertyName("ssl_mode")] public string SslMode { get; set; } = "prefer";
    public string Addr { get; set; } = string.Empty; public string Username { get; set; } = string.Empty; public int Db { get; set; }
    [JsonPropertyName("container_name")] public string ContainerName { get; set; } = string.Empty;
}
public sealed class DataManagementS3ProfileDto
{
    [JsonPropertyName("profile_id")] public string ProfileId { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; [JsonPropertyName("is_active")] public bool IsActive { get; set; }
    public DataManagementS3Dto S3 { get; set; } = new(); [JsonPropertyName("secret_access_key_configured")] public bool SecretAccessKeyConfigured { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; } [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}
public sealed class DataManagementBackupJobDto
{
    [JsonPropertyName("job_id")] public string JobId { get; set; } = string.Empty; [JsonPropertyName("backup_type")] public string BackupType { get; set; } = string.Empty; public string Status { get; set; } = string.Empty;
    [JsonPropertyName("triggered_by")] public string TriggeredBy { get; set; } = string.Empty; [JsonPropertyName("idempotency_key")] public string IdempotencyKey { get; set; } = string.Empty;
    [JsonPropertyName("upload_to_s3")] public bool UploadToS3 { get; set; } [JsonPropertyName("s3_profile_id")] public string? S3ProfileId { get; set; }
    [JsonPropertyName("postgres_profile_id")] public string? PostgresProfileId { get; set; } [JsonPropertyName("redis_profile_id")] public string? RedisProfileId { get; set; }
    [JsonPropertyName("started_at")] public DateTimeOffset? StartedAt { get; set; } [JsonPropertyName("finished_at")] public DateTimeOffset? FinishedAt { get; set; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; } public DataManagementArtifactDto Artifact { get; set; } = new(); public DataManagementS3ObjectDto S3 { get; set; } = new();
}
public sealed class DataManagementArtifactDto { [JsonPropertyName("local_path")] public string LocalPath { get; set; } = string.Empty; [JsonPropertyName("size_bytes")] public long SizeBytes { get; set; } public string Sha256 { get; set; } = string.Empty; }
public sealed class DataManagementS3ObjectDto { public string Bucket { get; set; } = string.Empty; public string Key { get; set; } = string.Empty; public string ETag { get; set; } = string.Empty; }

public sealed class RiskControlConfigDto
{
    public bool Enabled { get; set; } public string Mode { get; set; } = "off"; [JsonPropertyName("base_url")] public string BaseUrl { get; set; } = "https://api.openai.com"; public string Model { get; set; } = "omni-moderation-latest";
    [JsonPropertyName("proxy_id")] public long? ProxyId { get; set; } [JsonPropertyName("api_key_configured")] public bool ApiKeyConfigured { get; set; } [JsonPropertyName("api_key_masked")] public string ApiKeyMasked { get; set; } = string.Empty;
    [JsonPropertyName("api_key_count")] public int ApiKeyCount { get; set; } [JsonPropertyName("api_key_masks")] public List<string> ApiKeyMasks { get; set; } = []; [JsonPropertyName("api_key_statuses")] public List<RiskApiKeyStatusDto> ApiKeyStatuses { get; set; } = [];
    [JsonPropertyName("timeout_ms")] public int TimeoutMs { get; set; } = 3000; [JsonPropertyName("sample_rate")] public int SampleRate { get; set; } = 100;
    [JsonPropertyName("all_groups")] public bool AllGroups { get; set; } = true; [JsonPropertyName("group_ids")] public List<long> GroupIds { get; set; } = []; [JsonPropertyName("record_non_hits")] public bool RecordNonHits { get; set; }
    public Dictionary<string, double> Thresholds { get; set; } = []; [JsonPropertyName("worker_count")] public int WorkerCount { get; set; } = 4; [JsonPropertyName("queue_size")] public int QueueSize { get; set; } = 32768;
    [JsonPropertyName("block_status")] public int BlockStatus { get; set; } = 403; [JsonPropertyName("block_message")] public string BlockMessage { get; set; } = "Request blocked by content policy.";
    [JsonPropertyName("email_on_hit")] public bool EmailOnHit { get; set; } [JsonPropertyName("auto_ban_enabled")] public bool AutoBanEnabled { get; set; } [JsonPropertyName("ban_threshold")] public int BanThreshold { get; set; } = 3;
    [JsonPropertyName("violation_window_hours")] public int ViolationWindowHours { get; set; } = 24; [JsonPropertyName("retry_count")] public int RetryCount { get; set; } = 1;
    [JsonPropertyName("hit_retention_days")] public int HitRetentionDays { get; set; } = 30; [JsonPropertyName("non_hit_retention_days")] public int NonHitRetentionDays { get; set; } = 1;
    [JsonPropertyName("pre_hash_check_enabled")] public bool PreHashCheckEnabled { get; set; } [JsonPropertyName("blocked_keywords")] public List<string> BlockedKeywords { get; set; } = [];
    [JsonPropertyName("keyword_blocking_mode")] public string KeywordBlockingMode { get; set; } = "keyword_only"; [JsonPropertyName("model_filter")] public RiskModelFilterDto ModelFilter { get; set; } = new();
    [JsonPropertyName("cyber_policy_exclude_from_ban_count")] public bool CyberPolicyExcludeFromBanCount { get; set; }

    public void NormalizeCollections()
    {
        ApiKeyMasks ??= [];
        ApiKeyStatuses ??= [];
        GroupIds ??= [];
        Thresholds ??= [];
        BlockedKeywords ??= [];
        ModelFilter ??= new();
        ModelFilter.NormalizeCollections();
    }
}
public sealed class RiskModelFilterDto
{
    public string Type { get; set; } = "all";
    public List<string> Models { get; set; } = [];

    public void NormalizeCollections() => Models ??= [];
}
public sealed class RiskApiKeyStatusDto
{
    public int Index { get; set; } [JsonPropertyName("key_hash")] public string KeyHash { get; set; } = string.Empty; public string Masked { get; set; } = string.Empty; public string Status { get; set; } = "unknown";
    [JsonPropertyName("failure_count")] public long FailureCount { get; set; } [JsonPropertyName("success_count")] public long SuccessCount { get; set; } [JsonPropertyName("last_error")] public string LastError { get; set; } = string.Empty;
    [JsonPropertyName("last_checked_at")] public DateTimeOffset? LastCheckedAt { get; set; } [JsonPropertyName("frozen_until")] public DateTimeOffset? FrozenUntil { get; set; }
    [JsonPropertyName("last_latency_ms")] public long LastLatencyMs { get; set; } [JsonPropertyName("last_http_status")] public int LastHttpStatus { get; set; } [JsonPropertyName("last_tested")] public bool LastTested { get; set; } public bool Configured { get; set; }
}
public sealed class RiskControlStatusDto
{
    public bool Enabled { get; set; } [JsonPropertyName("risk_control_enabled")] public bool RiskControlEnabled { get; set; } public string Mode { get; set; } = string.Empty;
    [JsonPropertyName("worker_count")] public int WorkerCount { get; set; } [JsonPropertyName("max_workers")] public int MaxWorkers { get; set; } [JsonPropertyName("active_workers")] public int ActiveWorkers { get; set; } [JsonPropertyName("idle_workers")] public int IdleWorkers { get; set; }
    [JsonPropertyName("queue_size")] public int QueueSize { get; set; } [JsonPropertyName("queue_length")] public int QueueLength { get; set; } [JsonPropertyName("queue_usage_percent")] public double QueueUsagePercent { get; set; }
    public long Enqueued { get; set; } public long Dropped { get; set; } public long Processed { get; set; } public long Errors { get; set; }
    [JsonPropertyName("pre_block_active")] public long PreBlockActive { get; set; } [JsonPropertyName("pre_block_checked")] public long PreBlockChecked { get; set; } [JsonPropertyName("pre_block_allowed")] public long PreBlockAllowed { get; set; }
    [JsonPropertyName("pre_block_blocked")] public long PreBlockBlocked { get; set; } [JsonPropertyName("pre_block_errors")] public long PreBlockErrors { get; set; } [JsonPropertyName("pre_block_avg_latency_ms")] public double PreBlockAvgLatencyMs { get; set; }
    [JsonPropertyName("pre_block_api_key_active")] public long PreBlockApiKeyActive { get; set; } [JsonPropertyName("pre_block_api_key_available_count")] public int PreBlockApiKeyAvailableCount { get; set; } [JsonPropertyName("pre_block_api_key_total_calls")] public long PreBlockApiKeyTotalCalls { get; set; }
    [JsonPropertyName("pre_block_api_key_loads")] public List<RiskApiKeyLoadDto> PreBlockApiKeyLoads { get; set; } = []; [JsonPropertyName("api_key_statuses")] public List<RiskApiKeyStatusDto> ApiKeyStatuses { get; set; } = [];
    [JsonPropertyName("flagged_hash_count")] public long FlaggedHashCount { get; set; } [JsonPropertyName("last_cleanup_at")] public DateTimeOffset? LastCleanupAt { get; set; }
    [JsonPropertyName("last_cleanup_deleted_hit")] public long LastCleanupDeletedHit { get; set; } [JsonPropertyName("last_cleanup_deleted_non_hit")] public long LastCleanupDeletedNonHit { get; set; }

    public void NormalizeCollections()
    {
        PreBlockApiKeyLoads ??= [];
        ApiKeyStatuses ??= [];
    }
}
public sealed class RiskApiKeyLoadDto
{
    public int Index { get; set; } [JsonPropertyName("key_hash")] public string KeyHash { get; set; } = string.Empty; public string Masked { get; set; } = string.Empty; public string Status { get; set; } = "unknown";
    public long Active { get; set; } public long Total { get; set; } public long Success { get; set; } public long Errors { get; set; } [JsonPropertyName("avg_latency_ms")] public double AvgLatencyMs { get; set; }
    [JsonPropertyName("last_latency_ms")] public long LastLatencyMs { get; set; } [JsonPropertyName("last_http_status")] public int LastHttpStatus { get; set; }
}
public sealed class RiskControlLogDto
{
    public long Id { get; set; } [JsonPropertyName("request_id")] public string RequestId { get; set; } = string.Empty; [JsonPropertyName("user_id")] public long? UserId { get; set; } [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;
    [JsonPropertyName("api_key_id")] public long? ApiKeyId { get; set; } [JsonPropertyName("api_key_name")] public string ApiKeyName { get; set; } = string.Empty; [JsonPropertyName("group_id")] public long? GroupId { get; set; } [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty; public string Provider { get; set; } = string.Empty; public string Model { get; set; } = string.Empty; public string Mode { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; public bool Flagged { get; set; } [JsonPropertyName("highest_category")] public string HighestCategory { get; set; } = string.Empty; [JsonPropertyName("highest_score")] public double HighestScore { get; set; }
    [JsonPropertyName("matched_keyword")] public string MatchedKeyword { get; set; } = string.Empty; [JsonPropertyName("category_scores")] public Dictionary<string, double> CategoryScores { get; set; } = []; [JsonPropertyName("threshold_snapshot")] public Dictionary<string, double> ThresholdSnapshot { get; set; } = [];
    [JsonPropertyName("input_excerpt")] public string InputExcerpt { get; set; } = string.Empty; [JsonPropertyName("upstream_latency_ms")] public long? UpstreamLatencyMs { get; set; } public string Error { get; set; } = string.Empty;
    [JsonPropertyName("violation_count")] public int ViolationCount { get; set; } [JsonPropertyName("auto_banned")] public bool AutoBanned { get; set; } [JsonPropertyName("email_sent")] public bool EmailSent { get; set; } [JsonPropertyName("user_status")] public string UserStatus { get; set; } = string.Empty;
    [JsonPropertyName("queue_delay_ms")] public long? QueueDelayMs { get; set; } [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
}
public sealed class RiskApiKeyTestResponseDto
{
    public List<RiskApiKeyStatusDto> Items { get; set; } = []; [JsonPropertyName("audit_result")] public RiskAuditResultDto? AuditResult { get; set; } [JsonPropertyName("image_count")] public int ImageCount { get; set; }
}
public sealed class RiskAuditResultDto
{
    public bool Flagged { get; set; } [JsonPropertyName("highest_category")] public string HighestCategory { get; set; } = string.Empty; [JsonPropertyName("highest_score")] public double HighestScore { get; set; }
    [JsonPropertyName("composite_score")] public double CompositeScore { get; set; } [JsonPropertyName("category_scores")] public Dictionary<string, double> CategoryScores { get; set; } = []; public Dictionary<string, double> Thresholds { get; set; } = [];
}
public sealed class RiskUnbanResponseDto { [JsonPropertyName("user_id")] public long UserId { get; set; } public string Status { get; set; } = string.Empty; }
public sealed class RiskDeleteHashResponseDto { [JsonPropertyName("input_hash")] public string InputHash { get; set; } = string.Empty; public bool Deleted { get; set; } }
public sealed class RiskClearHashesResponseDto { public long Deleted { get; set; } }

public sealed class AffiliateAdminEntryDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; } public string Email { get; set; } = string.Empty; public string Username { get; set; } = string.Empty; [JsonPropertyName("aff_code")] public string AffCode { get; set; } = string.Empty; [JsonPropertyName("aff_code_custom")] public bool AffCodeCustom { get; set; }
    [JsonPropertyName("aff_rebate_rate_percent")] public double? AffRebateRatePercent { get; set; } [JsonPropertyName("aff_count")] public int AffCount { get; set; }
}
public sealed class AffiliateUserLookupDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
public sealed class AffiliateInviteRecordDto
{
    [JsonPropertyName("inviter_id")] public long InviterId { get; set; } [JsonPropertyName("inviter_email")] public string InviterEmail { get; set; } = string.Empty; [JsonPropertyName("inviter_username")] public string InviterUsername { get; set; } = string.Empty;
    [JsonPropertyName("invitee_id")] public long InviteeId { get; set; } [JsonPropertyName("invitee_email")] public string InviteeEmail { get; set; } = string.Empty; [JsonPropertyName("invitee_username")] public string InviteeUsername { get; set; } = string.Empty; [JsonPropertyName("aff_code")] public string AffCode { get; set; } = string.Empty;
    [JsonPropertyName("total_rebate")] public decimal TotalRebate { get; set; } [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
}
public sealed class AffiliateRebateRecordDto
{
    [JsonPropertyName("order_id")] public long OrderId { get; set; } [JsonPropertyName("out_trade_no")] public string OutTradeNo { get; set; } = string.Empty; [JsonPropertyName("inviter_email")] public string InviterEmail { get; set; } = string.Empty; [JsonPropertyName("invitee_email")] public string InviteeEmail { get; set; } = string.Empty;
    [JsonPropertyName("order_amount")] public decimal OrderAmount { get; set; } [JsonPropertyName("pay_amount")] public decimal PayAmount { get; set; } [JsonPropertyName("rebate_amount")] public decimal RebateAmount { get; set; } [JsonPropertyName("payment_type")] public string PaymentType { get; set; } = string.Empty; [JsonPropertyName("order_status")] public string OrderStatus { get; set; } = string.Empty; [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
}
public sealed class AffiliateTransferRecordDto
{
    [JsonPropertyName("ledger_id")] public long LedgerId { get; set; } [JsonPropertyName("user_id")] public long UserId { get; set; } [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty; public string Username { get; set; } = string.Empty; public decimal Amount { get; set; }
    [JsonPropertyName("balance_after")] public decimal? BalanceAfter { get; set; } [JsonPropertyName("available_quota_after")] public decimal? AvailableQuotaAfter { get; set; } [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
}
public sealed class AffiliateUserOverviewDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; } public string Email { get; set; } = string.Empty; public string Username { get; set; } = string.Empty; [JsonPropertyName("aff_code")] public string AffCode { get; set; } = string.Empty; [JsonPropertyName("rebate_rate_percent")] public double RebateRatePercent { get; set; }
    [JsonPropertyName("invited_count")] public int InvitedCount { get; set; } [JsonPropertyName("rebated_invitee_count")] public int RebatedInviteeCount { get; set; } [JsonPropertyName("available_quota")] public decimal AvailableQuota { get; set; } [JsonPropertyName("history_quota")] public decimal HistoryQuota { get; set; }
}

public sealed class ChannelMonitorV2ConfigDto
{
    public int Version { get; set; } public bool Enabled { get; set; } [JsonPropertyName("refresh_interval_seconds")] public int RefreshIntervalSeconds { get; set; } = 300;
    public List<ChannelMonitorV2PlatformDto> Platforms { get; set; } = []; [JsonPropertyName("group_ids")] public List<long> GroupIds { get; set; } = []; [JsonPropertyName("health_thresholds")] public ChannelMonitorV2ThresholdsDto HealthThresholds { get; set; } = new();
    [JsonPropertyName("ignored_error_categories")] public List<string> IgnoredErrorCategories { get; set; } = []; [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ChannelMonitorDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("api_mode")] public string ApiMode { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    [JsonPropertyName("api_key_masked")] public string ApiKeyMasked { get; set; } = string.Empty;
    [JsonPropertyName("api_key_decrypt_failed")] public bool ApiKeyDecryptFailed { get; set; }
    [JsonPropertyName("primary_model")] public string PrimaryModel { get; set; } = string.Empty;
    [JsonPropertyName("extra_models")] public List<string> ExtraModels { get; set; } = [];
    [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    [JsonPropertyName("interval_seconds")] public int IntervalSeconds { get; set; }
    [JsonPropertyName("jitter_seconds")] public int JitterSeconds { get; set; }
    [JsonPropertyName("last_checked_at")] public DateTimeOffset? LastCheckedAt { get; set; }
    [JsonPropertyName("primary_status")] public string PrimaryStatus { get; set; } = string.Empty;
    [JsonPropertyName("primary_latency_ms")] public int? PrimaryLatencyMs { get; set; }
    [JsonPropertyName("availability_7d")] public double Availability7d { get; set; }
    [JsonPropertyName("extra_models_status")] public List<ChannelMonitorExtraModelStatusDto> ExtraModelsStatus { get; set; } = [];
    [JsonPropertyName("template_id")] public long? TemplateId { get; set; }
    [JsonPropertyName("extra_headers")] public Dictionary<string, string> ExtraHeaders { get; set; } = [];
    [JsonPropertyName("body_override_mode")] public string BodyOverrideMode { get; set; } = "off";
    [JsonPropertyName("body_override")] public JsonElement BodyOverride { get; set; }
    [JsonPropertyName("check_mode")] public string CheckMode { get; set; } = "probe";
    [JsonPropertyName("account_id")] public long? AccountId { get; set; }
    [JsonPropertyName("latest_quota")] public MonitorQuotaSnapshotDto? LatestQuota { get; set; }
}

public sealed class MonitorQuotaTierDto
{
    public string Window { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    [JsonPropertyName("used_percent")] public double UsedPercent { get; set; }
    public double? Used { get; set; }
    public double? Limit { get; set; }
    [JsonPropertyName("reset_at")] public string? ResetAt { get; set; }
}

public sealed class MonitorBalanceDto
{
    public string Currency { get; set; } = string.Empty;
    public double Balance { get; set; }
}

public sealed class MonitorQuotaSnapshotDto
{
    public string Source { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<MonitorQuotaTierDto> Tiers { get; set; } = [];
    public double? Balance { get; set; }
    public List<MonitorBalanceDto> Balances { get; set; } = [];
    public string Currency { get; set; } = string.Empty;
    [JsonPropertyName("plan_level")] public string PlanLevel { get; set; } = string.Empty;
    [JsonPropertyName("credential_invalid")] public bool CredentialInvalid { get; set; }
    public string Error { get; set; } = string.Empty;
    [JsonPropertyName("fetched_at")] public DateTimeOffset? FetchedAt { get; set; }
}
public sealed class ChannelMonitorExtraModelStatusDto
{
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("latency_ms")] public int? LatencyMs { get; set; }
}
public sealed class ChannelMonitorRunResultDto
{
    public List<ChannelMonitorCheckResultDto> Results { get; set; } = [];
}
public sealed class ChannelMonitorCheckResultDto
{
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("latency_ms")] public int? LatencyMs { get; set; }
    [JsonPropertyName("ping_latency_ms")] public int? PingLatencyMs { get; set; }
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("checked_at")] public DateTimeOffset? CheckedAt { get; set; }
    public MonitorQuotaSnapshotDto? Quota { get; set; }
}
public sealed class ChannelMonitorHistoryDto
{
    public List<ChannelMonitorHistoryItemDto> Items { get; set; } = [];
}
public sealed class ChannelMonitorHistoryItemDto
{
    public long Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("latency_ms")] public int? LatencyMs { get; set; }
    [JsonPropertyName("ping_latency_ms")] public int? PingLatencyMs { get; set; }
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("checked_at")] public DateTimeOffset? CheckedAt { get; set; }
    public MonitorQuotaSnapshotDto? Quota { get; set; }
}
public sealed class ChannelMonitorTemplateDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("api_mode")] public string ApiMode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("extra_headers")] public Dictionary<string, string> ExtraHeaders { get; set; } = [];
    [JsonPropertyName("body_override_mode")] public string BodyOverrideMode { get; set; } = "off";
    [JsonPropertyName("body_override")] public JsonElement BodyOverride { get; set; }
    [JsonPropertyName("associated_monitors")] public long AssociatedMonitors { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}
public sealed class ChannelMonitorAssociatedDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("api_mode")] public string ApiMode { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
public sealed class ChannelMonitorV2PlatformDto { public string Platform { get; set; } = string.Empty; public bool Enabled { get; set; } public List<string> Models { get; set; } = []; }
public sealed class ChannelMonitorV2ThresholdsDto
{
    [JsonPropertyName("minimum_sample")] public long MinimumSample { get; set; } = 50; [JsonPropertyName("warning_error_rate")] public double WarningErrorRate { get; set; } = .05; [JsonPropertyName("critical_error_rate")] public double CriticalErrorRate { get; set; } = .2;
    [JsonPropertyName("target_ttft_ms")] public long TargetTtftMs { get; set; } = 3000; [JsonPropertyName("warning_ttft_ms")] public long WarningTtftMs { get; set; } = 3000; [JsonPropertyName("critical_ttft_ms")] public long CriticalTtftMs { get; set; } = 10000;
    [JsonPropertyName("warning_cache_rate")] public double WarningCacheRate { get; set; } = 0; [JsonPropertyName("critical_cache_rate")] public double CriticalCacheRate { get; set; } = 0; [JsonPropertyName("error_weight")] public double ErrorWeight { get; set; } = .6; [JsonPropertyName("ttft_weight")] public double TtftWeight { get; set; } = .2; [JsonPropertyName("cache_weight")] public double CacheWeight { get; set; } = .2;
}
public sealed class ChannelMonitorV2FilterDto { public string Range { get; set; } = "90m"; public List<string> Platforms { get; set; } = []; public List<long> GroupIds { get; set; } = []; public List<string> Models { get; set; } = []; }
public sealed class ChannelMonitorV2LatencyDto { [JsonPropertyName("sample_count")] public long SampleCount { get; set; } [JsonPropertyName("p50_ms")] public long? P50Ms { get; set; } [JsonPropertyName("p90_ms")] public long? P90Ms { get; set; } [JsonPropertyName("p95_ms")] public long? P95Ms { get; set; } [JsonPropertyName("avg_ms")] public double? AvgMs { get; set; } }
public sealed class ChannelMonitorV2MetricDto
{
    [JsonPropertyName("success_requests")] public long SuccessRequests { get; set; } [JsonPropertyName("error_requests")] public long ErrorRequests { get; set; } [JsonPropertyName("request_count")] public long RequestCount { get; set; } [JsonPropertyName("input_tokens")] public long InputTokens { get; set; } [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; } [JsonPropertyName("cache_creation_tokens")] public long CacheCreationTokens { get; set; } [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; } [JsonPropertyName("token_count")] public long TokenCount { get; set; } public double Rpm { get; set; } public double Tpm { get; set; } [JsonPropertyName("error_rate")] public double ErrorRate { get; set; } [JsonPropertyName("success_rate")] public double SuccessRate { get; set; } [JsonPropertyName("cache_rate")] public double CacheRate { get; set; } [JsonPropertyName("cache_rate_numerator")] public long CacheRateNumerator { get; set; } [JsonPropertyName("cache_rate_denominator")] public long CacheRateDenominator { get; set; } [JsonPropertyName("upstream_affected_requests")] public long? UpstreamAffectedRequests { get; set; } [JsonPropertyName("upstream_attempt_count")] public long? UpstreamAttemptCount { get; set; } public ChannelMonitorV2LatencyDto Ttft { get; set; } = new(); public ChannelMonitorV2LatencyDto Duration { get; set; } = new();
}
public sealed class ChannelMonitorV2HealthDto { public string Overall { get; set; } = "unknown"; [JsonPropertyName("error_rate")] public string ErrorRate { get; set; } = "unknown"; public string Ttft { get; set; } = "unknown"; public string Cache { get; set; } = "unknown"; public double? Score { get; set; } [JsonPropertyName("error_rate_score")] public double? ErrorRateScore { get; set; } [JsonPropertyName("ttft_score")] public double? TtftScore { get; set; } [JsonPropertyName("cache_score")] public double? CacheScore { get; set; } [JsonPropertyName("minimum_sample")] public long MinimumSample { get; set; } public ChannelMonitorV2ThresholdsDto? Thresholds { get; set; } }
public sealed class ChannelMonitorV2CoverageDto { [JsonPropertyName("requested_start")] public DateTimeOffset? RequestedStart { get; set; } [JsonPropertyName("requested_end")] public DateTimeOffset? RequestedEnd { get; set; } [JsonPropertyName("coverage_start")] public DateTimeOffset? CoverageStart { get; set; } [JsonPropertyName("data_through")] public DateTimeOffset? DataThrough { get; set; } [JsonPropertyName("computed_at")] public DateTimeOffset? ComputedAt { get; set; } [JsonPropertyName("coverage_complete")] public bool CoverageComplete { get; set; } [JsonPropertyName("aggregation_lag_seconds")] public long AggregationLagSeconds { get; set; } [JsonPropertyName("bucket_seconds")] public int BucketSeconds { get; set; } [JsonPropertyName("bootstrap")] public ChannelMonitorV2BootstrapDto? Bootstrap { get; set; } }
public sealed class ChannelMonitorV2BootstrapDto { public bool Active { get; set; } [JsonPropertyName("progress_percent")] public int ProgressPercent { get; set; } [JsonPropertyName("covered_from")] public DateTimeOffset? CoveredFrom { get; set; } [JsonPropertyName("target_start")] public DateTimeOffset? TargetStart { get; set; } }
public sealed class ChannelMonitorV2SnapshotDto { public ChannelMonitorV2ConfigDto Config { get; set; } = new(); public ChannelMonitorV2CoverageDto Coverage { get; set; } = new(); public ChannelMonitorV2MetricDto Metrics { get; set; } = new(); public ChannelMonitorV2HealthDto Health { get; set; } = new(); public List<ChannelMonitorV2TrendDto> Trend { get; set; } = []; }
public sealed class ChannelMonitorV2TrendDto { [JsonPropertyName("bucket_start")] public DateTimeOffset? BucketStart { get; set; } public ChannelMonitorV2MetricDto Metrics { get; set; } = new(); public ChannelMonitorV2HealthDto Health { get; set; } = new(); }
public sealed class ChannelMonitorV2DimensionsDto { public List<ChannelMonitorV2DimensionDto> Platforms { get; set; } = []; public List<ChannelMonitorV2GroupDimensionDto> Groups { get; set; } = []; public List<ChannelMonitorV2DimensionDto> Models { get; set; } = []; }
public sealed class ChannelMonitorV2DimensionDto { public string Value { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public string? Platform { get; set; } [JsonPropertyName("request_count")] public long RequestCount { get; set; } }
public sealed class ChannelMonitorV2GroupDimensionDto { public long Id { get; set; } public string Name { get; set; } = string.Empty; public string? Platform { get; set; } [JsonPropertyName("request_count")] public long RequestCount { get; set; } }
public sealed class ChannelMonitorV2ModelRowDto { public string Platform { get; set; } = string.Empty; public string Model { get; set; } = string.Empty; public ChannelMonitorV2MetricDto Metrics { get; set; } = new(); public ChannelMonitorV2HealthDto Health { get; set; } = new(); }
public sealed class ChannelMonitorV2MatrixRowDto { public string Platform { get; set; } = string.Empty; [JsonPropertyName("group_id")] public long? GroupId { get; set; } [JsonPropertyName("group_name")] public string GroupName { get; set; } = string.Empty; public string Model { get; set; } = string.Empty; public ChannelMonitorV2MetricDto Metrics { get; set; } = new(); public ChannelMonitorV2HealthDto Health { get; set; } = new(); public List<ChannelMonitorV2TrendDto> Buckets { get; set; } = []; }
public sealed class ChannelMonitorV2MatrixDto { [JsonPropertyName("group_by")] public string GroupBy { get; set; } = "platform_group"; public ChannelMonitorV2CoverageDto Coverage { get; set; } = new(); public List<ChannelMonitorV2MatrixRowDto> Items { get; set; } = []; }
public sealed class ChannelMonitorV2ErrorRowDto { public string Category { get; set; } = string.Empty; public long Count { get; set; } public double Rate { get; set; } public bool Ignored { get; set; } public List<ChannelMonitorV2ErrorDetailDto> Details { get; set; } = []; }
public sealed class ChannelMonitorV2ErrorDetailDto { public string Platform { get; set; } = string.Empty; public string Model { get; set; } = string.Empty; [JsonPropertyName("error_type")] public string ErrorType { get; set; } = string.Empty; [JsonPropertyName("status_code")] public int StatusCode { get; set; } [JsonPropertyName("upstream_status_code")] public int UpstreamStatusCode { get; set; } public string Message { get; set; } = string.Empty; public long Count { get; set; } }
public sealed class ChannelMonitorV2UserRowDto { [JsonPropertyName("user_id")] public long? UserId { get; set; } public int Rank { get; set; } [JsonPropertyName("display_label")] public string DisplayLabel { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string Username { get; set; } = string.Empty; [JsonPropertyName("is_self")] public bool IsSelf { get; set; } [JsonPropertyName("can_drilldown")] public bool CanDrilldown { get; set; } public ChannelMonitorV2MetricDto Metrics { get; set; } = new(); }
public sealed class ChannelMonitorV2ListDto<T> { public ChannelMonitorV2CoverageDto Coverage { get; set; } = new(); public List<T> Items { get; set; } = []; }
