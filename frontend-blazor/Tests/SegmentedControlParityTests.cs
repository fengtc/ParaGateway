using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class SegmentedControlParityTests
{
    [Fact]
    public void SharedSegmentedControlsMatchManagementSecondaryButtons()
    {
        var css = Read("wwwroot", "css", "app.css");
        var button = Rule(css, ".button");
        var secondary = Rule(css, ".button.secondary");
        var segmentedButton = Rule(css, ".segmented-control button");

        Assert.Contains(".segmented-control {", css);
        foreach (var declaration in new[]
        {
            "min-height: 38px;",
            "padding: 7px 14px;",
            "gap: 7px;",
            "font-size: .82rem;",
            "font-weight: 650;",
            "line-height: 1.2;",
            "border-radius: 5px;"
        })
        {
            Assert.Contains(declaration, button, StringComparison.Ordinal);
            Assert.Contains(declaration, segmentedButton, StringComparison.Ordinal);
        }

        foreach (var declaration in new[]
        {
            "color: var(--ink);",
            "background: var(--surface);"
        })
        {
            Assert.Contains(declaration, secondary, StringComparison.Ordinal);
            Assert.Contains(declaration, segmentedButton, StringComparison.Ordinal);
        }

        Assert.Contains("border-color: var(--form-control-border);", secondary, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid var(--form-control-border);", segmentedButton, StringComparison.Ordinal);
        Assert.Contains(".segmented-control button:hover:not(:disabled)", css);
        Assert.Contains(".segmented-control button.active", css);
        Assert.Contains(".segmented-control.compact button", css);
        Assert.Contains("html[data-theme=\"dark\"] .segmented-control button.active", css);
    }

    [Fact]
    public void ChannelMonitorModeButtonsHaveManagementIconsAndTabState()
    {
        var markup = Read("Pages", "AdminChannelMonitor.razor");

        Assert.Contains("role=\"tablist\" aria-label=\"渠道监控模式\"", markup);
        Assert.Contains("role=\"tab\" aria-selected=\"@(!v2View)\"", markup);
        Assert.Contains("role=\"tab\" aria-selected=\"@v2View\"", markup);
        Assert.Contains("<Icon Name=\"activity\" Size=\"16\" /> V1 主动探测", markup);
        Assert.Contains("<Icon Name=\"database\" Size=\"16\" /> V2 被动聚合", markup);
    }

    [Fact]
    public void EverySharedSegmentedControlReceivesTheGlobalButtonStyle()
    {
        var sources = new[]
        {
            Read("Pages", "AdminChannelMonitor.razor"),
            Read("Pages", "AdminAffiliates.razor"),
            Read("Pages", "UserAnnouncements.razor"),
            Read("Components", "ChannelStatusV1Panel.razor"),
            Read("Components", "ChannelStatusV2Panel.razor"),
            Read("Components", "AdminDashboardPanel.razor")
        };

        Assert.All(sources, source => Assert.Contains("segmented-control", source));
    }

    [Fact]
    public void DashboardAndChannelMonitorDoNotOverrideTheSharedManagementButtonStyle()
    {
        var dashboardCss = Read("Components", "AdminDashboardPanel.razor.css");
        var monitorCss = Read("Components", "ChannelStatusV2Panel.razor.css");
        var monitorMarkup = Read("Components", "ChannelStatusV2Panel.razor");

        Assert.DoesNotContain(".segmented-control {", dashboardCss, StringComparison.Ordinal);
        Assert.DoesNotContain(".segmented-control button", dashboardCss, StringComparison.Ordinal);
        Assert.DoesNotContain(".segmented-control.compact", monitorCss, StringComparison.Ordinal);
        Assert.DoesNotContain("segmented-control compact", monitorMarkup, StringComparison.Ordinal);
        Assert.Contains("class=\"segmented-control\" role=\"group\" aria-label=\"时间范围\"", monitorMarkup, StringComparison.Ordinal);
        Assert.Contains("class=\"segmented-control\" role=\"group\" aria-label=\"趋势视图\"", monitorMarkup, StringComparison.Ordinal);
        Assert.Contains("class=\"segmented-control health-mode-tabs\" role=\"group\" aria-label=\"健康维度\"", monitorMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalStylesheetVersionChangesWithManagementTabRelease()
    {
        var index = Read("wwwroot", "index.html");

        Assert.Contains("css/app.css?v=20260820-official-nav-parity-a", index);
    }

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments])).ReplaceLineEndings("\n");
    }

    private static string Rule(string css, string selector)
    {
        var marker = "\n" + selector + " {";
        var start = css.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0 || css.StartsWith(selector + " {", StringComparison.Ordinal), $"CSS rule '{selector}' was not found.");
        var bodyStart = start >= 0 ? start + marker.Length : selector.Length + 2;
        var end = css.IndexOf('}', bodyStart);
        Assert.True(end > bodyStart, $"CSS rule '{selector}' is incomplete.");
        return css[bodyStart..end];
    }
}
