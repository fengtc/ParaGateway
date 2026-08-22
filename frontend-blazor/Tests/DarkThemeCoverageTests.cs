using System.Text.RegularExpressions;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class DarkThemeCoverageTests
{
    [Fact]
    public void AuthenticationAndSharedSurfacesUseThemeVariables()
    {
        var authLayout = Read("Layout", "AuthLayout.razor.css");
        var app = Read("wwwroot", "css", "app.css");

        Assert.Contains("background: var(--canvas);", authLayout, StringComparison.Ordinal);
        Assert.Matches(@"\.login-panel\s*\{[^}]*background:\s*var\(--surface\)", app);
        Assert.Matches(@"\.modal-dialog\s*\{[^}]*background:\s*var\(--surface\)", app);
        Assert.Matches(@"\.toast-item\s*\{[^}]*background:\s*var\(--surface\)", app);
        Assert.Contains("--surface-muted: #18263a;", app, StringComparison.Ordinal);
        Assert.Contains("--surface-alt: #162337;", app, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossComponentDarkRulesLiveInGlobalStylesheet()
    {
        var app = Read("wwwroot", "css", "app.css");

        Assert.Contains("html[data-theme=\"dark\"] .health-realtime-panel", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .overview-icon.green", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .dashboard-stat-icon.blue", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .supported-model-chip", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .announcement-popup > header", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .announcement-summary-strip > article.unread > span", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .overall-status.operational", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .monitor-status.degraded", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .monitor-v2-title > span", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .backup-status.success", app, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] .oauth-result", app, StringComparison.Ordinal);

        foreach (var file in IsolatedStyles())
        {
            var css = File.ReadAllText(file);
            Assert.DoesNotContain(":global(", css, StringComparison.Ordinal);
            Assert.DoesNotContain("data-theme", css, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void HardCodedWhiteBackgroundsAreLimitedToContentAndControlIndicators()
    {
        var allowedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine("wwwroot", "css", "app.css")] = 2, // shared switch thumb and TOTP QR
            [Path.Combine("Components", "AnnouncementBell.razor.css")] = 1, // unread dot
            [Path.Combine("Components", "EmailTemplateEditor.razor.css")] = 1, // light email fidelity preview
            [Path.Combine("Components", "NativeAccountCreateModal.razor.css")] = 1, // switch thumb
            [Path.Combine("Components", "SecuritySettings.razor.css")] = 2, // switch thumb and QR
            [Path.Combine("Pages", "AdminOps.razor.css")] = 1, // switch thumb
            [Path.Combine("Pages", "Providers.razor.css")] = 1 // switch thumb
        };

        var actual = SourceStyles()
            .Select(path => new
            {
                RelativePath = Path.GetRelativePath(Root, path),
                Count = Regex.Matches(File.ReadAllText(path), @"background(?:-color)?\s*:\s*(?:#fff(?:fff)?|white)\s*;", RegexOptions.IgnoreCase).Count
            })
            .Where(item => item.Count > 0)
            .ToDictionary(item => item.RelativePath, item => item.Count, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(allowedCounts.Count, actual.Count);
        foreach (var (path, count) in allowedCounts)
        {
            Assert.True(actual.TryGetValue(path, out var actualCount), $"Missing expected allowlisted white background in {path}.");
            Assert.Equal(count, actualCount);
        }
    }

    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static IEnumerable<string> IsolatedStyles() =>
        new[] { "Pages", "Components", "Layout" }
            .SelectMany(directory => Directory.GetFiles(Path.Combine(Root, directory), "*.razor.css", SearchOption.AllDirectories));

    private static IEnumerable<string> SourceStyles() =>
        IsolatedStyles().Append(Path.Combine(Root, "wwwroot", "css", "app.css"));

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([Root, .. segments])).ReplaceLineEndings("\n");
}
