using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class UserDashboardPageParityTests
{
    [Fact]
    public void HomeRoutesAdminsAndUsersToIndependentDashboardComponents()
    {
        var page = ReadSource("Pages", "Home.razor");

        Assert.Contains("<AdminDashboardPanel />", page, StringComparison.Ordinal);
        Assert.Contains("<UserDashboardPanel />", page, StringComparison.Ordinal);
        Assert.DoesNotContain("用户总数", page, StringComparison.Ordinal);
        Assert.DoesNotContain("成功率", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDashboardAsync", page, StringComparison.Ordinal);
    }

    [Fact]
    public void UserDashboardCoversOfficialStatsChartsQuotasRecentUsageAndQuickActions()
    {
        var component = ReadSource("Components", "UserDashboardPanel.razor");

        foreach (var label in new[]
        {
            "可用余额", "API 密钥", "今日请求", "今日费用",
            "今日 Token", "总 Token", "性能指标", "平均响应",
            "平台用量", "平台额度", "模型分布", "Token 使用趋势",
            "最近使用", "快捷操作"
        })
        {
            Assert.Contains(label, component, StringComparison.Ordinal);
        }

        Assert.Contains("Api.GetUserDashboardStatsAsync()", component, StringComparison.Ordinal);
        Assert.Contains("Api.GetUserDashboardTrendAsync", component, StringComparison.Ordinal);
        Assert.Contains("Api.GetMyUsageModelsAsync", component, StringComparison.Ordinal);
        Assert.Contains("Api.GetMyUsageLogsAsync", component, StringComparison.Ordinal);
        Assert.Contains("Api.GetMyPlatformQuotasAsync()", component, StringComparison.Ordinal);
        Assert.Contains("Api.GetMyApiKeysPageAsync", component, StringComparison.Ordinal);
        Assert.Contains("Take(5)", component, StringComparison.Ordinal);
        Assert.Contains("DateTime.Today.AddDays(-6)", component, StringComparison.Ordinal);
        Assert.Contains("DxDateRangePicker", component, StringComparison.Ordinal);
        Assert.Contains("DxPieChart", component, StringComparison.Ordinal);
        Assert.Contains("Cache Hit Rate", component, StringComparison.Ordinal);
        Assert.Contains("href=\"/keys\"", component, StringComparison.Ordinal);
        Assert.Contains("href=\"/usage\"", component, StringComparison.Ordinal);
        Assert.Contains("href=\"/batch-image\"", component, StringComparison.Ordinal);
        Assert.Contains("href=\"/redeem\"", component, StringComparison.Ordinal);
        Assert.Contains("GroupAllowsBatchImageGeneration", component, StringComparison.Ordinal);
        Assert.Contains("Auth.User?.IsSimpleMode", component, StringComparison.Ordinal);
        Assert.Contains("diffTotal > 0.0001", component, StringComparison.Ordinal);
        Assert.Contains("window.Limit.Value == 0", component, StringComparison.Ordinal);
        Assert.Contains("stats.TodayCacheCreationTokens + stats.TodayCacheReadTokens", component, StringComparison.Ordinal);
        Assert.Contains("stats.TotalCacheCreationTokens + stats.TotalCacheReadTokens", component, StringComparison.Ordinal);
        Assert.Contains("缓存创建", component, StringComparison.Ordinal);
        Assert.Contains("缓存读取", component, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardChartLegendsDoNotCoverTheChartCanvas()
    {
        var component = ReadSource("Components", "UserDashboardPanel.razor");
        var styles = ReadSource("Components", "UserDashboardPanel.razor.css");

        Assert.Equal(2, component.Split("<DxChartLegend Visible=\"false\" />", StringSplitOptions.None).Length - 1);
        Assert.Contains("class=\"trend-chart-legend\"", component, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Token 使用趋势图例\"", component, StringComparison.Ordinal);
        Assert.Contains(".trend-chart-layout", styles, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap;", styles, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardRefreshButtonKeepsItsLabelOnOneLine()
    {
        var component = ReadSource("Components", "UserDashboardPanel.razor");
        var styles = ReadSource("Components", "UserDashboardPanel.razor.css");

        Assert.Contains("dashboard-refresh-button", component, StringComparison.Ordinal);
        Assert.Contains(".dashboard-refresh-button", styles, StringComparison.Ordinal);
        Assert.Contains("min-width: 76px;", styles, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiClientUsesTheOfficialUserDashboardEndpoints()
    {
        var api = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("/usage/dashboard/stats", api, StringComparison.Ordinal);
        Assert.Contains("/usage/dashboard/trend", api, StringComparison.Ordinal);
        Assert.Contains("/usage/dashboard/models", api, StringComparison.Ordinal);
        Assert.Contains("/usage?{BuildUserUsageQuery(query, includePagination: true)}", api, StringComparison.Ordinal);
        Assert.Contains("/user/platform-quotas", api, StringComparison.Ordinal);
        Assert.Contains("Task<UserPlatformQuotaResponseDto> GetMyPlatformQuotasAsync", api, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardContractsDeserializeAllOfficialFieldsAndRunMode()
    {
        const string statsJson = """
        {
          "total_api_keys": 4,
          "active_api_keys": 3,
          "total_requests": 28,
          "total_input_tokens": 100,
          "total_output_tokens": 50,
          "total_cache_creation_tokens": 25,
          "total_cache_read_tokens": 75,
          "total_tokens": 250,
          "total_cost": 2.5,
          "total_actual_cost": 1.25,
          "today_requests": 7,
          "today_input_tokens": 20,
          "today_output_tokens": 10,
          "today_cache_creation_tokens": 5,
          "today_cache_read_tokens": 15,
          "today_tokens": 50,
          "today_cost": 0.5,
          "today_actual_cost": 0.25,
          "average_duration_ms": 1250,
          "rpm": 9,
          "tpm": 1500,
          "by_platform": [{"platform":"openai","total_requests":20,"total_tokens":200,"total_actual_cost":1.0,"today_requests":5,"today_tokens":40,"today_actual_cost":0.2}]
        }
        """;
        const string quotasJson = """
        {"platform_quotas":[{"platform":"openai","daily_limit_usd":10,"daily_usage_usd":2.5,"daily_window_resets_at":"2026-08-16T00:00:00Z"}]}
        """;

        var stats = JsonSerializer.Deserialize<UserDashboardStats>(statsJson);
        var quotas = JsonSerializer.Deserialize<UserPlatformQuotaResponseDto>(quotasJson);
        var authUser = AuthUser.From(new GoUser { Id = 1, RunMode = "simple" });

        Assert.NotNull(stats);
        Assert.Equal(4, stats.TotalApiKeys);
        Assert.Equal(250, stats.TotalTokens);
        Assert.Equal(50, stats.TodayTokens);
        Assert.Equal(25, stats.TotalCacheCreationTokens);
        Assert.Equal(75, stats.TotalCacheReadTokens);
        Assert.Equal(5, stats.TodayCacheCreationTokens);
        Assert.Equal(15, stats.TodayCacheReadTokens);
        Assert.Equal(9, stats.Rpm);
        Assert.Equal("openai", Assert.Single(stats.ByPlatform).Platform);
        Assert.NotNull(quotas);
        Assert.Equal(10, Assert.Single(quotas.PlatformQuotas).DailyLimitUsd);
        Assert.True(authUser.IsSimpleMode);
    }

    [Fact]
    public void UserTrendCalculatesCacheHitRateLikeTheOfficialChart()
    {
        var point = new UserUsageTrendPointDto
        {
            InputTokens = 100,
            CacheCreationTokens = 50,
            CacheReadTokens = 50
        };

        Assert.Equal(25d, point.CacheHitRate);
    }

    private static string ReadSource(string folder, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, folder, fileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {folder}/{fileName}.");
    }
}
