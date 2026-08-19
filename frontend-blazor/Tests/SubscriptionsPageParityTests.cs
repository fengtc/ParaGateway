using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class SubscriptionsPageParityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void AdminSubscriptionsPageMatchesOfficialManagementSurface()
    {
        var markup = ReadSource("Pages", "Subscriptions.razor");

        Assert.Contains("@page \"/admin/subscriptions\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"page-header\"", markup, StringComparison.Ordinal);
        Assert.Contains("邮箱/用户名/备注/API Key 模糊搜索", markup, StringComparison.Ordinal);
        Assert.Contains("private string statusFilter = \"active\";", markup, StringComparison.Ordinal);
        Assert.Contains("全部状态", markup, StringComparison.Ordinal);
        Assert.Contains("全部分组", markup, StringComparison.Ordinal);
        Assert.Contains("全部平台", markup, StringComparison.Ordinal);
        Assert.Contains("列设置", markup, StringComparison.Ordinal);
        Assert.Contains("subscription-hidden-columns", markup, StringComparison.Ordinal);
        Assert.Contains("subscription-user-column-mode", markup, StringComparison.Ordinal);
        Assert.Contains("使用指南", markup, StringComparison.Ordinal);
        Assert.Contains("分配订阅", markup, StringComparison.Ordinal);
        Assert.Contains("<th>分组</th>", markup, StringComparison.Ordinal);
        Assert.Contains("<th>用量</th>", markup, StringComparison.Ordinal);
        Assert.Contains("到期时间", markup, StringComparison.Ordinal);
        Assert.Contains("生效中", markup, StringComparison.Ordinal);
        Assert.Contains("调整", markup, StringComparison.Ordinal);
        Assert.Contains("重置配额", markup, StringComparison.Ordinal);
        Assert.Contains("撤销", markup, StringComparison.Ordinal);
        Assert.Contains("恢复", markup, StringComparison.Ordinal);
        Assert.Contains("分配一个订阅以开始使用。", markup, StringComparison.Ordinal);
        Assert.Contains("Api.GetAdminSubscriptionsAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.AssignAdminSubscriptionAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.AdjustAdminSubscriptionAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.ResetAdminSubscriptionQuotaAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.RevokeAdminSubscriptionAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.RestoreAdminSubscriptionAsync", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("支付购买页面未实现", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("购买订阅", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminAndSelfRoutesUseTheirOwnDataSurfaces()
    {
        var markup = ReadSource("Pages", "Subscriptions.razor");

        Assert.Contains("AbsolutePath.TrimEnd('/').Equals(\"/admin/subscriptions\"", markup, StringComparison.Ordinal);
        Assert.Contains("Api.GetMySubscriptionsAsync", markup, StringComparison.Ordinal);
        Assert.Contains("暂无有效订阅", markup, StringComparison.Ordinal);
        Assert.Contains("请联系管理员获取订阅", markup, StringComparison.Ordinal);
        Assert.Contains("self-subscriptions-grid", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Auth.User?.IsAdmin", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SubscriptionClientConnectsEveryOfficialManagementEndpoint()
    {
        var client = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("/admin/subscriptions?", client, StringComparison.Ordinal);
        Assert.Contains("/admin/subscriptions/{id}/progress", client, StringComparison.Ordinal);
        Assert.Contains("/admin/subscriptions/assign", client, StringComparison.Ordinal);
        Assert.Contains("/admin/subscriptions/bulk-assign", client, StringComparison.Ordinal);
        Assert.Contains("/admin/subscriptions/{id}/extend", client, StringComparison.Ordinal);
        Assert.Contains("subscription-adjust-{id}-", client, StringComparison.Ordinal);
        Assert.Contains("[\"Idempotency-Key\"]", client, StringComparison.Ordinal);
        Assert.Contains("/admin/subscriptions/{id}/reset-quota", client, StringComparison.Ordinal);
        Assert.Contains("/admin/subscriptions/{id}/revoke", client, StringComparison.Ordinal);
        Assert.Contains("/admin/subscriptions/{id}/restore", client, StringComparison.Ordinal);
        Assert.Contains("GetMySubscriptionsAsync", client, StringComparison.Ordinal);
    }

    [Fact]
    public void SubscriptionContractPreservesUsageWindowsAndNestedEntities()
    {
        var value = JsonSerializer.Deserialize<SubscriptionDto>("""
            {
              "id": 18,
              "user_id": 3,
              "group_id": 9,
              "status": "active",
              "starts_at": "2026-08-01T00:00:00Z",
              "expires_at": "2026-09-01T00:00:00Z",
              "daily_window_start": "2026-08-14T00:00:00Z",
              "weekly_window_start": "2026-08-10T00:00:00Z",
              "monthly_window_start": "2026-08-01T00:00:00Z",
              "daily_usage_usd": 1.25,
              "weekly_usage_usd": 7.5,
              "monthly_usage_usd": 22.75,
              "assigned_by": 1,
              "notes": "manual",
              "user": { "id": 3, "email": "member@example.com", "username": "member" },
              "group": {
                "id": 9,
                "name": "OpenAI Pro",
                "platform": "openai",
                "subscription_type": "subscription",
                "daily_limit_usd": 5,
                "weekly_limit_usd": 25,
                "monthly_limit_usd": 80,
                "peak_rate_enabled": true,
                "peak_start": "18:00",
                "peak_end": "23:00",
                "peak_rate_multiplier": 1.5
              }
            }
            """, Json);

        Assert.NotNull(value);
        Assert.Equal(1.25, value.DailyUsageUsd);
        Assert.Equal(7.5, value.WeeklyUsageUsd);
        Assert.Equal(22.75, value.MonthlyUsageUsd);
        Assert.Equal("member@example.com", value.User?.Email);
        Assert.Equal("OpenAI Pro", value.Group?.Name);
        Assert.Equal(80, value.Group?.MonthlyLimitUsd);
        Assert.True(value.Group?.PeakRateEnabled);
        Assert.Equal(1.5, value.Group?.PeakRateMultiplier);
    }

    [Fact]
    public void ShellUsesOfficialSubscriptionDescription()
    {
        var layout = ReadSource("Layout", "MainLayout.razor");
        Assert.Contains("new(\"订阅管理\", \"管理用户订阅和配额限制\")", layout, StringComparison.Ordinal);
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
