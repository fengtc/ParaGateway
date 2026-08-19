using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class SetupWizardTests
{
    [Fact]
    public void SetupWizardCoversOfficialInstallContract()
    {
        var page = ReadSource("Pages", "Setup.razor");
        var client = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("@page \"/setup\"", page, StringComparison.Ordinal);
        Assert.Contains("TestSetupDatabaseAsync", page, StringComparison.Ordinal);
        Assert.Contains("TestSetupRedisAsync", page, StringComparison.Ordinal);
        Assert.Contains("InstallSetupAsync", page, StringComparison.Ordinal);
        Assert.Contains("needs_setup", page, StringComparison.Ordinal);
        Assert.Contains("/setup/test-db", client, StringComparison.Ordinal);
        Assert.Contains("/setup/test-redis", client, StringComparison.Ordinal);
        Assert.Contains("/setup/install", client, StringComparison.Ordinal);
        Assert.Contains("dbname", ReadSource("Models", "Dtos.cs"), StringComparison.Ordinal);
        Assert.Contains("enable_tls", ReadSource("Models", "Dtos.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneProxyForwardsSetupMutationEndpoints()
    {
        var caddy = ReadStandalone("Caddyfile");
        var nginx = ReadStandalone("nginx.conf");

        foreach (var path in new[] { "/setup/status", "/setup/test-db", "/setup/test-redis", "/setup/install" })
        {
            Assert.Contains(path, caddy, StringComparison.Ordinal);
        }
        foreach (var endpoint in new[] { "status", "test-db", "test-redis", "install" })
            Assert.Contains(endpoint, nginx, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicToolRoutesRemainPublic()
    {
        var guard = ReadSource("Components", "RouteGuard.razor");
        Assert.Contains("typeof(Pages.KeyUsage)", guard, StringComparison.Ordinal);
        Assert.Contains("typeof(Pages.NotFound)", guard, StringComparison.Ordinal);
        Assert.DoesNotContain("typeof(Pages.KeyUsage)\n        || RouteData.PageType == typeof(Pages.CustomPage)", guard, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }

    private static string ReadStandalone(string name) => ReadSource("..", "..", "deploy", "standalone", name);
}
