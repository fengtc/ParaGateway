using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class GroupsPageParityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void GroupsPageMatchesOfficialListAndActions()
    {
        var markup = ReadSource("Pages", "Groups.razor");

        Assert.Contains("@page \"/admin/groups\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"page-header\"", markup, StringComparison.Ordinal);
        Assert.Contains("搜索分组...", markup, StringComparison.Ordinal);
        Assert.Contains("全部平台", markup, StringComparison.Ordinal);
        Assert.Contains("全部状态", markup, StringComparison.Ordinal);
        Assert.Contains("全部分组", markup, StringComparison.Ordinal);
        Assert.Contains("列设置", markup, StringComparison.Ordinal);
        Assert.Contains("排序", markup, StringComparison.Ordinal);
        Assert.Contains("创建分组", markup, StringComparison.Ordinal);
        Assert.Contains("专属倍率", markup, StringComparison.Ordinal);
        Assert.Contains("专属 RPM", markup, StringComparison.Ordinal);
        Assert.Contains("复制", markup, StringComparison.Ordinal);
        Assert.Contains("group.Platform == \"composite\"", markup, StringComparison.Ordinal);
        Assert.Contains("组合路由", markup, StringComparison.Ordinal);
        Assert.Contains("group-hidden-columns", markup, StringComparison.Ordinal);
        Assert.Contains("GetAdminGroupUsageSummaryAsync", markup, StringComparison.Ordinal);
        Assert.Contains("GetAdminGroupCapacitySummaryAsync", markup, StringComparison.Ordinal);
        Assert.Contains("UpdateAdminGroupSortOrderAsync", markup, StringComparison.Ordinal);
        foreach (var platform in new[] { "kimi", "zhipu", "deepseek" })
        {
            Assert.Contains($"value=\"{platform}\"", markup, StringComparison.Ordinal);
        }
        Assert.Contains("GetUsage(group).YesterdayCost", markup, StringComparison.Ordinal);
        Assert.Contains("昨日", markup, StringComparison.Ordinal);
        Assert.Contains("GroupOverridesModal", markup, StringComparison.Ordinal);
        Assert.Contains("CompositeRoutesModal", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("支付", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupsClientConnectsOfficialManagementEndpoints()
    {
        var client = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("/admin/groups?", client, StringComparison.Ordinal);
        Assert.Contains("/admin/groups/all?include_inactive=true", client, StringComparison.Ordinal);
        Assert.Contains("/admin/groups/{Uri.EscapeDataString(id)}/duplicate", client, StringComparison.Ordinal);
        Assert.Contains("Idempotency-Key", client, StringComparison.Ordinal);
        Assert.Contains("/admin/groups/usage-summary", client, StringComparison.Ordinal);
        Assert.Contains("/admin/groups/capacity-summary", client, StringComparison.Ordinal);
        Assert.Contains("/admin/groups/sort-order", client, StringComparison.Ordinal);
        Assert.Contains("/rate-multipliers", client, StringComparison.Ordinal);
        Assert.Contains("/rpm-overrides", client, StringComparison.Ordinal);
        Assert.Contains("/composite-routes", client, StringComparison.Ordinal);
        Assert.Contains("/composite-routes/preview", client, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupContractsPreserveOfficialCountsLimitsAndCapacity()
    {
        var group = JsonSerializer.Deserialize<GoGroup>("""
            {
              "id": 12,
              "name": "OpenAI 主分组",
              "platform": "openai",
              "status": "active",
              "subscription_type": "subscription",
              "rate_multiplier": 1.25,
              "account_count": 7,
              "active_account_count": 5,
              "rate_limited_account_count": 1,
              "sort_order": 20,
              "daily_limit_usd": 10,
              "weekly_limit_usd": 50,
              "monthly_limit_usd": 100,
              "rpm_limit": 300
            }
            """, Json);
        var capacity = JsonSerializer.Deserialize<GroupCapacitySummaryDto>("""
            { "group_id": 12, "concurrency_used": 2, "concurrency_max": 8, "sessions_used": 1, "sessions_max": 4, "rpm_used": 20, "rpm_max": 300 }
            """, Json);

        Assert.NotNull(group);
        var dto = GroupDto.From(group);
        Assert.Equal(7, dto.AccountCount);
        Assert.Equal(5, dto.ActiveAccountCount);
        Assert.Equal(1, dto.RateLimitedAccountCount);
        Assert.Equal(100, dto.MonthlyLimitUsd);
        Assert.Equal(300, dto.RpmLimit);
        Assert.NotNull(capacity);
        Assert.Equal(8, capacity.ConcurrencyMax);
        Assert.Equal(300, capacity.RpmMax);
    }

    [Fact]
    public void CompositeRouteAndOverrideComponentsExposeOfficialFields()
    {
        var routes = ReadSource("Components", "CompositeRoutesModal.razor");
        var overrides = ReadSource("Components", "GroupOverridesModal.razor");

        Assert.Contains("公开模型", routes, StringComparison.Ordinal);
        Assert.Contains("匹配类型", routes, StringComparison.Ordinal);
        Assert.Contains("目标平台", routes, StringComparison.Ordinal);
        Assert.Contains("接口范围", routes, StringComparison.Ordinal);
        Assert.Contains("上游模型", routes, StringComparison.Ordinal);
        Assert.Contains("路由预览", routes, StringComparison.Ordinal);
        Assert.Contains("CreateCompositeRouteAsync", routes, StringComparison.Ordinal);
        Assert.Contains("UpdateCompositeRouteAsync", routes, StringComparison.Ordinal);
        Assert.Contains("DeleteCompositeRouteAsync", routes, StringComparison.Ordinal);
        Assert.Contains("PreviewCompositeRouteAsync", routes, StringComparison.Ordinal);
        Assert.Contains("搜索用户邮箱或用户名", overrides, StringComparison.Ordinal);
        Assert.Contains("批量调整", overrides, StringComparison.Ordinal);
        Assert.Contains("SaveAdminGroupRateMultipliersAsync", overrides, StringComparison.Ordinal);
        Assert.Contains("SaveAdminGroupRpmOverridesAsync", overrides, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupListTypographyUsesThePageDescriptionSizeAsItsPrimaryBaseline()
    {
        var css = ReadSource("Pages", "Groups.razor.css");
        var index = ReadSource("wwwroot", "index.html");

        Assert.Contains(".groups-data-table { width: 100%; min-width: 1370px; border-collapse: collapse; color: var(--ink); font-size: .83rem; }", css, StringComparison.Ordinal);
        Assert.Contains(".groups-data-table th { height: 42px; color: var(--muted); background: var(--surface-muted); font-size: .76rem;", css, StringComparison.Ordinal);
        Assert.Contains(".name-cell strong { display: block; color: var(--ink); font-size: .88rem; }", css, StringComparison.Ordinal);
        Assert.Contains("font-size: .75rem;", css, StringComparison.Ordinal);
        Assert.Contains("font-size: .69rem;", css, StringComparison.Ordinal);
        Assert.Contains("ParaGateway.Frontend.styles.css?v=20260819-dark-theme-audit", index, StringComparison.Ordinal);
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
