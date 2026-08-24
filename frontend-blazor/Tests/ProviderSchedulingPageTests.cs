using System.ComponentModel.DataAnnotations;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ProviderSchedulingPageTests
{
    [Fact]
    public void OfficialAccountAndIndependentUpstreamPagesHaveSeparateRoutesAndResponsibilities()
    {
        var official = File.ReadAllText(SourcePath("Pages", "Providers.razor"));
        var upstream = File.ReadAllText(SourcePath("Pages", "UpstreamAccounts.razor"));
        var navigation = File.ReadAllText(SourcePath("Layout", "NavMenu.razor"));

        Assert.Contains("@page \"/admin/accounts\"", official, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/admin/upstream-accounts\"", official, StringComparison.Ordinal);
        Assert.DoesNotContain("account-weight", official, StringComparison.Ordinal);
        Assert.DoesNotContain("account-rpm-limit", official, StringComparison.Ordinal);
        Assert.DoesNotContain("account-circuit-threshold", official, StringComparison.Ordinal);
        Assert.DoesNotContain("account-upstream-auth", official, StringComparison.Ordinal);

        Assert.Contains("@page \"/admin/upstream-accounts\"", upstream, StringComparison.Ordinal);
        Assert.Contains("upstream-weight", upstream, StringComparison.Ordinal);
        Assert.Contains("upstream-rpm", upstream, StringComparison.Ordinal);
        Assert.Contains("upstream-breaker", upstream, StringComparison.Ordinal);
        Assert.Contains("upstream-wif-url", upstream, StringComparison.Ordinal);
        Assert.Contains("UsageWindowRows", upstream, StringComparison.Ordinal);

        Assert.Contains("href=\"/admin/accounts\"", navigation, StringComparison.Ordinal);
        Assert.Contains("账号管理", navigation, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin/upstream-accounts\"", navigation, StringComparison.Ordinal);
        Assert.DoesNotContain("兼容上游连接", navigation, StringComparison.Ordinal);
    }

    [Fact]
    public void IndependentUpstreamPolicyDefaultsAndValidationAreNotOnAccountInput()
    {
        var defaults = new UpstreamAccountInput();
        Assert.Equal(100, defaults.Weight);
        Assert.Equal(120, defaults.RpmLimit);
        Assert.Equal(3, defaults.CircuitBreakerThreshold);
        Assert.Equal(60, defaults.CircuitBreakerCooldownSeconds);

        var invalid = new UpstreamAccountInput
        {
            Name = "invalid",
            ProviderType = "openai",
            BaseUrl = "https://api.openai.com",
            AuthType = "api_key",
            ApiKey = "sk-test",
            Weight = 0,
            RpmLimit = 1_000_001,
            CircuitBreakerThreshold = 1_001,
            CircuitBreakerCooldownSeconds = 86_401
        };
        var validation = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), validation, validateAllProperties: true));
        Assert.Contains(validation, result => result.MemberNames.Contains(nameof(UpstreamAccountInput.Weight)));
        Assert.Contains(validation, result => result.MemberNames.Contains(nameof(UpstreamAccountInput.RpmLimit)));
        Assert.Contains(validation, result => result.MemberNames.Contains(nameof(UpstreamAccountInput.CircuitBreakerThreshold)));
        Assert.Contains(validation, result => result.MemberNames.Contains(nameof(UpstreamAccountInput.CircuitBreakerCooldownSeconds)));

        Assert.Null(typeof(AccountInput).GetProperty("Weight"));
        Assert.Null(typeof(AccountInput).GetProperty("RpmLimit"));
        Assert.Null(typeof(AccountInput).GetProperty("CircuitBreakerThreshold"));
        Assert.Null(typeof(AccountInput).GetProperty("UpstreamAuthType"));
    }

    private static string SourcePath(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine([root, .. segments]);
    }
}
