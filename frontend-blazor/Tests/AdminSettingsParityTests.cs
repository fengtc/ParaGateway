using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AdminSettingsParityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SettingsContractDeserializesOfficialSecurityUserAndNotificationFields()
    {
        const string source = """
        {
          "tencent_captcha_enabled":true,
          "tencent_captcha_region":"intl",
          "aliyun_captcha_enabled":false,
          "api_key_acl_trust_forwarded_ip":true,
          "forwarded_client_ip_headers":["CF-Connecting-IP","X-Forwarded-For"],
          "wechat_connect_enabled":true,
          "wechat_connect_open_enabled":true,
          "wechat_connect_open_app_id":"wx-open",
          "dingtalk_connect_enabled":true,
          "oidc_connect_discovery_url":"https://issuer/.well-known/openid-configuration",
          "oidc_connect_use_pkce":true,
          "default_subscriptions":[{"group_id":7,"validity_days":30}],
          "default_platform_quotas":{"openai":{"daily":10,"weekly":null,"monthly":100}},
          "force_email_on_third_party_signup":true,
          "balance_low_notify_enabled":true,
          "balance_low_notify_threshold":5.5,
          "subscription_expiry_notify_enabled":true,
          "account_quota_notify_enabled":true,
          "account_quota_notify_emails":[{"email":"admin@example.com","disabled":false,"verified":true}],
          "channel_monitor_show_quota":true,
          "account_scheduling_thresholds":{"openai":90,"anthropic":80,"grok":70,"kimi":60,"zhipu":50}
        }
        """;

        var settings = JsonSerializer.Deserialize<AdminSettingsDto>(source, Json)!;

        Assert.True(settings.TencentCaptchaEnabled);
        Assert.Equal("intl", settings.TencentCaptchaRegion);
        Assert.Equal(2, settings.ForwardedClientIpHeaders.Count);
        Assert.True(settings.WeChatConnectOpenEnabled);
        Assert.True(settings.DingTalkConnectEnabled);
        Assert.True(settings.OidcConnectUsePkce);
        Assert.Equal(7, settings.DefaultSubscriptions[0].GroupId);
        Assert.Equal(100m, settings.DefaultPlatformQuotas["openai"].Monthly);
        Assert.True(settings.ForceEmailOnThirdPartySignup);
        Assert.Equal(5.5m, settings.BalanceLowNotifyThreshold);
        Assert.True(settings.AccountQuotaNotifyEmails[0].Verified);
        Assert.True(settings.ChannelMonitorShowQuota);
        Assert.Equal(60, settings.AccountSchedulingThresholds["kimi"]);
        Assert.Equal(50, settings.AccountSchedulingThresholds["zhipu"]);
    }

    [Fact]
    public void V0178ChannelQuotaAndSchedulingThresholdControlsMatchOfficialContract()
    {
        var page = ReadSource("Pages", "AdminSettings.razor");
        var gateway = ReadSource("Components", "GatewayAdvancedSettings.razor");

        Assert.Contains("settings.ChannelMonitorMode == \"v1\"", page, StringComparison.Ordinal);
        Assert.Contains("用户端展示配额快照", page, StringComparison.Ordinal);
        Assert.Contains("[\"channel_monitor_show_quota\"] = settings.ChannelMonitorShowQuota", page, StringComparison.Ordinal);
        Assert.Contains("NormalizeSchedulingThresholds(settings.AccountSchedulingThresholds)", page, StringComparison.Ordinal);
        foreach (var platform in new[] { "openai", "anthropic", "grok", "kimi", "zhipu" })
        {
            Assert.Contains($"\"{platform}\"", gateway, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("\"deepseek\"", gateway, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityTabExposesAllOfficialCaptchaAndOAuthProvidersWithWriteOnlySecrets()
    {
        var security = ReadSource("Components", "SecurityAdvancedSettings.razor");
        var settings = ReadSource("Pages", "AdminSettings.razor");

        foreach (var text in new[] { "Cloudflare Turnstile", "腾讯云验证码", "阿里云验证码", "LinuxDo OAuth", "GitHub OAuth", "Google OAuth", "微信登录", "钉钉登录", "通用 OIDC" })
        {
            Assert.Contains(text, security, StringComparison.Ordinal);
        }

        foreach (var key in new[] { "tencent_captcha_cloud_secret_key", "aliyun_captcha_access_key_secret", "linuxdo_connect_client_secret", "dingtalk_connect_client_secret", "wechat_connect_open_app_secret", "github_oauth_client_secret", "google_oauth_client_secret", "oidc_connect_client_secret" })
        {
            Assert.Contains(key, settings, StringComparison.Ordinal);
        }

        Assert.Contains("foreach (var secret in SecretSettingKeys) payload.Remove(secret)", settings, StringComparison.Ordinal);
        Assert.Contains("AddSecretIfPresent", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void UserAndEmailTabsExposeSubscriptionsQuotasAndNotifications()
    {
        var page = ReadSource("Pages", "AdminSettings.razor");
        var users = ReadSource("Components", "UserDefaultsSettings.razor");
        var notifications = ReadSource("Components", "NotificationSettings.razor");

        Assert.Contains("<UserDefaultsSettings Settings=\"settings\" />", page, StringComparison.Ordinal);
        Assert.Contains("default_subscriptions", page, StringComparison.Ordinal);
        Assert.Contains("auth_source_default_{sourceId}_platform_quotas", page, StringComparison.Ordinal);
        foreach (var source in new[] { "email", "linuxdo", "oidc", "wechat", "github", "google", "dingtalk" })
        {
            Assert.Contains($"(\"{source}\"", users, StringComparison.Ordinal);
        }
        foreach (var platform in new[] { "anthropic", "openai", "gemini", "antigravity", "grok" })
        {
            Assert.Contains(platform, ReadSource("Components", "PlatformQuotaMatrix.razor"), StringComparison.Ordinal);
        }
        Assert.Contains("订阅到期提醒", notifications, StringComparison.Ordinal);
        Assert.Contains("余额不足提醒", notifications, StringComparison.Ordinal);
        Assert.Contains("上游账号配额提醒", notifications, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SecurityAdvancedSettings.razor.css", ".security-advanced-card > header h2")]
    [InlineData("UserDefaultsSettings.razor.css", ".user-defaults-card > header h2")]
    [InlineData("NotificationSettings.razor.css", ".notification-card > header h2")]
    public void NestedSettingsCardHeadingsMatchAdminTypography(string fileName, string selector)
    {
        var css = ReadSource("Components", fileName).ReplaceLineEndings("\n");
        var expected = $"{selector} {{\n    margin: 0;\n    color: var(--text);\n    font-size: .98rem;\n}}";

        Assert.Contains(expected, css, StringComparison.Ordinal);
    }

    [Fact]
    public void BackupPageUsesImageStorageAndPollsBackupAndRestoreStatus()
    {
        var page = ReadSource("Pages", "AdminBackup.razor");
        var css = ReadSource("Pages", "AdminBackup.razor.css");
        var api = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("异步生图对象存储", page, StringComparison.Ordinal);
        Assert.Contains("GetImageStorageConfigAsync", page, StringComparison.Ordinal);
        Assert.Contains("UpdateImageStorageConfigAsync", page, StringComparison.Ordinal);
        Assert.Contains("TestImageStorageAsync", page, StringComparison.Ordinal);
        Assert.Contains("manualExpireDays", page, StringComparison.Ordinal);
        Assert.Contains("PollActiveOperationsAsync", page, StringComparison.Ordinal);
        Assert.Contains("RestoreStatus", page, StringComparison.Ordinal);
        Assert.Contains("/admin/backups/image-storage", api, StringComparison.Ordinal);
        Assert.Contains(".backup-card > .form-grid", css, StringComparison.Ordinal);
        Assert.Contains("padding: 20px;", css, StringComparison.Ordinal);
        Assert.Contains(".backup-card > .section-footer-actions", css, StringComparison.Ordinal);
    }

    [Fact]
    public void DataManagementCompatibilityPageDoesNotExposeDeprecatedAgentControls()
    {
        var page = ReadSource("Pages", "AdminDataManagement.razor");
        var api = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("Navigation.NavigateTo(\"/admin/backups\", replace: true)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAdminDataManagementHealthAsync", page, StringComparison.Ordinal);
        foreach (var route in new[] { "/data-management/sources/", "/data-management/s3/profiles", "/data-management/s3/test", "/data-management/backups" })
        {
            Assert.Contains(route, api, StringComparison.Ordinal);
        }
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }
}
