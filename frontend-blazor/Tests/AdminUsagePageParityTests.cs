using System.Net;
using System.Text;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AdminUsagePageParityTests
{
    [Fact]
    public void AdminUsageRestoresOfficialDetailTabsAndRouteDrillDown()
    {
        var panel = ReadSource("Components", "AdminUsagePanel.razor");
        var route = ReadSource("Pages", "AdminUsage.razor");

        Assert.Equal(3, panel.Split("role=\"tab\"", StringSplitOptions.None).Length - 1);
        foreach (var label in new[] { "用量记录", "错误请求", "用户排行" })
        {
            Assert.Contains(label, panel, StringComparison.Ordinal);
        }

        Assert.Contains("activeTab == \"errors\"", panel, StringComparison.Ordinal);
        Assert.Contains("activeTab == \"ranking\"", panel, StringComparison.Ordinal);
        Assert.Contains("<AdminUsageErrorTab @key=\"contentVersion\"", panel, StringComparison.Ordinal);
        Assert.Contains("<AdminUsageRankingTab @key=\"contentVersion\"", panel, StringComparison.Ordinal);
        Assert.Contains("SelectRankedUserAsync", panel, StringComparison.Ordinal);
        Assert.Contains("selectedUserId = user.UserId", panel, StringComparison.Ordinal);
        Assert.Contains("type=\"datetime-local\"", panel, StringComparison.Ordinal);
        Assert.Contains("step=\"1\"", panel, StringComparison.Ordinal);
        Assert.Contains("用户邮箱", panel, StringComparison.Ordinal);
        Assert.Contains("for=\"admin-usage-department\">部门", panel, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"全部部门\"", panel, StringComparison.Ordinal);
        Assert.True(
            panel.IndexOf("admin-usage-department", StringComparison.Ordinal) < panel.IndexOf("admin-usage-user-email", StringComparison.Ordinal),
            "部门查询条件应显示在用户邮箱查询条件之前");
        Assert.Contains("ExportCsvAsync", panel, StringComparison.Ordinal);
        Assert.Contains("导出 CSV", panel, StringComparison.Ordinal);
        Assert.Contains("paraGateway.downloadBytes", panel, StringComparison.Ordinal);
        Assert.Contains("费用（USD）", panel, StringComparison.Ordinal);
        Assert.Contains("SearchAdminUsageUsersAsync", panel, StringComparison.Ordinal);
        Assert.Contains("AdminUsageUserSelectionPolicy.FindExact", panel, StringComparison.Ordinal);
        Assert.Contains("AdminUsageUserSelectionPolicy.OrderOptions", panel, StringComparison.Ordinal);
        Assert.Contains("match is null || match.Deleted", panel, StringComparison.Ordinal);
        Assert.Contains("DateTime.Today.ToString(\"yyyy-MM-dd'T'00:00:00\"", panel, StringComparison.Ordinal);
        Assert.Contains("DateTime.Today.AddDays(1).ToString(\"yyyy-MM-dd'T'00:00:00\"", panel, StringComparison.Ordinal);
        Assert.Contains("SupplyParameterFromQuery(Name = \"user_id\")", route, StringComparison.Ordinal);
        Assert.Contains("SupplyParameterFromQuery(Name = \"user_email\")", route, StringComparison.Ordinal);
        Assert.Contains("@page \"/admin/usage\"", route, StringComparison.Ordinal);
        Assert.Contains("InitialStartDate=\"@QueryStartDate\"", route, StringComparison.Ordinal);
        Assert.Contains("InitialEndDate=\"@QueryEndDate\"", route, StringComparison.Ordinal);
        Assert.Contains("<th>来源 IP</th>", panel, StringComparison.Ordinal);
        Assert.Contains("data-label=\"来源 IP\"", panel, StringComparison.Ordinal);
        Assert.Contains("item.IpAddress", panel, StringComparison.Ordinal);
        foreach (var label in new[] { "用户", "Key", "部门" })
        {
            Assert.Contains($"<th>{label}</th>", panel, StringComparison.Ordinal);
            Assert.Contains($"data-label=\"{label}\"", panel, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("用户 / Key", panel, StringComparison.Ordinal);

        var styles = ReadSource("Components", "AdminUsagePanel.razor.css");
        var errorStyles = ReadSource("Components", "AdminUsageErrorTab.razor.css");
        var rankingStyles = ReadSource("Components", "AdminUsageRankingTab.razor.css");
        Assert.Contains("class=\"admin-usage-filter-card\"", panel, StringComparison.Ordinal);
        foreach (var expected in new[] { "background: var(--surface)", "border: 1px solid var(--line)", "border-radius: 10px", "grid-template-columns: repeat(4" })
        {
            Assert.Contains(expected, styles, StringComparison.Ordinal);
        }
        Assert.Contains("background: var(--surface)", errorStyles, StringComparison.Ordinal);
        Assert.Contains("border-radius: 10px", errorStyles, StringComparison.Ordinal);
        Assert.Contains("background: var(--surface)", rankingStyles, StringComparison.Ordinal);
        Assert.Contains("border-radius: 10px", rankingStyles, StringComparison.Ordinal);
        Assert.Contains(".source-ip", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", styles, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("203.0.113.10")]
    [InlineData("2001:db8:85a3::8a2e:370:7334")]
    [InlineData(null)]
    public void AdminUsageMappingPreservesSourceIp(string? sourceIp)
    {
        var record = UsageRecordDto.From(new GoUsageLog
        {
            Id = 19,
            Model = "gpt-5.6",
            IpAddress = sourceIp,
            CreatedAt = new DateTimeOffset(2026, 8, 25, 8, 30, 0, TimeSpan.Zero)
        });

        Assert.Equal(sourceIp, record.IpAddress);
    }

    [Fact]
    public async Task AdminUsageClientPreservesSourceIpAndDepartmentFromApiJson()
    {
        var handler = new UsageQueryHandler
        {
            UsagePayload = """
                {"code":0,"message":"success","data":{"items":[{"id":19,"model":"gpt-5.6","department":"研发部","ip_address":"2001:db8:85a3::8a2e:370:7334","created_at":"2026-08-25T08:30:00Z"}],"total":1,"page":1,"page_size":20,"pages":1}}
                """
        };
        var api = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());

        var result = await api.GetUsageAsync(new AdminUsageQuery());

        var record = Assert.Single(result.Items);
        Assert.Equal("2001:db8:85a3::8a2e:370:7334", record.IpAddress);
        Assert.Equal("研发部", record.Department);
    }

    [Fact]
    public void ErrorTabUsesUnifiedAdminLogsAndSafeDetailSurface()
    {
        var tab = ReadSource("Components", "AdminUsageErrorTab.razor");
        var client = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("Api.GetAdminOpsErrorLogsAsync", tab, StringComparison.Ordinal);
        Assert.Contains("TimeRange = \"custom\"", tab, StringComparison.Ordinal);
        Assert.Contains("View = \"all\"", tab, StringComparison.Ordinal);
        Assert.Contains("UserId = UserId", tab, StringComparison.Ordinal);
        Assert.Contains("错误消息或请求 ID", tab, StringComparison.Ordinal);
        Assert.Contains("Api.GetAdminOpsErrorLogDetailAsync", tab, StringComparison.Ordinal);
        Assert.Contains("错误请求详情", tab, StringComparison.Ordinal);
        Assert.Contains("响应正文", tab, StringComparison.Ordinal);
        Assert.Contains("/admin/ops/errors?{BuildAdminOpsErrorQuery(filter)}", client, StringComparison.Ordinal);
        Assert.Contains("/admin/ops/errors/{id}", client, StringComparison.Ordinal);
    }

    [Fact]
    public void RankingTabUsesFullUserBreakdownContract()
    {
        var tab = ReadSource("Components", "AdminUsageRankingTab.razor");

        foreach (var label in new[] { "请求", "输入 Token", "输出 Token", "缓存 Token", "总 Token", "实际费用" })
        {
            Assert.Contains(label, tab, StringComparison.Ordinal);
        }
        foreach (var limit in new[] { "Top 20", "Top 50", "Top 100", "Top 200" })
        {
            Assert.Contains(limit, tab, StringComparison.Ordinal);
        }

        Assert.Contains("Api.GetAdminDashboardUserBreakdownAsync(new AdminDashboardUserBreakdownQueryDto", tab, StringComparison.Ordinal);
        Assert.Contains("private string sortBy = \"total_tokens\"", tab, StringComparison.Ordinal);
        Assert.Contains("OnSelectUser.InvokeAsync", tab, StringComparison.Ordinal);
        Assert.Contains("value.ToString(\"0.00\"", tab, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0000", tab, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminUsageClientsForwardSharedFiltersToOfficialEndpoints()
    {
        var handler = new UsageQueryHandler();
        var api = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());

        await api.GetUsageAsync(new AdminUsageQuery
        {
            Page = 2,
            PageSize = 50,
            StartDate = "2026-08-23T00:00:00",
            EndDate = "2026-08-24T00:00:00",
            Timezone = "Asia/Shanghai",
            UserId = 17,
            Model = "gpt-5.6",
            Department = "研发部",
            SortBy = "created_at",
            SortOrder = "desc",
            ExactTotal = true
        });

        Assert.Equal("/api/v1/admin/usage", handler.LastPath);
        AssertQuery(handler, "page=2", "page_size=50", "start_date=2026-08-23T00:00:00", "end_date=2026-08-24T00:00:00", "timezone=Asia/Shanghai", "user_id=17", "model=gpt-5.6", "department=研发部", "exact_total=true");

        await api.GetAdminOpsErrorLogsAsync(new OpsErrorListQueryDto
        {
            Page = 3,
            PageSize = 20,
            TimeRange = "custom",
            StartTime = new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.FromHours(8)),
            EndTime = new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.FromHours(8)),
            UserId = 17,
            Model = "gpt-5.6",
            StatusCodes = "429",
            View = "all"
        });

        Assert.Equal("/api/v1/admin/ops/errors", handler.LastPath);
        AssertQuery(handler, "page=3", "view=all", "start_time=", "end_time=", "user_id=17", "model=gpt-5.6", "status_codes=429");

        await api.GetAdminDashboardUserBreakdownAsync(new AdminDashboardUserBreakdownQueryDto
        {
            StartDate = "2026-08-23T00:00:00",
            EndDate = "2026-08-24T00:00:00",
            Timezone = "Asia/Shanghai",
            UserId = 17,
            Model = "gpt-5.6",
            SortBy = "total_tokens",
            Limit = 100,
            EndExclusive = true
        });

        Assert.Equal("/api/v1/admin/dashboard/user-breakdown", handler.LastPath);
        AssertQuery(handler, "start_date=2026-08-23T00:00:00", "end_date=2026-08-24T00:00:00", "timezone=Asia/Shanghai", "user_id=17", "model=gpt-5.6", "sort_by=total_tokens", "limit=100", "end_exclusive=true");

        var users = await api.SearchAdminUsageUsersAsync("dev@example.com");
        Assert.Equal("/api/v1/admin/usage/search-users", handler.LastPath);
        AssertQuery(handler, "q=dev@example.com");
        Assert.Equal("dev@example.com", Assert.Single(users).Email);
    }

    private static void AssertQuery(UsageQueryHandler handler, params string[] fragments)
    {
        var query = Uri.UnescapeDataString(handler.LastQuery);
        foreach (var fragment in fragments) Assert.Contains(fragment, query, StringComparison.Ordinal);
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

    private sealed class NullJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class UsageQueryHandler : HttpMessageHandler
    {
        public string LastPath { get; private set; } = string.Empty;
        public string LastQuery { get; private set; } = string.Empty;
        public string? UsagePayload { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.AbsolutePath ?? string.Empty;
            LastQuery = request.RequestUri?.Query ?? string.Empty;
            var payload = LastPath.EndsWith("/user-breakdown", StringComparison.Ordinal)
                ? "{\"code\":0,\"message\":\"success\",\"data\":{\"users\":[],\"start_date\":\"2026-08-23\",\"end_date\":\"2026-08-24\"}}"
                : LastPath.EndsWith("/search-users", StringComparison.Ordinal)
                    ? "{\"code\":0,\"message\":\"success\",\"data\":[{\"id\":17,\"email\":\"dev@example.com\",\"deleted\":false}]}"
                : UsagePayload ?? "{\"code\":0,\"message\":\"success\",\"data\":{\"items\":[],\"total\":0,\"page\":1,\"page_size\":20,\"pages\":0}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
