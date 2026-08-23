using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AccountStatsModalParityTests
{
    [Fact]
    public void AccountStatsModalMatchesOfficialStructuredSections()
    {
        var markup = Read("Components", "AccountStatsModal.razor");
        var distribution = Read("Components", "AccountStatsDistributionPanel.razor");

        foreach (var text in new[]
        {
            "最近 30 天使用情况", "30 天总成本", "30 天总请求", "日均成本", "日均请求",
            "今日概览", "最高成本日", "最高请求日", "累计 Token", "平均响应时间",
            "使用趋势", "模型分布", "入站端点分布", "上游端点分布"
        })
        {
            Assert.Contains(text, markup, StringComparison.Ordinal);
        }

        Assert.Contains("GetAccountStatsAsync(accountId, 30)", markup, StringComparison.Ordinal);
        Assert.Contains("<DxChart ", markup, StringComparison.Ordinal);
        Assert.Contains("Axis=\"RequestAxis\"", markup, StringComparison.Ordinal);
        Assert.Contains("<DxPieChart ", distribution, StringComparison.Ordinal);
        Assert.Contains("用户计费", distribution, StringComparison.Ordinal);
        Assert.Contains("账号成本", distribution, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("json-preview", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountStatsModalSupportsResponsiveAndThemeSafeSurfaces()
    {
        var css = Read("Components", "AccountStatsModal.razor.css");
        var distributionCss = Read("Components", "AccountStatsDistributionPanel.razor.css");

        Assert.Contains("grid-template-columns: repeat(4", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 520px)", css, StringComparison.Ordinal);
        Assert.Contains("background: var(--surface)", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--ink)", css, StringComparison.Ordinal);
        Assert.Contains("overflow: auto", distributionCss, StringComparison.Ordinal);
        Assert.DoesNotContain("background: #fff", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("background: white", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccountStatsChartsKeepLegendsOutsideTheChartCanvas()
    {
        var markup = Read("Components", "AccountStatsModal.razor");
        var css = Read("Components", "AccountStatsModal.razor.css");
        var distribution = Read("Components", "AccountStatsDistributionPanel.razor");

        Assert.Contains("aria-label=\"账号使用趋势图例\"", markup, StringComparison.Ordinal);
        Assert.Contains("<DxChartLegend Visible=\"false\" />", markup, StringComparison.Ordinal);
        Assert.Contains("<DxChartLegend Visible=\"false\" />", distribution, StringComparison.Ordinal);
        Assert.Contains(".account-stats-chart-layout", css, StringComparison.Ordinal);
        Assert.Contains(".account-stats-chart-legend", css, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap;", css, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", css, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. parts]));
    }
}
