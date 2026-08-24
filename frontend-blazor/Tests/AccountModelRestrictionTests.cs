using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AccountModelRestrictionTests
{
    [Theory]
    [InlineData("openai", "apikey", true)]
    [InlineData("anthropic", "apikey", true)]
    [InlineData("gemini", "apikey", true)]
    [InlineData("grok", "oauth", true)]
    [InlineData("openai", "oauth", true)]
    [InlineData("antigravity", "oauth", true)]
    [InlineData("anthropic", "oauth", false)]
    [InlineData("gemini", "oauth", false)]
    [InlineData("copilot", "oauth", true)]
    [InlineData("copilot", "apikey", true)]
    public void VisibilityMatchesOfficialAccountTypeRules(string platform, string type, bool expected)
    {
        Assert.Equal(expected, AccountModelRestrictions.ShouldShow(platform, type));
    }

    [Fact]
    public void WhitelistSerializesAsIdentityModelMapping()
    {
        var input = new AccountInput
        {
            Platform = "openai",
            Type = "apikey",
            ModelRestrictionMode = "whitelist",
            AllowedModels = ["gpt-5.4", " gpt-5.6-sol ", "gpt-5.4"]
        };

        var patch = AccountModelRestrictions.BuildCredentialPatch(input, includeEmpty: false);
        var mapping = Assert.IsType<Dictionary<string, string>>(patch!["model_mapping"]);

        Assert.Equal(2, mapping.Count);
        Assert.Equal("gpt-5.4", mapping["gpt-5.4"]);
        Assert.Equal("gpt-5.6-sol", mapping["gpt-5.6-sol"]);
    }

    [Fact]
    public void MappingModeSupportsOneTrailingWildcard()
    {
        var input = new AccountInput
        {
            Platform = "grok",
            Type = "oauth",
            ModelRestrictionMode = "mapping",
            ModelMappings = [new ModelMappingInput { From = "gpt-*", To = "grok-4.6" }]
        };

        Assert.Null(AccountModelRestrictions.Validate(input));
        var patch = AccountModelRestrictions.BuildCredentialPatch(input, includeEmpty: false);
        var mapping = Assert.IsType<Dictionary<string, string>>(patch!["model_mapping"]);
        Assert.Equal("grok-4.6", mapping["gpt-*"]);
    }

    [Fact]
    public void InvalidWildcardIsRejected()
    {
        var input = new AccountInput
        {
            Platform = "openai",
            Type = "apikey",
            ModelRestrictionMode = "mapping",
            ModelMappings = [new ModelMappingInput { From = "gpt-*-mini", To = "gpt-5.4" }]
        };

        Assert.Contains("通配符只能放在末尾", AccountModelRestrictions.Validate(input), StringComparison.Ordinal);
    }

    [Fact]
    public void EditLoadSplitsIdentityAndTranslatedMappingsAndCanClearExplicitly()
    {
        using var document = JsonDocument.Parse("""{"model_mapping":{"gpt-5.4":"gpt-5.4","claude-*":"gpt-5.6-sol"}}""");
        var credentials = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        var input = new AccountInput { Platform = "openai", Type = "oauth", IsEditing = true };

        AccountModelRestrictions.Load(input, credentials);

        Assert.Equal(["gpt-5.4"], input.AllowedModels);
        var mapping = Assert.Single(input.ModelMappings);
        Assert.Equal("claude-*", mapping.From);
        Assert.Equal("gpt-5.6-sol", mapping.To);

        input.AllowedModels.Clear();
        input.ModelMappings.Clear();
        var patch = AccountModelRestrictions.BuildCredentialPatch(input, includeEmpty: true);
        Assert.Empty(Assert.IsType<Dictionary<string, string>>(patch!["model_mapping"]));
    }

    [Fact]
    public void AntigravityOnlyOffersMappingMode()
    {
        Assert.False(AccountModelRestrictions.SupportsWhitelist("antigravity"));
        Assert.True(AccountModelRestrictions.ShouldShow("antigravity", "oauth"));
    }
}
