using System.Globalization;
using System.Text;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class UsageDistributionChartTests
{
    [Fact]
    public void TokenMetricAggregatesTheTailAndSortsDisplayedSharesAgain()
    {
        var source = new[]
        {
            Row("A", 40, 1),
            Row("B", 15, 2),
            Row("C", 10, 3),
            Row("D", 9, 4),
            Row("E", 8, 5),
            Row("F", 7, 6),
            Row("G", 6, 7),
            Row("H", 6, 8),
            Row("I", 6, 9)
        };

        var result = UsageDistributionChartBuilder.Build(source, "tokens", "其他模型");

        Assert.Equal(7, result.Count);
        Assert.Equal(new[] { "A", "其他模型", "B", "C", "D", "E", "F" }, result.Select(row => row.FullLabel));
        Assert.Equal(new double[] { 40, 18, 15, 10, 9, 8, 7 }, result.Select(row => row.Value));
        Assert.Equal(1d, result.Sum(row => row.Share), 10);
        Assert.True(result.Zip(result.Skip(1), (current, next) => current.Share >= next.Share).All(value => value));
        var other = Assert.Single(result, row => row.IsOther);
        Assert.Equal(3, other.ItemCount);
        Assert.Equal(18, other.Tokens);
    }

    [Fact]
    public void CostMetricUsesActualCostAndCanChangeTheOrder()
    {
        var source = new[]
        {
            Row("token-first", 100, 1),
            Row("cost-first", 50, 20),
            Row("cost-second", 25, 5)
        };

        var tokenRows = UsageDistributionChartBuilder.Build(source, "tokens", "其他");
        var costRows = UsageDistributionChartBuilder.Build(source, "actual_cost", "其他");

        Assert.Equal(new[] { "token-first", "cost-first", "cost-second" }, tokenRows.Select(row => row.FullLabel));
        Assert.Equal(new[] { "cost-first", "cost-second", "token-first" }, costRows.Select(row => row.FullLabel));
        Assert.Equal(20d / 26d, costRows[0].Share, 10);
        Assert.Equal(20d, costRows[0].Value);
    }

    [Fact]
    public void SelectedMetricFiltersNonPositiveAndNonFiniteValues()
    {
        var source = new[]
        {
            Row("tokens-only", 10, 0),
            Row("cost-only", 0, 5),
            Row("negative", -1, -2),
            Row("not-a-number", 0, double.NaN),
            Row("infinite", 0, double.PositiveInfinity)
        };

        Assert.Equal("tokens-only", Assert.Single(UsageDistributionChartBuilder.Build(source, "tokens", "其他")).FullLabel);
        Assert.Equal("cost-only", Assert.Single(UsageDistributionChartBuilder.Build(source, "actual_cost", "其他")).FullLabel);
        Assert.Empty(UsageDistributionChartBuilder.Build([Row("empty", 0, 0)], "tokens", "其他"));
    }

    [Fact]
    public void LabelsAreUnicodeSafeTruncatedAndUnique()
    {
        var fullLabel = string.Concat(Enumerable.Repeat("😀", 30));
        var result = UsageDistributionChartBuilder.Build(
            [Row(fullLabel, 20, 2), Row(fullLabel, 10, 1)],
            "tokens",
            "其他");

        Assert.Equal(2, result.Count);
        Assert.Equal(UsageDistributionChartBuilder.DefaultLabelLimit, StringInfo.ParseCombiningCharacters(result[0].Label).Length);
        Assert.Equal(UsageDistributionChartBuilder.DefaultLabelLimit, StringInfo.ParseCombiningCharacters(result[1].Label).Length);
        Assert.NotEqual(result[0].Label, result[1].Label);
        Assert.EndsWith(" (2)", result[1].Label, StringComparison.Ordinal);
        Assert.Equal(result[0].Label, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(result[0].Label)));
        Assert.Equal(result[1].Label, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(result[1].Label)));
        Assert.All(result, row => Assert.Equal(fullLabel, row.FullLabel));
    }

    private static DistributionLegendRow Row(string label, long tokens, double cost) => new(label, tokens, cost);
}
