using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AdminDashboardPageTests
{
    [Fact]
    public void HomeUsesTheFullAdminDashboardOnlyForAdministrators()
    {
        var home = ReadSource("Pages", "Home.razor");

        Assert.Contains("Auth.User?.IsAdmin == true", home, StringComparison.Ordinal);
        Assert.Contains("<AdminDashboardPanel />", home, StringComparison.Ordinal);
        Assert.Contains("<UserDashboardPanel />", home, StringComparison.Ordinal);
        Assert.DoesNotContain("Api.GetDashboardAsync()", home, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminDashboardMatchesTheOfficialOverviewSections()
    {
        var dashboard = ReadSource("Components", "AdminDashboardPanel.razor");

        foreach (var label in new[]
        {
            "API 密钥", "账号", "今日请求", "用户",
            "今日 Token", "总 Token", "性能指标", "平均响应"
        })
        {
            Assert.Contains(label, dashboard, StringComparison.Ordinal);
        }

        Assert.Contains("快捷操作", dashboard, StringComparison.Ordinal);
        Assert.Contains("批量生图", dashboard, StringComparison.Ordinal);
        Assert.Contains("canUseBatchImage", dashboard, StringComparison.Ordinal);
        Assert.Contains("GroupAllowsBatchImageGeneration", dashboard, StringComparison.Ordinal);
        Assert.Contains("分组定价", dashboard, StringComparison.Ordinal);
        Assert.Contains("DxDateRangePicker", dashboard, StringComparison.Ordinal);
        Assert.Contains("粒度", dashboard, StringComparison.Ordinal);
        Assert.Contains("模型分布", dashboard, StringComparison.Ordinal);
        Assert.Contains("用户消费榜", dashboard, StringComparison.Ordinal);
        Assert.Contains("Token 使用趋势", dashboard, StringComparison.Ordinal);
        Assert.Contains("Cache Hit Rate", dashboard, StringComparison.Ordinal);
        Assert.Contains("最近使用（Top 12）", dashboard, StringComparison.Ordinal);
        Assert.Contains("ToggleModelBreakdownAsync", dashboard, StringComparison.Ordinal);
        Assert.Contains("model-breakdown-table", dashboard, StringComparison.Ordinal);
        Assert.Contains("Navigation.NavigateTo($\"/admin/usage?user_id=", dashboard, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminDashboardUsesSnapshotAndRankingEndpoints()
    {
        var dashboard = ReadSource("Components", "AdminDashboardPanel.razor");
        var api = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("Api.GetAdminDashboardSnapshotAsync", dashboard, StringComparison.Ordinal);
        Assert.Contains("Api.GetAdminDashboardRankingAsync", dashboard, StringComparison.Ordinal);
        Assert.Contains("/admin/dashboard/snapshot-v2", api, StringComparison.Ordinal);
        Assert.Contains("include_model_stats=true", api, StringComparison.Ordinal);
        Assert.Contains("include_users_trend=true", api, StringComparison.Ordinal);
        Assert.Contains("users_trend_limit=12", api, StringComparison.Ordinal);
        Assert.Contains("/admin/dashboard/users-ranking", api, StringComparison.Ordinal);
        Assert.Contains("/admin/dashboard/user-breakdown", api, StringComparison.Ordinal);
        Assert.Contains("model_source={Uri.EscapeDataString(normalizedSource)}", api, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminDashboardChartLegendsDoNotCoverTheChartCanvases()
    {
        var dashboard = ReadSource("Components", "AdminDashboardPanel.razor");
        var styles = ReadSource("Components", "AdminDashboardPanel.razor.css");

        Assert.Equal(2, dashboard.Split("<DxChartLegend Visible=\"false\" />", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, dashboard.Split("Position=\"RelativePosition.Outside\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("Color=\"@series.ChartColor\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("UserTrendPalette", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-chart-legend", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain(".admin-chart-legend", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotContractDeserializesAllCostAndTrendFields()
    {
        const string json = """
        {
          "generated_at":"2026-08-14T10:00:00Z",
          "start_date":"2026-08-13",
          "end_date":"2026-08-14",
          "granularity":"hour",
          "stats":{
            "total_users":8,
            "today_new_users":2,
            "total_api_keys":7,
            "active_api_keys":6,
            "total_accounts":5,
            "normal_accounts":4,
            "error_accounts":1,
            "today_tokens":1234,
            "total_tokens":5678,
            "today_cost":1.2,
            "today_actual_cost":1.1,
            "today_account_cost":0.9,
            "total_cost":7.2,
            "total_actual_cost":6.1,
            "total_account_cost":5.9,
            "rpm":3,
            "tpm":120,
            "average_duration_ms":850
          },
          "trend":[{
            "date":"2026-08-14T10:00:00+08:00",
            "input_tokens":100,
            "output_tokens":50,
            "cache_creation_tokens":20,
            "cache_read_tokens":80,
            "total_tokens":250,
            "cost":0.1,
            "actual_cost":0.08
          }],
          "models":[],
          "users_trend":[]
        }
        """;

        var value = JsonSerializer.Deserialize<AdminDashboardSnapshotDto>(json);

        Assert.NotNull(value);
        Assert.Equal(5.9, value.Stats!.TotalAccountCost, 6);
        Assert.Equal(250, value.Trend[0].TotalTokens);
        Assert.Equal(40d, value.Trend[0].CacheHitRate, 6);
    }

    [Fact]
    public void UserBreakdownContractDeserializesOfficialModelDetailFields()
    {
        const string json = """
        {
          "users":[{
            "user_id":42,
            "email":"user@example.test",
            "requests":9,
            "input_tokens":100,
            "output_tokens":50,
            "cache_tokens":25,
            "total_tokens":175,
            "cost":0.8,
            "actual_cost":0.6,
            "account_cost":0.4
          }],
          "start_date":"2026-08-14",
          "end_date":"2026-08-15"
        }
        """;

        var value = JsonSerializer.Deserialize<AdminDashboardUserBreakdownResponseDto>(json);

        Assert.NotNull(value);
        Assert.Single(value.Users);
        Assert.Equal(42, value.Users[0].UserId);
        Assert.Equal(175, value.Users[0].TotalTokens);
        Assert.Equal(0.4, value.Users[0].AccountCost, 6);
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }
}
