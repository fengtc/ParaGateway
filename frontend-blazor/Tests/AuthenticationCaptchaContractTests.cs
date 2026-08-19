using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AuthenticationCaptchaContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void AuthenticationRequestsSerializeAllOfficialCaptchaProofFields()
    {
        var proof = new CaptchaProof
        {
            TurnstileToken = "turnstile-or-aliyun",
            TencentCaptchaTicket = "ticket-value",
            TencentCaptchaRandstr = "@rand-value"
        };

        var login = new LoginRequest { Email = "user@example.com", Password = "secret-value" };
        login.ApplyCaptcha(proof);
        var register = new RegisterRequest { Email = "user@example.com", Password = "password-1234", ConfirmPassword = "password-1234", DisplayName = "User" };
        register.ApplyCaptcha(proof);
        var forgot = new ForgotPasswordRequest { Email = "user@example.com" };
        forgot.ApplyCaptcha(proof);
        var verify = new SendVerifyCodeRequest { Email = "user@example.com" };
        verify.ApplyCaptcha(proof);

        foreach (var request in new object[] { login, register, forgot, verify })
        {
            var value = JsonSerializer.Serialize(request, Json);
            Assert.Contains("\"turnstile_token\":\"turnstile-or-aliyun\"", value, StringComparison.Ordinal);
            Assert.Contains("\"tencent_captcha_ticket\":\"ticket-value\"", value, StringComparison.Ordinal);
            Assert.Contains("\"tencent_captcha_randstr\":\"@rand-value\"", value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PublicSettingsExposeAllOfficialCaptchaConfigurationKeys()
    {
        const string source = """
        {
          "turnstile_enabled": true,
          "turnstile_site_key": "site-key",
          "tencent_captcha_enabled": true,
          "tencent_captcha_app_id": "app-id",
          "tencent_captcha_region": "intl",
          "aliyun_captcha_enabled": true,
          "aliyun_captcha_scene_id": "scene-id",
          "aliyun_captcha_prefix": "prefix",
          "aliyun_captcha_region": "sgp"
        }
        """;

        var settings = JsonSerializer.Deserialize<PublicSettingsDto>(source, Json)!;
        Assert.True(settings.TurnstileEnabled);
        Assert.Equal("site-key", settings.TurnstileSiteKey);
        Assert.Equal("app-id", settings.TencentCaptchaAppId);
        Assert.Equal("intl", settings.TencentCaptchaRegion);
        Assert.Equal("scene-id", settings.AliyunCaptchaSceneId);
        Assert.Equal("prefix", settings.AliyunCaptchaPrefix);
        Assert.Equal("sgp", settings.AliyunCaptchaRegion);
    }

    [Fact]
    public void LoginPageUsesPostOAuthStartAndPasskeyCaptchaProof()
    {
        var login = ReadSource("Pages", "Login.razor");
        var callback = ReadSource("Pages", "AuthCallback.razor");
        Assert.Contains("Api.StartOAuthLoginAsync", login, StringComparison.Ordinal);
        Assert.Contains("Api.BeginPasskeyLoginAsync(proof)", login, StringComparison.Ordinal);
        Assert.Contains("turnstile_token = proof?.TurnstileToken", callback, StringComparison.Ordinal);
        Assert.Contains("request.ApplyCaptcha", callback, StringComparison.Ordinal);
    }

    [Fact]
    public void TotpSetupRendersQrCodeAndLoginCompletesSecondFactor()
    {
        var security = ReadSource("Components", "SecuritySettings.razor");
        var login = ReadSource("Pages", "Login.razor");
        Assert.Contains("SvgQRCode", security, StringComparison.Ordinal);
        Assert.Contains("totpQrSvgDataUri", security, StringComparison.Ordinal);
        Assert.Contains("auth.Requires2FA", login, StringComparison.Ordinal);
        Assert.Contains("Auth.CompletePasswordTwoFactorAsync", login, StringComparison.Ordinal);
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
