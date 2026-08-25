using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class UsersPageParityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void UsersPageMatchesOfficialManagementSurface()
    {
        var markup = ReadSource("Pages", "Users.razor");

        Assert.Contains("@page \"/admin/users\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"page-header\"", markup, StringComparison.Ordinal);
        Assert.Contains("搜索用户", markup, StringComparison.Ordinal);
        Assert.Contains("筛选设置", markup, StringComparison.Ordinal);
        Assert.Contains("列设置", markup, StringComparison.Ordinal);
        Assert.Contains("属性配置", markup, StringComparison.Ordinal);
        Assert.Contains("创建用户", markup, StringComparison.Ordinal);
        Assert.Contains("批量编辑", markup, StringComparison.Ordinal);
        Assert.Contains("API 密钥", markup, StringComparison.Ordinal);
        Assert.Contains("授权分组", markup, StringComparison.Ordinal);
        Assert.Contains("余额历史", markup, StringComparison.Ordinal);
        Assert.Contains("平台限额", markup, StringComparison.Ordinal);
        Assert.Contains("ReplaceAdminUserGroupAsync", markup, StringComparison.Ordinal);
        Assert.Contains("UserAttributesConfigModal", markup, StringComparison.Ordinal);
        Assert.Contains("authorized-group-options", markup, StringComparison.Ordinal);
        Assert.Contains("@oninput=\"GroupFilterChangedAsync\"", markup, StringComparison.Ordinal);
        Assert.Contains("UsageSortHeader", markup, StringComparison.Ordinal);
        Assert.Contains("admin-users-usage-sort", markup, StringComparison.Ordinal);
        Assert.Contains("仅排序当前页", markup, StringComparison.Ordinal);
        Assert.Contains("订阅模式请求不受此限额约束", markup, StringComparison.Ordinal);
        Assert.Contains("全部清空（取消所有限额）", markup, StringComparison.Ordinal);

        Assert.Contains("RunSensitiveAsync", markup, StringComparison.Ordinal);
        Assert.Contains("VerifyTotpStepUpAsync", markup, StringComparison.Ordinal);
        Assert.Contains("user-hidden-columns", markup, StringComparison.Ordinal);
        Assert.Contains("user-visible-filters", markup, StringComparison.Ordinal);
        Assert.Contains("user-filter-values", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("支付", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void UserBalancesUseFixedTwoDecimalDisplayWithoutChangingUsageFormatting()
    {
        var markup = ReadSource("Pages", "Users.razor");

        Assert.Contains("@BalanceMoney(user.Balance)", markup, StringComparison.Ordinal);
        Assert.Contains("当前余额：@BalanceMoney(actionUser.Balance)", markup, StringComparison.Ordinal);
        Assert.Contains("@BalanceMoney(PreviewBalance)", markup, StringComparison.Ordinal);
        Assert.Contains("@BalanceMoney(balanceHistory.TotalRecharged)", markup, StringComparison.Ordinal);
        Assert.Contains("return IsBalanceHistory(item) ? $\"{sign}{BalanceMoney(item.Value)}\"", markup, StringComparison.Ordinal);
        Assert.Contains("BalanceMoney(decimal value)", markup, StringComparison.Ordinal);
        Assert.Contains("BalanceMoney(double value)", markup, StringComparison.Ordinal);
        Assert.Contains("ToString(\"0.00\", System.Globalization.CultureInfo.InvariantCulture)", markup, StringComparison.Ordinal);
        Assert.Contains("Money(stats.TodayActualCost)", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void MoreActionsMenuFloatsOutsideTheScrollableTable()
    {
        var markup = ReadSource("Pages", "Users.razor");
        var css = ReadSource("Pages", "Users.razor.css");
        var tableScrollEnd = markup.IndexOf("<div class=\"table-pagination\">", StringComparison.Ordinal);
        var floatingMenu = markup.IndexOf("class=\"row-action-menu\"", StringComparison.Ordinal);

        Assert.Contains("class=\"row-action-menu-backdrop\"", markup, StringComparison.Ordinal);
        Assert.Contains("paraGateway.positionFloatingMenu", markup, StringComparison.Ordinal);
        Assert.True(tableScrollEnd >= 0 && floatingMenu > tableScrollEnd);
        Assert.Contains(".row-action-menu { display: grid; position: fixed;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("row-menu-anchor", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedUserAttributeCellsRemainTableCells()
    {
        var markup = ReadSource("Pages", "Users.razor");
        var css = ReadSource("Pages", "Users.razor.css");

        // The shared .truncate-cell rule is a block element for non-table content.
        // User-list cells must explicitly retain table-cell layout or row borders
        // and vertically centered attribute values drift out of alignment.
        Assert.Contains("<td class=\"truncate-cell\">@FormatAttributeValue(user.Id, definition)</td>", markup, StringComparison.Ordinal);
        Assert.Contains(".users-data-table td.truncate-cell", css, StringComparison.Ordinal);
        Assert.Contains("display: table-cell;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void UsersPageConnectsEveryOfficialAdminEndpoint()
    {
        var client = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("/admin/users?", client, StringComparison.Ordinal);
        Assert.Contains("/admin/users/{id}/balance", client, StringComparison.Ordinal);
        Assert.Contains("/admin/users/{userId}/balance-history", client, StringComparison.Ordinal);
        Assert.Contains("/admin/users/batch-limits", client, StringComparison.Ordinal);
        Assert.Contains("/admin/users/{userId}/platform-quotas", client, StringComparison.Ordinal);
        Assert.Contains("/admin/users/{userId}/platform-quotas/reset", client, StringComparison.Ordinal);
        Assert.Contains("/admin/users/{userId}/replace-group", client, StringComparison.Ordinal);
        Assert.Contains("/admin/dashboard/users-usage", client, StringComparison.Ordinal);
        Assert.Contains("/admin/user-attributes/batch", client, StringComparison.Ordinal);
        Assert.Contains("/admin/groups/all", client, StringComparison.Ordinal);
    }

    [Fact]
    public void DepartmentAttributeIsSharedByCreateAndEditInTheRequestedOrder()
    {
        var markup = ReadSource("Pages", "Users.razor");

        var usernameIndex = markup.IndexOf("id=\"user-name\"", StringComparison.Ordinal);
        var departmentIndex = markup.IndexOf("id=\"user-department\"", StringComparison.Ordinal);
        var roleIndex = markup.IndexOf("id=\"user-role\"", StringComparison.Ordinal);
        var editorEndIndex = markup.IndexOf("<AppModal Open=\"bulkOpen\"", roleIndex, StringComparison.Ordinal);

        Assert.True(usernameIndex >= 0 && usernameIndex < departmentIndex);
        Assert.True(departmentIndex < roleIndex);
        Assert.True(roleIndex < editorEndIndex);
        Assert.Contains("DepartmentAttributeKey = \"department\"", markup, StringComparison.Ordinal);
        Assert.Contains("@if (DepartmentAttribute is { } departmentAttribute)", markup, StringComparison.Ordinal);
        Assert.Contains("@foreach (var definition in OtherEditorAttributes)", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@foreach (var definition in EnabledAttributes)", markup[usernameIndex..editorEndIndex], StringComparison.Ordinal);
        Assert.Contains("var created = await Api.CreateAdminUserAsync(payload);", markup, StringComparison.Ordinal);
        Assert.Contains("UpdateUserAttributeValuesAsync(created.Id.ToString(), editorAttributes)", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchUsageAndAttributeMapsDeserializeNumericUserKeys()
    {
        var usage = JsonSerializer.Deserialize<AdminBatchUsersUsageResponseDto>("""
            {
              "stats": {
                "42": {
                  "user_id": 42,
                  "today_actual_cost": 1.25,
                  "total_actual_cost": 9.5,
                  "by_platform": [
                    { "platform": "openai", "today_actual_cost": 0.75, "total_actual_cost": 4.5 }
                  ]
                }
              }
            }
            """, Json);
        var attributes = JsonSerializer.Deserialize<AdminBatchUserAttributesResponseDto>("""
            { "attributes": { "42": { "7": "研发部" } } }
            """, Json);

        Assert.NotNull(usage);
        Assert.Equal(1.25, usage.Stats[42].TodayActualCost);
        Assert.Equal("openai", usage.Stats[42].ByPlatform.Single().Platform);
        Assert.NotNull(attributes);
        Assert.Equal("研发部", attributes.Attributes[42][7]);
    }

    [Fact]
    public void AdminUserContractIncludesOfficialGroupsSubscriptionsAndActivity()
    {
        var user = JsonSerializer.Deserialize<GoUser>("""
            {
              "id": 9,
              "email": "user@example.com",
              "username": "测试用户",
              "notes": "备注",
              "role": "user",
              "balance": 12.5,
              "concurrency": 4,
              "current_concurrency": 2,
              "rpm_limit": 30,
              "status": "active",
              "allowed_groups": [3],
              "group_rates": { "3": 1.2 },
              "last_used_at": "2026-08-14T08:00:00Z",
              "subscriptions": [
                { "id": 1, "user_id": 9, "group_id": 5, "status": "active" }
              ],
              "created_at": "2026-08-01T00:00:00Z",
              "updated_at": "2026-08-14T00:00:00Z"
            }
            """, Json);

        Assert.NotNull(user);
        Assert.Equal(2, user.CurrentConcurrency);
        Assert.Equal(3, user.AllowedGroups.Single());
        Assert.Equal(1.2, user.GroupRates[3]);
        Assert.Single(user.Subscriptions);
        Assert.NotNull(user.LastUsedAt);
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
