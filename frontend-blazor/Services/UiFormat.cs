using System.Globalization;

namespace ParaGateway.Frontend.Services;

public static class UiFormat
{
    public static string Money(long micros) => (micros / 1_000_000m).ToString("$#,##0.00", CultureInfo.InvariantCulture);

    public static string Usd(double value) => value.ToString("$#,##0.00", CultureInfo.InvariantCulture);

    public static string Integer(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string DateTime(DateTimeOffset value) => value == default
        ? "-"
        : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public static string Date(DateTimeOffset value) => value == default
        ? "-"
        : value.ToLocalTime().ToString("yyyy-MM-dd");

    public static string RelativeDateTime(DateTimeOffset value)
    {
        if (value == default) return "-";
        var local = value.ToLocalTime();
        var span = DateTimeOffset.Now - local;
        var relative = span.TotalSeconds switch
        {
            < 60 => "刚刚",
            _ when span.TotalMinutes < 60 => $"{Math.Max(1, (int)span.TotalMinutes)} 分钟前",
            _ when span.TotalHours < 24 => $"{Math.Max(1, (int)span.TotalHours)} 小时前",
            _ when span.TotalDays < 30 => $"{Math.Max(1, (int)span.TotalDays)} 天前",
            _ => local.ToString("yyyy-MM-dd")
        };
        return $"{relative} · {local:yyyy-MM-dd HH:mm}";
    }

    public static string Provider(string type) => type.Equals("claude", StringComparison.OrdinalIgnoreCase)
            ? "Claude"
            : "OpenAI";
}
