using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParaGateway.Frontend.Models;

public static partial class ChannelTimePricingRules
{
    public const string DefaultTimezone = "Asia/Shanghai";

    public static string? ValidateModelPricingJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return "模型定价必须是有效的 JSON 数组。";
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return "模型定价必须是 JSON 数组。";

            var entryIndex = 0;
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                entryIndex++;
                if (entry.ValueKind != JsonValueKind.Object)
                    return $"模型定价第 {entryIndex} 项必须是 JSON 对象。";
                if (!entry.TryGetProperty("time_pricing", out var timePricing)
                    || timePricing.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;
                if (timePricing.ValueKind != JsonValueKind.Object)
                    return $"模型定价第 {entryIndex} 项的 time_pricing 必须是对象。";
                if (!timePricing.TryGetProperty("periods", out var periods)
                    || periods.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;
                if (periods.ValueKind != JsonValueKind.Array)
                    return $"模型定价第 {entryIndex} 项的 periods 必须是数组。";
                if (periods.GetArrayLength() == 0) continue;

                var billingMode = entry.TryGetProperty("billing_mode", out var mode)
                    && mode.ValueKind == JsonValueKind.String
                    ? mode.GetString()
                    : "token";
                if (!string.Equals(billingMode, "token", StringComparison.OrdinalIgnoreCase))
                    return $"模型定价第 {entryIndex} 项只有 Token 计费模式可以配置分时倍率。";

                if (!timePricing.TryGetProperty("timezone", out var timezoneNode)
                    || timezoneNode.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(timezoneNode.GetString()))
                    return $"模型定价第 {entryIndex} 项必须填写有效的 IANA 时区。";
                var timezone = timezoneNode.GetString()!.Trim();
                if (!IsValidTimezone(timezone))
                    return $"模型定价第 {entryIndex} 项的时区“{timezone}”无效。";

                var parsedPeriods = new List<(int Start, int End)>();
                var periodIndex = 0;
                foreach (var period in periods.EnumerateArray())
                {
                    periodIndex++;
                    if (period.ValueKind != JsonValueKind.Object)
                        return $"模型定价第 {entryIndex} 项的第 {periodIndex} 个时段必须是对象。";
                    if (!TryReadClock(period, "start_time", false, out var start)
                        || !TryReadClock(period, "end_time", true, out var end))
                        return $"模型定价第 {entryIndex} 项的第 {periodIndex} 个时段必须使用 HH:mm 或 HH:mm:ss 格式。";
                    if (start >= end)
                        return $"模型定价第 {entryIndex} 项的第 {periodIndex} 个时段开始时间必须早于结束时间。";
                    if (!period.TryGetProperty("multiplier", out var multiplierNode)
                        || multiplierNode.ValueKind != JsonValueKind.Number
                        || !multiplierNode.TryGetDouble(out var multiplier)
                        || !double.IsFinite(multiplier)
                        || multiplier < 0.01
                        || Math.Abs(multiplier * 100 - Math.Round(multiplier * 100)) > 1e-9)
                        return $"模型定价第 {entryIndex} 项的第 {periodIndex} 个时段倍率必须不小于 0.01，且最多保留两位小数。";

                    parsedPeriods.Add((start, end));
                }

                parsedPeriods.Sort((left, right) => left.Start.CompareTo(right.Start));
                for (var index = 1; index < parsedPeriods.Count; index++)
                {
                    if (parsedPeriods[index].Start < parsedPeriods[index - 1].End)
                        return $"模型定价第 {entryIndex} 项的分时区间不能重叠。";
                }
            }
        }

        return null;
    }

    public static bool IsValidTimezone(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone)
            || string.Equals(timezone.Trim(), "Local", StringComparison.Ordinal))
            return false;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timezone.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    public static string NormalizeClockTime(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Replace('：', ':');
        if (normalized == "24:00:00") return "00:00:00";
        return LegacyClockRegex().IsMatch(normalized) ? normalized + ":00" : normalized;
    }

    public static bool IsDraftPeriodIncomplete(string? startTime, string? endTime, string? multiplier) =>
        string.IsNullOrWhiteSpace(startTime)
        || string.IsNullOrWhiteSpace(endTime)
        || string.IsNullOrWhiteSpace(multiplier);

    private static bool TryReadClock(JsonElement period, string propertyName, bool isEnd, out int seconds)
    {
        seconds = 0;
        if (!period.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.String)
            return false;
        var value = node.GetString() ?? string.Empty;
        var format = ClockRegex().IsMatch(value) ? "HH:mm:ss" : LegacyClockRegex().IsMatch(value) ? "HH:mm" : null;
        if (format is null || !TimeOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return false;
        seconds = isEnd && time == TimeOnly.MinValue ? 24 * 60 * 60 : time.Hour * 60 * 60 + time.Minute * 60 + time.Second;
        return true;
    }

    [GeneratedRegex("^(?:[01]\\d|2[0-3]):[0-5]\\d:[0-5]\\d$", RegexOptions.CultureInvariant)]
    private static partial Regex ClockRegex();

    [GeneratedRegex("^(?:[01]\\d|2[0-3]):[0-5]\\d$", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyClockRegex();
}
