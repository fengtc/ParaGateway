using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class BrandingAndFirstRunPromptTests
{
    [Fact]
    public void UserFacingFrontendUsesOnlyParaGatewayBrandingAndHasNoTourHooks()
    {
        var projectRoot = FindProjectRoot();
        var sourceRoots = new[] { "Components", "Layout", "Models", "Pages", "Services", "wwwroot" };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".razor", ".cs", ".css", ".html", ".js", ".json", ".md", ".svg"
        };

        var source = string.Join('\n', sourceRoots
            .Select(path => Path.Combine(projectRoot, path))
            .Where(Directory.Exists)
            .SelectMany(path => Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}_framework{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .Select(File.ReadAllText));

        Assert.DoesNotContain("sub2api", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Para AI Coding Gateway", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-tour", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("driver.js", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onboarding", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FrontendCannotRequestOrRenderAdministratorCompliancePrompt()
    {
        var projectRoot = FindProjectRoot();
        var apiClient = File.ReadAllText(Path.Combine(projectRoot, "Services", "ApiClient.cs"));
        var app = File.ReadAllText(Path.Combine(projectRoot, "App.razor"));

        Assert.DoesNotContain("ComplianceRequired", apiClient, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAdminComplianceStatusAsync", apiClient, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptAdminComplianceAsync", apiClient, StringComparison.Ordinal);
        Assert.DoesNotContain("ADMIN_COMPLIANCE_ACK_REQUIRED", apiClient, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminComplianceGate", app, StringComparison.Ordinal);

        var repositoryRoot = Directory.GetParent(projectRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        Assert.False(File.Exists(Path.Combine(repositoryRoot, "Components", "AdminComplianceGate.razor")));
    }

    [Fact]
    public void AuthenticationPagesShowOnlyTheParaGatewayBrandName()
    {
        var projectRoot = FindProjectRoot();
        var authenticationPages = new[]
        {
            "Login.razor",
            "Register.razor",
            "ForgotPassword.razor",
            "ResetPassword.razor",
            "EmailVerify.razor"
        };

        foreach (var page in authenticationPages)
        {
            var source = File.ReadAllText(Path.Combine(projectRoot, "Pages", page));

            Assert.Contains("<strong>ParaGateway</strong>", source, StringComparison.Ordinal);
            Assert.DoesNotContain("<small>Gateway</small>", source, StringComparison.Ordinal);
        }
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ParaGateway.Frontend.csproj")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate ParaGateway frontend project root.");
    }
}
