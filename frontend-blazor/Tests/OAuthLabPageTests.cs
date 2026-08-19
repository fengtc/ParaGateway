using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class OAuthLabPageTests
{
    [Fact]
    public void OAuthLabDoesNotExposeWorkerSimulationEndpoints()
    {
        var markup = File.ReadAllText(FindPage("OAuthLab.razor"));

        Assert.Contains("官方 Go 后端", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("StartOAuthLabAsync", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("auth.json", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProviderPageOnlyShowsOAuthLabEntryWhenTheWorkerEnablesIt()
    {
        var markup = File.ReadAllText(FindPage("Providers.razor"));

        Assert.Contains("href=\"/provider-oauth\"", markup, StringComparison.Ordinal);
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
