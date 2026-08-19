using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class DashboardOpsTypographyTests
{
    [Fact]
    public void DashboardUsesUserManagementReadableTypography()
    {
        var css = Read("Components", "AdminDashboardPanel.razor.css");

        Assert.Contains(".admin-dashboard { font-size: 1rem; }", css);
        Assert.Contains(".admin-metric-content > span { font-size: .86rem; }", css);
        Assert.Contains(".admin-metric-content small { font-size: .8rem; }", css);
        Assert.Contains(".dashboard-card-heading h2 { font-size: 1rem; }", css);
        Assert.Contains(".quick-action strong { font-size: .88rem; }", css);
        Assert.Contains(".compact-dashboard-table { font-size: .88rem; }", css);
        Assert.Contains(".compact-dashboard-table th { font-size: .8rem; }", css);
        Assert.Contains(":deep(.dxbl-chart) { font-size: .8rem; }", css);
    }

    [Fact]
    public void RegularUserDashboardUsesTheSameReadableTypography()
    {
        var css = Read("Components", "UserDashboardPanel.razor.css");

        Assert.Contains(".user-dashboard { font-size: 1rem; }", css);
        Assert.Contains(".dashboard-stat-copy > span { font-size: .86rem; }", css);
        Assert.Contains(".dashboard-card-heading h2 { font-size: 1rem; }", css);
        Assert.Contains(".compact-dashboard-table { font-size: .88rem; }", css);
        Assert.Contains(".compact-dashboard-table th { font-size: .8rem; }", css);
        Assert.Contains(".usage-row-main strong { font-size: .86rem; }", css);
        Assert.Contains(".cost-pair em {\n    color: #89938e;\n    font-size: .76rem;", css);
        Assert.DoesNotContain("font-size: .72em;", css, StringComparison.Ordinal);
        Assert.Contains(":deep(.dxbl-chart) { font-size: .8rem; }", css);
    }

    [Fact]
    public void OperationsMonitoringUsesUserManagementReadableTypography()
    {
        var css = Read("Pages", "AdminOps.razor.css");

        Assert.Contains(".ops-dashboard { font-size: 1rem; }", css);
        Assert.Contains(".ops-title-block h2 { font-size: 1.35rem; }", css);
        Assert.Contains(".ops-ready-line,\n.ops-modal-heading { font-size: .85rem; }", css);
        Assert.Contains(".ops-table { font-size: .88rem; }", css);
        Assert.Contains(".ops-table th { font-size: .8rem; }", css);
        Assert.Contains(".card-header h3 { font-size: 1rem; }", css);
        Assert.Contains(".ops-pagination { font-size: .85rem; }", css);
        Assert.Contains(".form-grid label,\n.settings-tabs button { font-size: .85rem; }", css);
        Assert.Contains(":deep(.dxbl-chart) { font-size: .8rem; }", css);
    }

    [Fact]
    public void IsolatedStylesheetVersionChangesWithDashboardTypographyRelease()
    {
        var index = Read("wwwroot", "index.html");

        Assert.Contains("ParaGateway.Frontend.styles.css?v=20260819-dark-theme-audit", index);
    }

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments])).ReplaceLineEndings("\n");
    }
}
