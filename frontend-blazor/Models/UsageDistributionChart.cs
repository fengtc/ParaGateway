using System.Globalization;

namespace ParaGateway.Frontend.Models;

public sealed record UsageDistributionChartRow(
    string Label,
    string FullLabel,
    long Tokens,
    double Cost,
    double Value,
    double Share,
    bool IsOther,
    int ItemCount);

public static class UsageDistributionChartBuilder
{
    public const int DefaultItemLimit = 6;
    public const int DefaultLabelLimit = 24;

    public static IReadOnlyList<UsageDistributionChartRow> Build(
        IEnumerable<DistributionLegendRow>? rows,
        string? metric,
        string? otherLabel,
        int itemLimit = DefaultItemLimit,
        int labelLimit = DefaultLabelLimit)
    {
        itemLimit = Math.Max(1, itemLimit);
        labelLimit = Math.Max(8, labelLimit);
        var useCost = IsCostMetric(metric);
        var candidates = (rows ?? [])
            .Select(row => new Candidate(
                NormalizeLabel(row.Label),
                row.Tokens,
                row.Cost,
                useCost ? row.Cost : row.Tokens,
                false,
                1))
            .Where(row => row.Value > 0 && double.IsFinite(row.Value))
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.FullLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0) return [];

        var total = candidates.Sum(row => row.Value);
        if (total <= 0 || !double.IsFinite(total)) return [];

        var displayed = candidates.Take(itemLimit).ToList();
        var remaining = candidates.Skip(itemLimit).ToList();
        if (remaining.Count > 0)
        {
            var normalizedOtherLabel = NormalizeLabel(otherLabel, "其他");
            var fullOtherLabel = candidates.Any(row => string.Equals(row.FullLabel, normalizedOtherLabel, StringComparison.OrdinalIgnoreCase))
                ? $"{normalizedOtherLabel}（汇总）"
                : normalizedOtherLabel;
            displayed.Add(new Candidate(
                fullOtherLabel,
                remaining.Sum(row => row.Tokens),
                remaining.Sum(row => row.Cost),
                remaining.Sum(row => row.Value),
                true,
                remaining.Count));
        }

        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return displayed
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.IsOther)
            .ThenBy(row => row.FullLabel, StringComparer.OrdinalIgnoreCase)
            .Select(row => new UsageDistributionChartRow(
                CreateUniqueChartLabel(row.FullLabel, usedLabels, labelLimit),
                row.FullLabel,
                row.Tokens,
                row.Cost,
                row.Value,
                row.Value / total,
                row.IsOther,
                row.ItemCount))
            .ToList();
    }

    public static bool IsCostMetric(string? metric) =>
        string.Equals(metric, "actual_cost", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeLabel(string? value, string fallback = "未命名") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string CreateUniqueChartLabel(string value, ISet<string> usedLabels, int labelLimit)
    {
        var fullLabel = NormalizeLabel(value);
        var textElementIndexes = StringInfo.ParseCombiningCharacters(fullLabel);
        for (var index = 1; ; index++)
        {
            var suffix = index == 1 ? string.Empty : $" ({index})";
            var prefixLimit = Math.Max(4, labelLimit - suffix.Length);
            var prefix = textElementIndexes.Length <= prefixLimit
                ? fullLabel
                : $"{fullLabel[..textElementIndexes[prefixLimit - 3]]}...";
            var candidate = prefix + suffix;
            if (usedLabels.Add(candidate)) return candidate;
        }
    }

    private sealed record Candidate(
        string FullLabel,
        long Tokens,
        double Cost,
        double Value,
        bool IsOther,
        int ItemCount);
}
