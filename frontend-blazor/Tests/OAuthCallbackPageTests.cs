using System.Text.Json;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class OAuthCallbackPageTests
{
    [Fact]
    public void FullLocalhostCallbackPastedIntoCodeFieldIsNormalized()
    {
        var result = OAuthCallbackInputParser.Normalize(
            callbackUrl: null,
            code: "http://localhost:1455/auth/callback?code=sample-code%2Fpart&scope=openid+profile&state=fresh-state",
            state: "stale-state",
            sessionId: "pending-session");

        Assert.Equal("sample-code/part", result.Code);
        Assert.Equal("fresh-state", result.State);
        Assert.Equal("pending-session", result.SessionId);
    }

    [Fact]
    public void CallbackQueryOverridesStateAndSessionId()
    {
        var result = OAuthCallbackInputParser.Normalize(
            callbackUrl: "?code=query-code&state=query-state&session_id=query-session",
            code: string.Empty,
            state: "stale-state",
            sessionId: "stale-session");

        Assert.Equal("query-code", result.Code);
        Assert.Equal("query-state", result.State);
        Assert.Equal("query-session", result.SessionId);
    }

    [Fact]
    public void CallbackLikeCodeCannotBeSubmittedAsANakedCode()
    {
        var exception = Assert.Throws<FormatException>(() => OAuthCallbackInputParser.Normalize(
            callbackUrl: null,
            code: "http://localhost:1455/auth/callback",
            state: "state",
            sessionId: "session"));

        Assert.Contains("查询参数", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CallbackPageCoversOfficialUserOAuthRoutes()
    {
        var page = ReadSource("Pages", "AuthCallback.razor");

        Assert.Contains("@page \"/auth/oauth/callback\"", page, StringComparison.Ordinal);
        Assert.Contains("@page \"/auth/linuxdo/callback\"", page, StringComparison.Ordinal);
        Assert.Contains("@page \"/auth/wechat/callback\"", page, StringComparison.Ordinal);
        Assert.Contains("@page \"/auth/dingtalk/callback\"", page, StringComparison.Ordinal);
        Assert.Contains("@page \"/auth/oidc/callback\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoverableErrorsDoNotReplaceTheOAuthForm()
    {
        var page = ReadSource("Pages", "AuthCallback.razor");

        Assert.Contains("inline-alert danger", page, StringComparison.Ordinal);
        Assert.DoesNotContain("else if (!string.IsNullOrWhiteSpace(userError))", page, StringComparison.Ordinal);
        Assert.Contains("readonly=\"@registrationEmailReadOnly\"", page, StringComparison.Ordinal);
        Assert.Contains("授权码 code（也可粘贴完整回调 URL）", page, StringComparison.Ordinal);
        Assert.Contains("OAuthCallbackInputParser.Normalize", page, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingVerifyCodeUsesTheOAuthUnionResponse()
    {
        var api = ReadSource("Services", "ApiClient.cs");
        var page = ReadSource("Pages", "AuthCallback.razor");

        Assert.Contains("Task<OAuthCompletionDto> SendPendingOAuthVerifyCodeAsync", api, StringComparison.Ordinal);
        Assert.Contains("IsPendingOAuthContinuation(response)", page, StringComparison.Ordinal);
        Assert.Contains("await ApplyCompletionAsync(response)", page, StringComparison.Ordinal);
    }

    [Fact]
    public void OAuthUnionResponseDeserializesCountdownAndChoiceState()
    {
        const string json = """
            {
              "countdown": 60,
              "auth_result": "pending_session",
              "provider": "oidc",
              "intent": "login",
              "step": "choose_account_action_required",
              "resolved_email": "owner@example.com"
            }
            """;

        var result = JsonSerializer.Deserialize<OAuthCompletionDto>(json);

        Assert.NotNull(result);
        Assert.Equal(60, result.Countdown);
        Assert.Equal("pending_session", result.AuthResult);
        Assert.Equal("choose_account_action_required", result.Step);
        Assert.Equal("owner@example.com", result.ResolvedEmail);
    }

    [Fact]
    public void BindCompletionWithoutAccessTokenHasAnExplicitSuccessPath()
    {
        var page = ReadSource("Pages", "AuthCallback.razor");

        Assert.Contains("adoptionDecisionSubmitted: true", page, StringComparison.Ordinal);
        Assert.Contains("result.Intent, \"bind_current_user\"", page, StringComparison.Ordinal);
        Assert.Contains("IsExplicitBindCompletion(result, bindInitiatedProvider)", page, StringComparison.Ordinal);
        Assert.Contains("paragateway.oauth.binding_provider", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Auth.User is not null && IsProviderOAuthRegistrationProvider", page, StringComparison.Ordinal);
        Assert.Contains("第三方账户绑定成功", page, StringComparison.Ordinal);
        Assert.Contains("result.Redirect ?? \"/profile\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void WeChatBindingCarriesTheBrowserSpecificOAuthMode()
    {
        var page = ReadSource("Pages", "Account.razor");
        var script = ReadSource("wwwroot", "js", "paragateway.js");

        Assert.Contains("AddWeChatModeAsync(provider, start.AuthorizeUrl)", page, StringComparison.Ordinal);
        Assert.Contains("MicroMessenger", page, StringComparison.Ordinal);
        Assert.Contains("values[\"mode\"] = mode", page, StringComparison.Ordinal);
        Assert.Contains("getUserAgent", script, StringComparison.Ordinal);
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
