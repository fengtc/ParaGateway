using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ProfilePageParityTests
{
    [Fact]
    public void ProfilePageMatchesTheOfficialInformationArchitecture()
    {
        var page = ReadSource("Pages", "Account.razor");
        var css = ReadSource("Pages", "Account.razor.css");

        Assert.Contains("data-testid=\"profile-overview-hero\"", page, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"profile-basics-panel\"", page, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"profile-auth-bindings-panel\"", page, StringComparison.Ordinal);
        Assert.Contains("账户余额", page, StringComparison.Ordinal);
        Assert.Contains("并发限制", page, StringComparison.Ordinal);
        Assert.Contains("注册时间", page, StringComparison.Ordinal);
        Assert.Contains("资料与头像", page, StringComparison.Ordinal);
        Assert.Contains("登录方式绑定", page, StringComparison.Ordinal);
        Assert.Contains("资料来源", page, StringComparison.Ordinal);
        Assert.Contains("修改密码", page, StringComparison.Ordinal);
        Assert.Contains("密码至少需要 8 个字符", page, StringComparison.Ordinal);
        Assert.Contains("PublicSettings=\"publicSettings\"", page, StringComparison.Ordinal);
        Assert.Contains("max-width", css, StringComparison.Ordinal);
        Assert.Contains("950px", css, StringComparison.Ordinal);
        Assert.Contains("profile-metrics", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileAvatarAndIdentityBindingsUseTheOfficialBackendContracts()
    {
        var page = ReadSource("Pages", "Account.razor");
        var api = ReadSource("Services", "ApiClient.cs");
        var models = ReadSource("Models", "Dtos.cs");
        var javascript = ReadSource("wwwroot", "js", "paragateway.js");

        Assert.Contains("paraGateway.prepareAvatar", page, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"profile-avatar-file-input\"", page, StringComparison.Ordinal);
        Assert.Contains("AvatarUrl = avatarDraft", page, StringComparison.Ordinal);
        Assert.Contains("AvatarUrl = string.Empty", page, StringComparison.Ordinal);
        Assert.Contains("profile_sources", models, StringComparison.Ordinal);
        Assert.Contains("avatar_source", models, StringComparison.Ordinal);
        Assert.Contains("if (input.AvatarUrl is not null) payload[\"avatar_url\"]", api, StringComparison.Ordinal);
        Assert.Contains("avatarTargetBytes = 20 * 1024", javascript, StringComparison.Ordinal);
        Assert.Contains("image/webp", javascript, StringComparison.Ordinal);
        Assert.Contains("publicSettings.LinuxDoOAuthEnabled", page, StringComparison.Ordinal);
        Assert.Contains("publicSettings.DingTalkOAuthEnabled", page, StringComparison.Ordinal);
        Assert.Contains("publicSettings.OidcOAuthEnabled", page, StringComparison.Ordinal);
        Assert.Contains("publicSettings.WeChatOAuthEnabled", page, StringComparison.Ordinal);
    }

    [Fact]
    public void BalanceNotificationCardSupportsTheCompleteOfficialEmailFlow()
    {
        var security = ReadSource("Components", "SecuritySettings.razor");

        Assert.Contains("PublicSettings.BalanceLowNotifyEnabled", security, StringComparison.Ordinal);
        Assert.Contains("const int MaxNotifyEmails = 3", security, StringComparison.Ordinal);
        Assert.Contains("SaveBalanceThresholdAsync", security, StringComparison.Ordinal);
        Assert.Contains("pendingEmails", security, StringComparison.Ordinal);
        Assert.Contains("SendCodeForPendingEmailAsync", security, StringComparison.Ordinal);
        Assert.Contains("VerifyPendingEmailAsync", security, StringComparison.Ordinal);
        Assert.Contains("SendCodeForSavedEmailAsync", security, StringComparison.Ordinal);
        Assert.Contains("VerifySavedEmailAsync", security, StringComparison.Ordinal);
        Assert.Contains("ToggleNotifyEmailAsync", security, StringComparison.Ordinal);
        Assert.Contains("RemoveNotifyEmailAsync", security, StringComparison.Ordinal);
        Assert.Contains("Countdown = 60", security, StringComparison.Ordinal);
        Assert.Contains("@implements IDisposable", security, StringComparison.Ordinal);
    }

    [Fact]
    public void TotpAndPasskeyUseTheOfficialModalAndInlineFormFlows()
    {
        var security = ReadSource("Components", "SecuritySettings.razor");

        Assert.Contains("data-testid=\"totp-setup-modal\"", security, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"totp-disable-modal\"", security, StringComparison.Ordinal);
        Assert.Contains("totpSetupStep == 0", security, StringComparison.Ordinal);
        Assert.Contains("totpSetupStep == 1", security, StringComparison.Ordinal);
        Assert.Contains("totpSetupStep == 2", security, StringComparison.Ordinal);
        Assert.Contains("Api.GetTotpVerificationMethodAsync", security, StringComparison.Ordinal);
        Assert.Contains("Api.SendTotpVerifyCodeAsync", security, StringComparison.Ordinal);
        Assert.Contains("totpSetupCodeCooldown = 60", security, StringComparison.Ordinal);
        Assert.Contains("totpDisableCodeCooldown = 60", security, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"passkey-add-form\"", security, StringComparison.Ordinal);
        Assert.Contains("id=\"passkey-name\"", security, StringComparison.Ordinal);
        Assert.Contains("id=\"passkey-add-password\"", security, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"passkey-delete-modal\"", security, StringComparison.Ordinal);
        Assert.Contains("Api.BeginPasskeyRegistrationAsync(newPasskeyPassword)", security, StringComparison.Ordinal);
        Assert.Contains("Api.DeletePasskeyAsync(target.Id, passkeyDeletePassword)", security, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt\", \"请输入当前密码", security, StringComparison.Ordinal);
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
