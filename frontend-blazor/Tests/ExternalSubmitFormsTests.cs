using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using ParaGateway.Frontend.Models;

namespace ParaGateway.Frontend.Tests;

public sealed class ExternalSubmitFormsTests
{
    [Theory]
    [InlineData("Users.razor", "user-editor")]
    [InlineData("ApiKeys.razor", "key-editor")]
    [InlineData("UpstreamAccounts.razor", "upstream-account-editor")]
    public void ModalSubmitButtonTargetsExistingFormId(string pageName, string formId)
    {
        var markup = File.ReadAllText(FindPage(pageName));
        var formTags = Regex.Matches(markup, "<EditForm\\b[^>]*>", RegexOptions.CultureInvariant)
            .Select(match => match.Value);
        var buttonTags = Regex.Matches(markup, "<button\\b[^>]*>", RegexOptions.CultureInvariant)
            .Select(match => match.Value);

        Assert.Contains(formTags, tag => tag.Contains($"id=\"{formId}\"", StringComparison.Ordinal));
        Assert.Contains(
            buttonTags,
            tag => tag.Contains("type=\"submit\"", StringComparison.Ordinal)
                && tag.Contains($"form=\"{formId}\"", StringComparison.Ordinal));
    }

    [Fact]
    public void AccountEditorUsesDirectBlazorSaveCallback()
    {
        var markup = File.ReadAllText(FindPage("Providers.razor"));
        var buttonTags = Regex.Matches(markup, "<button\\b[^>]*>", RegexOptions.CultureInvariant)
            .Select(match => match.Value);

        Assert.Contains(buttonTags, tag => tag.Contains("type=\"button\"", StringComparison.Ordinal)
            && tag.Contains("@onclick=\"SubmitEditorAsync\"", StringComparison.Ordinal));
        Assert.DoesNotContain(buttonTags, tag => tag.Contains("form=\"account-editor\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ProviderRequestsOmitNullApiKey()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var updateJson = JsonSerializer.Serialize(new UpstreamAccountInput { ApiKey = null }, options);
        var testJson = JsonSerializer.Serialize(new UpstreamAccountInput { ApiKey = null }, options);

        AssertApiKeyIsOmitted(updateJson, "api_key");
        AssertApiKeyIsOmitted(testJson, "api_key");
    }

    private static void AssertApiKeyIsOmitted(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(propertyName, out _));
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
}
