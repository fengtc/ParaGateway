using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ProviderOAuthPageTests
{
    [Fact]
    public void OAuthPageUsesOfficialGoOAuthRoutes()
    {
        var markup = File.ReadAllText(FindPage("ProviderOAuth.razor"));

        Assert.Contains("官方 OAuth", markup, StringComparison.Ordinal);
        Assert.Contains("Api.StartOpenAIOAuthAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.StartAnthropicOAuthAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.StartGeminiOAuthAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.StartCopilotOAuthAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.PollCopilotOAuthAsync", markup, StringComparison.Ordinal);
        Assert.Contains("_ => await Api.StartOpenAIOAuthAsync()", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Api.StartOpenAIOAuthAsync(redirectUri)", markup, StringComparison.Ordinal);
        Assert.Contains("RedirectUri = platform == \"openai\" ? string.Empty : redirectUri", markup, StringComparison.Ordinal);
        Assert.Contains("填写 OpenAI 回调地址", markup, StringComparison.Ordinal);
        Assert.Contains("localhost:1455", markup, StringComparison.Ordinal);
        Assert.Contains("github_copilot", markup, StringComparison.Ordinal);
        Assert.Contains("selectedPlatform == \"copilot\" && copilotFlow", markup, StringComparison.Ordinal);
        Assert.Contains("PlatformButtonClass(\"openai\")", markup, StringComparison.Ordinal);
        Assert.Contains("PlatformButtonClass(\"anthropic\")", markup, StringComparison.Ordinal);
        Assert.Contains("PlatformButtonClass(\"copilot\")", markup, StringComparison.Ordinal);
        Assert.Contains("SelectPlatform(\"openai\")", markup, StringComparison.Ordinal);
        Assert.Contains("SelectPlatform(\"anthropic\")", markup, StringComparison.Ordinal);
        Assert.Contains("SelectPlatform(\"gemini\")", markup, StringComparison.Ordinal);
        Assert.Contains("SelectPlatform(\"antigravity\")", markup, StringComparison.Ordinal);
        Assert.Contains("SelectPlatform(\"grok\")", markup, StringComparison.Ordinal);
        Assert.Contains("SelectPlatform(\"copilot\")", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectCopilot", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick='() => StartAsync(", markup, StringComparison.Ordinal);
        Assert.Contains("id=\"official-oauth-account-name\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"oauth-account-name\"", markup, StringComparison.Ordinal);
        Assert.Contains("private string selectedPlatform = \"openai\";", markup, StringComparison.Ordinal);
        Assert.Contains("例如：{SelectedPlatformTitle} 主账号", markup, StringComparison.Ordinal);
        Assert.Contains("授权完成后，将使用该名称创建平台账号；切换平台不会共用该名称。", markup, StringComparison.Ordinal);
        Assert.Contains("Model=\"SelectedStart\"", markup, StringComparison.Ordinal);
        Assert.Contains("OnValidSubmit=\"StartCurrentPlatformAsync\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedStartModel", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedAccountName", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CopilotOAuthStartRequest", markup, StringComparison.Ordinal);
        Assert.Contains("@bind=\"SelectedStart.Name\"", markup, StringComparison.Ordinal);
        Assert.Contains("<ValidationMessage For=\"() => SelectedStart.Name\" />", markup, StringComparison.Ordinal);
        Assert.Contains("AccountName = startRequest.Name", markup, StringComparison.Ordinal);
        Assert.Contains("@key=\"selectedPlatform\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-platform=\"@selectedPlatform\"", markup, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(markup, "data-testid=\"oauth-account-name\""));
        Assert.Equal(1, CountOccurrences(markup, "@bind:event=\"oninput\""));
        Assert.Contains("private readonly Dictionary<string, OfficialOAuthStartRequest> officialStarts", markup, StringComparison.Ordinal);
        Assert.Contains("[\"openai\"] = new()", markup, StringComparison.Ordinal);
        Assert.Contains("[\"anthropic\"] = new()", markup, StringComparison.Ordinal);
        Assert.Contains("[\"gemini\"] = new()", markup, StringComparison.Ordinal);
        Assert.Contains("[\"antigravity\"] = new()", markup, StringComparison.Ordinal);
        Assert.Contains("[\"grok\"] = new()", markup, StringComparison.Ordinal);
        Assert.Contains("[\"copilot\"] = new()", markup, StringComparison.Ordinal);
        Assert.Contains("data-oauth-flow=\"device\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/provider-oauth#copilot-oauth\"", markup, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard.writeText", markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/accounts\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/providers\"", markup, StringComparison.Ordinal);
        var callbackMarkup = File.ReadAllText(FindPage("AuthCallback.razor"));
        Assert.Contains("Api.CreateAccountFromOAuthAsync", callbackMarkup, StringComparison.Ordinal);
        Assert.Contains("<option value=\"anthropic\">Claude / Anthropic</option>", callbackMarkup, StringComparison.Ordinal);
        Assert.Contains("Api.ExchangeAnthropicOAuthAsync(input)", callbackMarkup, StringComparison.Ordinal);
        Assert.Contains("Platform = form.Platform, Type = \"oauth\"", callbackMarkup, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/accounts\"", callbackMarkup, StringComparison.Ordinal);
        Assert.Contains("session_id", callbackMarkup, StringComparison.Ordinal);
        Assert.Contains("pending.AccountName", callbackMarkup, StringComparison.Ordinal);
        Assert.Contains("form.Name = !string.IsNullOrWhiteSpace(pending.AccountName)", callbackMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("auth.json", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProviderPageKeepsOAuthInsideTheNativeAccountFlow()
    {
        var markup = File.ReadAllText(FindPage("Providers.razor"));

        Assert.DoesNotContain("href=\"/provider-oauth\"", markup, StringComparison.Ordinal);
        Assert.Contains("<NativeAccountCreateModal", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OAuthPendingStatePreservesRequestedAccountName()
    {
        var pending = new OAuthPendingState
        {
            Platform = "anthropic",
            SessionId = "session-1",
            State = "state-1",
            RedirectUri = "https://localhost/auth/callback",
            AccountName = "Claude 主账号"
        };

        var restored = JsonSerializer.Deserialize<OAuthPendingState>(JsonSerializer.Serialize(pending));

        Assert.NotNull(restored);
        Assert.Equal("anthropic", restored.Platform);
        Assert.Equal("Claude 主账号", restored.AccountName);
    }

    [Fact]
    public void CopilotFlowContractReadsTheGoSnakeCaseResponse()
    {
        var flow = JsonSerializer.Deserialize<CopilotOAuthFlowDto>("""
        {
          "flow_id": "flow-1",
          "profile": "github_copilot",
          "status": "pending",
          "user_code": "ABCD-EFGH",
          "verification_uri": "https://github.com/login/device",
          "expires_at": "2026-08-16T03:00:00Z",
          "interval_seconds": 5,
          "next_poll_at": "2026-08-16T02:45:05Z",
          "provider_account_id": 42
        }
        """);

        Assert.NotNull(flow);
        Assert.Equal("flow-1", flow.FlowId);
        Assert.Equal("ABCD-EFGH", flow.UserCode);
        Assert.Equal("https://github.com/login/device", flow.VerificationUri);
        Assert.Equal(5, flow.IntervalSeconds);
        Assert.Equal(42, flow.ProviderAccountId);
    }

    private static string FindPage(string pageName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Pages", pageName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate the Blazor page {pageName}.");
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }

        return count;
    }
}
