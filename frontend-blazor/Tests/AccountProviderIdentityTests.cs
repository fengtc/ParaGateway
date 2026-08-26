using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AccountProviderIdentityTests
{
    [Fact]
    public void CanonicalCopilotRequiresTheCompleteStoredIdentity()
    {
        var account = Account(" OpenAI ", " OAuth ", " GitHub_Copilot ");

        Assert.True(AccountProviderIdentity.IsCanonicalGitHubCopilot(account));
        Assert.False(AccountProviderIdentity.IsLegacyGitHubCopilot(account));

        account.Platform = "anthropic";
        Assert.False(AccountProviderIdentity.IsCanonicalGitHubCopilot(account));
        Assert.True(AccountProviderIdentity.IsLegacyGitHubCopilot(account));

        account.Platform = "openai";
        account.Type = "apikey";
        Assert.False(AccountProviderIdentity.IsCanonicalGitHubCopilot(account));
        Assert.True(AccountProviderIdentity.IsLegacyGitHubCopilot(account));
    }

    [Fact]
    public void LegacyPlatformIsDisplayOnlyAndExtraProfileIsIgnored()
    {
        var legacy = Account("copilot", "oauth", null);
        Assert.False(AccountProviderIdentity.IsCanonicalGitHubCopilot(legacy));
        Assert.True(AccountProviderIdentity.IsLegacyGitHubCopilot(legacy));

        var extraOnly = Account("openai", "oauth", null);
        extraOnly.Extra = new Dictionary<string, JsonElement>
        {
            ["oauth_profile"] = JsonSerializer.SerializeToElement("github_copilot")
        };

        Assert.Equal(string.Empty, AccountProviderIdentity.OAuthProfile(extraOnly));
        Assert.False(AccountProviderIdentity.IsCanonicalGitHubCopilot(extraOnly));
        Assert.False(AccountProviderIdentity.IsLegacyGitHubCopilot(extraOnly));
    }

    private static AccountDto Account(string platform, string type, string? profile)
    {
        var credentials = new Dictionary<string, JsonElement>();
        if (profile is not null)
            credentials["oauth_profile"] = JsonSerializer.SerializeToElement(profile);
        return new AccountDto { Platform = platform, Type = type, Credentials = credentials };
    }
}
