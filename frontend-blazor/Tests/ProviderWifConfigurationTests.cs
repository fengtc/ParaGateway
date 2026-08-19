using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ProviderWifConfigurationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void IndependentUpstreamAuthenticationDefaultsRemainApiKeyCompatible()
    {
        var input = new UpstreamAccountInput();

        Assert.Equal("api_key", input.AuthType);
        Assert.Equal("client_secret_basic", input.WifClientAuthMethod);
        Assert.Equal("openai", input.ProviderType);
    }

    [Fact]
    public void WifInputSerializesOnlyTheIndependentSnakeCaseContract()
    {
        var input = CreateValidOpenAiWifInput();
        input.WifClientSecret = "client-secret";
        input.WifAudience = "https://api.openai.com";
        input.WifScope = "api.read api.write";

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(input, JsonOptions));
        var root = document.RootElement;

        Assert.Equal("wif", root.GetProperty("auth_type").GetString());
        Assert.Equal("openai", root.GetProperty("provider_type").GetString());
        Assert.Equal("https://issuer.example.com/oauth/token", root.GetProperty("wif_subject_token_url").GetString());
        Assert.Equal("client-id", root.GetProperty("wif_client_id").GetString());
        Assert.Equal("client-secret", root.GetProperty("wif_client_secret").GetString());
        Assert.Equal("client_secret_basic", root.GetProperty("wif_client_auth_method").GetString());
        Assert.Equal("idp_123", root.GetProperty("wif_identity_provider_id").GetString());
        Assert.Equal("sa_123", root.GetProperty("wif_service_account_id").GetString());
    }

    [Fact]
    public void NullSecretsAreOmittedFromIndependentRequests()
    {
        var json = JsonSerializer.Serialize(new UpstreamAccountInput(), JsonOptions);
        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.TryGetProperty("api_key", out _));
        Assert.False(document.RootElement.TryGetProperty("wif_client_secret", out _));
    }

    [Fact]
    public void OpenAiAndClaudeWifRequireTheirProviderSpecificIdentityFields()
    {
        var openAi = CreateValidOpenAiWifInput();
        Assert.Empty(Validate(openAi));
        openAi.WifIdentityProviderId = null;
        Assert.Contains(Validate(openAi), result => result.MemberNames.Contains(nameof(UpstreamAccountInput.WifIdentityProviderId)));

        var claude = new UpstreamAccountInput
        {
            Name = "Claude WIF",
            AuthType = "wif",
            ProviderType = "claude",
            BaseUrl = "https://api.anthropic.com",
            WifSubjectTokenUrl = "https://issuer.example.com/oauth/token",
            WifClientId = "client-id",
            WifClientSecret = "secret",
            WifClientAuthMethod = "client_secret_post",
            WifServiceAccountId = "sa_123"
        };

        var invalid = Validate(claude);
        Assert.Contains(invalid, result => result.MemberNames.Contains(nameof(UpstreamAccountInput.WifFederationRuleId)));
        Assert.Contains(invalid, result => result.MemberNames.Contains(nameof(UpstreamAccountInput.WifOrganizationId)));
        claude.WifFederationRuleId = "rule_123";
        claude.WifOrganizationId = "org_123";
        Assert.Empty(Validate(claude));
    }

    [Fact]
    public void WifControlsExistOnlyOnIndependentUpstreamPage()
    {
        var upstream = File.ReadAllText(FindPage("UpstreamAccounts.razor"));
        var official = File.ReadAllText(FindPage("Providers.razor"));

        Assert.Contains("upstream-wif-url", upstream, StringComparison.Ordinal);
        Assert.Contains("upstream-wif-secret", upstream, StringComparison.Ordinal);
        Assert.Contains("Identity Provider ID", upstream, StringComparison.Ordinal);
        Assert.Contains("Federation Rule ID", upstream, StringComparison.Ordinal);
        Assert.DoesNotContain("account-wif-token-url", official, StringComparison.Ordinal);
        Assert.DoesNotContain("account-wif-client-secret", official, StringComparison.Ordinal);
    }

    [Fact]
    public void EditingWifMayKeepSecretUntilAuthenticationBoundaryChanges()
    {
        var input = CreateValidOpenAiWifInput();
        input.IsEditing = true;
        input.WifClientSecret = null;
        input.OriginalProviderType = input.ProviderType;
        input.OriginalBaseUrl = input.BaseUrl;
        input.OriginalAuthType = input.AuthType;
        input.OriginalWifSubjectTokenUrl = input.WifSubjectTokenUrl!;
        input.OriginalWifClientId = input.WifClientId!;
        input.OriginalWifClientAuthMethod = input.WifClientAuthMethod;

        Assert.Empty(Validate(input));
        input.WifClientId = "replacement-client";
        Assert.Contains(Validate(input), result => result.MemberNames.Contains(nameof(UpstreamAccountInput.WifClientSecret)));
        input.WifClientSecret = "replacement-secret";
        Assert.Empty(Validate(input));
    }

    private static UpstreamAccountInput CreateValidOpenAiWifInput() => new()
    {
        Name = "OpenAI WIF",
        AuthType = "wif",
        ProviderType = "openai",
        BaseUrl = "https://api.openai.com",
        WifSubjectTokenUrl = "https://issuer.example.com/oauth/token",
        WifClientId = "client-id",
        WifClientSecret = "client-secret",
        WifIdentityProviderId = "idp_123",
        WifServiceAccountId = "sa_123"
    };

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }

    private static string FindPage(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Pages", fileName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate {fileName}.");
    }
}
