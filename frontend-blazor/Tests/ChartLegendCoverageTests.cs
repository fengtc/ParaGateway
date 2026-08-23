using System.Text.RegularExpressions;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ChartLegendCoverageTests
{
    private static readonly Regex ChartPattern = new(
        @"<Dx(?:Pie)?Chart\b[\s\S]*?</Dx(?:Pie)?Chart>",
        RegexOptions.CultureInvariant);

    [Fact]
    public void AllDevExpressChartsExplicitlyDisableCanvasLegends()
    {
        var root = FindFrontendRoot();
        var chartCount = 0;

        foreach (var file in Directory.EnumerateFiles(root.FullName, "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var matches = ChartPattern.Matches(source);
            for (var index = 0; index < matches.Count; index++)
            {
                chartCount++;
                Assert.Contains(
                    "<DxChartLegend Visible=\"false\" />",
                    matches[index].Value,
                    StringComparison.Ordinal);
            }
        }

        Assert.Equal(18, chartCount);
    }

    private static DirectoryInfo FindFrontendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ParaGateway.Frontend.csproj")))
                return directory;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the frontend project root.");
    }
}
