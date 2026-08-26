using System.Globalization;
using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class OAuthSubscriptionDisplayTests
{
    [Theory]
    [InlineData("plus", "Plus", "plus")]
    [InlineData("chatgpt_pro", "Pro", "pro")]
    [InlineData("self_serve_business_usage_based", "Business", "business")]
    [InlineData("claude-max-20x", "Claude Max 20x", "max")]
    public void NormalizesKnownOAuthPlans(string rawPlan, string expectedLabel, string expectedTone)
    {
        var display = OAuthSubscriptionDisplay.From(Account("openai", rawPlan));

        Assert.NotNull(display);
        Assert.Equal(expectedLabel, display.PlanLabel);
        Assert.Equal(expectedTone, display.PlanTone);
    }

    [Fact]
    public void MatchesOAuthPlatformsCaseInsensitively()
    {
        var account = Account("OpenAI", "pro");
        account.Type = "OAuth";

        Assert.Equal("Pro", OAuthSubscriptionDisplay.From(account)?.PlanLabel);
    }

    [Fact]
    public void FormatsSubscriptionExpiryWithoutUsingTokenExpiry()
    {
        var account = Account("anthropic", "pro", "2026-09-04T23:30:00Z");
        account.Credentials!["expires_at"] = JsonSerializer.SerializeToElement("2026-01-01T00:00:00Z");

        var display = OAuthSubscriptionDisplay.From(account);

        Assert.NotNull(display);
        var expectedDate = DateTimeOffset.Parse(
            "2026-09-04T23:30:00Z",
            CultureInfo.InvariantCulture).ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal($"到期 {expectedDate}", display.ExpiryLabel);
        Assert.Equal("2026-09-04T23:30:00Z", display.ExpiryTitle);
    }

    [Fact]
    public void HidesExpiryForFreePlanAndInvalidDate()
    {
        Assert.Null(OAuthSubscriptionDisplay.From(Account("openai", "free", "2026-09-04T00:00:00Z"))!.ExpiryLabel);
        Assert.Null(OAuthSubscriptionDisplay.From(Account("openai", "xbasic", "2026-09-04T00:00:00Z"))!.ExpiryLabel);
        Assert.Null(OAuthSubscriptionDisplay.From(Account("anthropic", "pro", "not-a-date"))!.ExpiryLabel);
    }

    [Fact]
    public void DoesNotShowExpiryWithoutAPlan()
    {
        Assert.Null(OAuthSubscriptionDisplay.From(Account("openai", null, "2026-09-04T00:00:00Z")));
    }

    [Fact]
    public void PrefersAccountMetadataOverShadowParentFallback()
    {
        var account = Account("openai", "plus", "2026-09-04T00:00:00Z");
        account.ParentPlanType = "team";
        account.ParentSubscriptionExpiresAt = "2027-02-03T00:00:00Z";

        var display = OAuthSubscriptionDisplay.From(account);

        Assert.NotNull(display);
        Assert.Equal("Plus", display.PlanLabel);
        Assert.Equal("2026-09-04T00:00:00Z", display.ExpiryTitle);
    }

    [Fact]
    public void UsesShadowParentMetadataWhenCredentialsAreEmpty()
    {
        var account = Account("openai", null);
        account.ParentAccountId = 42;
        account.ParentPlanType = "team";
        account.ParentSubscriptionExpiresAt = "2027-02-03T00:00:00Z";

        var display = OAuthSubscriptionDisplay.From(account);

        Assert.NotNull(display);
        Assert.Equal("Team", display.PlanLabel);
        var expectedDate = DateTimeOffset.Parse(
            "2027-02-03T00:00:00Z",
            CultureInfo.InvariantCulture).ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal($"到期 {expectedDate}", display.ExpiryLabel);
    }

    [Theory]
    [InlineData("openai", "apikey")]
    [InlineData("anthropic", "setup-token")]
    [InlineData("gemini", "oauth")]
    public void IgnoresUnsupportedAccountKinds(string platform, string type)
    {
        var account = Account(platform, "pro");
        account.Type = type;

        Assert.Null(OAuthSubscriptionDisplay.From(account));
    }

    [Fact]
    public void ExcludesGitHubCopilotOAuthRows()
    {
        var account = Account("openai", "pro");
        account.Credentials!["oauth_profile"] = JsonSerializer.SerializeToElement("github_copilot");

        Assert.Null(OAuthSubscriptionDisplay.From(account));
    }

    [Fact]
    public void IgnoresGitHubCopilotProfileFromExtraMetadata()
    {
        var account = Account("openai", "pro");
        account.Extra = new Dictionary<string, JsonElement>
        {
            ["oauth_profile"] = JsonSerializer.SerializeToElement("github_copilot")
        };

        var display = OAuthSubscriptionDisplay.From(account);

        Assert.NotNull(display);
        Assert.Equal("Pro", display.PlanLabel);
    }

    [Fact]
    public void ExcludesExplicitCopilotPlatform()
    {
        Assert.Null(OAuthSubscriptionDisplay.From(Account("copilot", "pro")));
    }

    [Fact]
    public void MapsParentSubscriptionFieldsFromGoAccount()
    {
        var mapped = AccountDto.From(new GoAccount
        {
            Id = 7,
            Platform = "openai",
            Type = "oauth",
            ParentPlanType = "pro",
            ParentSubscriptionExpiresAt = "2026-09-04T00:00:00Z"
        });

        Assert.Equal("pro", mapped.ParentPlanType);
        Assert.Equal("2026-09-04T00:00:00Z", mapped.ParentSubscriptionExpiresAt);
    }

    private static AccountDto Account(string platform, string? plan, string? expiry = null)
    {
        var credentials = new Dictionary<string, JsonElement>();
        if (plan is not null)
        {
            credentials["plan_type"] = JsonSerializer.SerializeToElement(plan);
        }
        if (expiry is not null)
        {
            credentials["subscription_expires_at"] = JsonSerializer.SerializeToElement(expiry);
        }

        return new AccountDto
        {
            Id = "1",
            Platform = platform,
            Type = "oauth",
            Credentials = credentials
        };
    }
}
