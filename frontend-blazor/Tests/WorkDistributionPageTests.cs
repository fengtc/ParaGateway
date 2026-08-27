using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class WorkDistributionPageTests
{
    [Fact]
    public void NavigationRouteGuardAndHeaderExposeAdminWorkDistribution()
    {
        var menu = Read("Layout", "NavMenu.razor");
        var layout = Read("Layout", "MainLayout.razor");
        var guard = Read("Components", "RouteGuard.razor");
        var page = Read("Pages", "AdminWorkDistribution.razor");

        var dashboard = menu.IndexOf("href=\"/admin/dashboard\"", StringComparison.Ordinal);
        var workDistribution = menu.IndexOf("href=\"/admin/work-distribution\"", StringComparison.Ordinal);
        var operations = menu.IndexOf("href=\"/admin/ops\"", StringComparison.Ordinal);
        Assert.True(dashboard >= 0 && dashboard < workDistribution && workDistribution < operations);
        Assert.Contains("工作分布", menu, StringComparison.Ordinal);
        Assert.Contains("\"admin/work-distribution\" => new(\"工作内容分析\"", layout, StringComparison.Ordinal);
        Assert.Contains("typeof(Pages.AdminWorkDistribution)", guard, StringComparison.Ordinal);
        Assert.Contains("@page \"/admin/work-distribution\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void PageHasCompleteFiltersTwoIndependentStackedBarsAndPrivacyQualitySignals()
    {
        var page = Read("Pages", "AdminWorkDistribution.razor");
        foreach (var label in new[]
                 {
                     "人员", "部门", "岗位角色", "开始日期", "结束日期",
                     "提交次数（AI 请求）", "工作量（Token）", "工作相关性", "工作类别分布",
                     "工作相关", "非工作", "不确定", "分类覆盖率", "平均置信度", "分类依据",
                     "低样本隐私状态", "部门工作分布", "人员工作分布", "分类明细", "申诉审核"
                 })
        {
            Assert.Contains(label, page, StringComparison.Ordinal);
        }

        Assert.Contains("summary?.WorkRelated", page, StringComparison.Ordinal);
        Assert.Contains("summary?.Categories", page, StringComparison.Ordinal);
        Assert.Contains("RelationSegments", page, StringComparison.Ordinal);
        Assert.Contains("CategorySegments", page, StringComparison.Ordinal);
        Assert.Contains("DepartmentChartRows", page, StringComparison.Ordinal);
        Assert.Contains("WorkRelatedShare(segments)", page, StringComparison.Ordinal);
        Assert.Contains("summary.Privacy.MinCohortSize", page, StringComparison.Ordinal);
        Assert.Contains("ClassificationSourceLabel", page, StringComparison.Ordinal);
        Assert.Contains("项目 / 仓库 / 提交类型", page, StringComparison.Ordinal);
        Assert.Contains("SubmissionTypeLabel(row.Metadata?.SubmissionType)", page, StringComparison.Ordinal);
        Assert.Contains("分类来源 / 版本", page, StringComparison.Ordinal);
        Assert.Contains("\"non_work\" => \"#dc2626\"", page, StringComparison.Ordinal);
        Assert.Contains("_ => \"#94a3b8\"", page, StringComparison.Ordinal);
        Assert.Contains("其他小样本部门（匿名汇总）", page, StringComparison.Ordinal);
        Assert.Contains("IsFilterableDepartment(item.Department)", page, StringComparison.Ordinal);
        Assert.Contains("roles.OrderBy(RoleLabel", page, StringComparison.Ordinal);
        Assert.Contains("value.Roles", page, StringComparison.Ordinal);
        Assert.Contains("IsFilterableRole(item.Role)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("{ \"admin\", \"user\" }", page, StringComparison.Ordinal);
        Assert.Contains("_ => value!.Trim()", page, StringComparison.Ordinal);
        Assert.True(page.Split("role=\"img\"", StringSplitOptions.None).Length - 1 >= 3);
        Assert.Contains("summary.Departments.Count", page, StringComparison.Ordinal);
        Assert.Contains("小部门不会单独显示", page, StringComparison.Ordinal);
        Assert.Contains("否则从当前汇总中隐藏", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Git commit", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("演示数据", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("non_work", "coding", "non_work", "non_work")]
    [InlineData("uncertain", "coding", "uncertain", "unclassified")]
    [InlineData("work", "documentation", "work", "documentation")]
    [InlineData("work", "non_work", "work", "coding")]
    public void CorrectionFormKeepsWorkRelationAndCategoryPairsConsistent(
        string inputRelation, string inputCategory, string expectedRelation, string expectedCategory)
    {
        var page = Read("Pages", "AdminWorkDistribution.razor");
        var component = new ParaGateway.Frontend.Pages.AdminWorkDistribution();
        var componentType = component.GetType();
        var setPair = componentType.GetMethod("SetCorrectionPair", BindingFlags.Instance | BindingFlags.NonPublic);
        var relationField = componentType.GetField("correctionWorkRelated", BindingFlags.Instance | BindingFlags.NonPublic);
        var categoryField = componentType.GetField("correctionCategory", BindingFlags.Instance | BindingFlags.NonPublic);
        var isValidProperty = componentType.GetProperty("IsCorrectionPairValid", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(setPair);
        Assert.NotNull(relationField);
        Assert.NotNull(categoryField);
        Assert.NotNull(isValidProperty);
        setPair.Invoke(component, [inputRelation, inputCategory]);
        Assert.Equal(expectedRelation, relationField.GetValue(component));
        Assert.Equal(expectedCategory, categoryField.GetValue(component));
        Assert.True((bool)isValidProperty.GetValue(component)!);
        Assert.Contains("disabled=\"@(correctionWorkRelated != \"work\")\"", page, StringComparison.Ordinal);
        Assert.Contains("!IsCorrectionPairValid", page, StringComparison.Ordinal);
    }

    [Fact]
    public void PageUsesRealRecordCorrectionReviewAndResolutionEndpoints()
    {
        var page = Read("Pages", "AdminWorkDistribution.razor");
        var client = Read("Services", "ApiClient.cs");

        foreach (var call in new[]
                 {
                     "GetAdminWorkDistributionSummaryAsync", "GetAdminWorkDistributionRecordsAsync",
                     "CreateAdminWorkDistributionCorrectionAsync", "GetAdminWorkDistributionReviewsAsync",
                     "ResolveAdminWorkDistributionReviewAsync"
                 })
        {
            Assert.Contains(call, page, StringComparison.Ordinal);
            Assert.Contains(call, client, StringComparison.Ordinal);
        }

        foreach (var route in new[]
                 {
                     "/admin/work-distribution/summary", "/admin/work-distribution/records?",
                     "/admin/work-distribution/records/{usageLogId}/correction",
                     "/admin/work-distribution/reviews?", "/admin/work-distribution/reviews/{reviewId}/resolve"
                 })
        {
            Assert.Contains(route, client, StringComparison.Ordinal);
        }
        Assert.Contains("仅包含结构化元数据和分类结果", page, StringComparison.Ordinal);
        Assert.Contains("不展示提示词、源代码或请求正文", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiClientForwardsExactWorkDistributionContract()
    {
        var handler = new WorkDistributionHandler();
        var api = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());

        var summary = await api.GetAdminWorkDistributionSummaryAsync(new WorkDistributionSummaryQueryDto
        {
            StartDate = "2026-08-01", EndDate = "2026-08-27", Timezone = "Asia/Shanghai",
            UserId = 17, Department = "研发", Role = "user", Metric = "tokens",
            MinSampleSize = 8, MinCohortSize = 7, UserLimit = 120
        });
        Assert.Equal("active", summary.CollectionStatus);
        Assert.Equal(12, summary.Coverage.TotalRequests);
        Assert.Equal(0.85, summary.AverageConfidence);
        Assert.Equal("work", Assert.Single(summary.WorkRelated).WorkRelated);
        Assert.Equal("coding", Assert.Single(summary.Categories).Category);
        AssertRequest(handler.Requests[0], HttpMethod.Get, "/api/v1/admin/work-distribution/summary",
            "start_date=2026-08-01", "end_date=2026-08-27", "timezone=Asia/Shanghai", "user_id=17",
            "department=研发", "role=user", "metric=tokens", "min_sample_size=8", "min_cohort_size=7", "user_limit=120");

        await api.GetAdminWorkDistributionRecordsAsync(new WorkDistributionRecordQueryDto
        {
            StartDate = "2026-08-01", EndDate = "2026-08-27", UserId = 17,
            Category = "coding", WorkRelated = "work", ReviewStatus = "pending", MinCohortSize = 9, Page = 2, PageSize = 30
        });
        AssertRequest(handler.Requests[1], HttpMethod.Get, "/api/v1/admin/work-distribution/records",
            "category=coding", "work_related=work", "review_status=pending", "min_cohort_size=9", "page=2", "page_size=30");

        await api.CreateAdminWorkDistributionCorrectionAsync(91, "work", "coding", "incorrect_category");
        Assert.Equal("/api/v1/admin/work-distribution/records/91/correction", handler.Requests[2].Uri.AbsolutePath);
        Assert.Contains("\"reason_code\":\"incorrect_category\"", handler.Requests[2].Body, StringComparison.Ordinal);

        await api.GetAdminWorkDistributionReviewsAsync(new WorkDistributionReviewQueryDto { Status = "pending", UserId = 17, Page = 3, PageSize = 25 });
        AssertRequest(handler.Requests[3], HttpMethod.Get, "/api/v1/admin/work-distribution/reviews", "status=pending", "user_id=17", "page=3", "page_size=25");

        await api.ResolveAdminWorkDistributionReviewAsync(7, "approved", "confirmed_correction");
        Assert.Equal("/api/v1/admin/work-distribution/reviews/7/resolve", handler.Requests[4].Uri.AbsolutePath);
        Assert.Contains("\"decision\":\"approved\"", handler.Requests[4].Body, StringComparison.Ordinal);
        Assert.Contains("\"resolution_note\":\"confirmed_correction\"", handler.Requests[4].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void DtosMatchServiceWireNamesAndLabels()
    {
        const string json = """
            {
              "collection_status":"active",
              "average_confidence":0.75,
              "confidence_sample_count":9,
              "privacy":{"min_sample_size":5,"min_cohort_size":8,"suppressed_users":2},
              "coverage":{"total_requests":20,"classified_requests":15,"unclassified_requests":5,"classified_percent":75},
              "work_related":[{"work_related":"work","requests":15,"total_tokens":300,"value":15,"percent":75}],
              "categories":[{"category":"documentation","work_related":"work","requests":8,"total_tokens":180,"value":8,"percent":40,"average_confidence":0.8,"confidence_sample_count":8}],
              "departments":[{"department":"研发","requests":4,"total_tokens":120,"value":4,"average_confidence":0.82,"confidence_sample_count":4,"categories":[{"category":"coding","work_related":"work","requests":4,"total_tokens":120,"value":4,"percent":100}]}],
              "roles":[{"role":"研发","user_count":8}],
              "users":[]
            }
            """;
        var value = JsonSerializer.Deserialize<WorkDistributionSummaryDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(value);
        Assert.Equal(5, value.Privacy.MinSampleSize);
        Assert.Equal(8, value.Privacy.MinCohortSize);
        Assert.Equal(75, value.Coverage.ClassifiedPercent);
        Assert.Equal("工作相关", WorkDistributionLabels.WorkRelated(value.WorkRelated[0].WorkRelated));
        Assert.Equal("文档", WorkDistributionLabels.Category(value.Categories[0].Category));
        Assert.Equal(0.8, value.Categories[0].AverageConfidence);
        Assert.Equal("研发", Assert.Single(value.Departments).Department);
        Assert.Equal(8, Assert.Single(value.Roles).UserCount);
        Assert.Empty(value.Users);
    }

    [Fact]
    public void PageStylesAreBoundedResponsiveAndKeepLongTextContained()
    {
        var css = Read("Pages", "AdminWorkDistribution.razor.css");
        Assert.Contains("max-width: 1540px", css, StringComparison.Ordinal);
        Assert.Contains("table-layout: fixed", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1280px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 680px)", css, StringComparison.Ordinal);
        Assert.Contains("color-mix(in srgb, var(--surface)", css, StringComparison.Ordinal);
        Assert.Contains(".department-distribution-section", css, StringComparison.Ordinal);
        Assert.Contains(".person-heading > span:last-child", css, StringComparison.Ordinal);
    }

    private static void AssertRequest(CapturedRequest request, HttpMethod method, string path, params string[] queryParts)
    {
        Assert.Equal(method, request.Method);
        Assert.Equal(path, request.Uri.AbsolutePath);
        var query = Uri.UnescapeDataString(request.Uri.Query);
        foreach (var part in queryParts) Assert.Contains(part, query, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        throw new FileNotFoundException(Path.Combine(parts));
    }

    private sealed class NullJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class WorkDistributionHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var payload = path.EndsWith("/summary", StringComparison.Ordinal)
                ? """{"code":0,"message":"success","data":{"collection_status":"active","average_confidence":0.85,"confidence_sample_count":8,"privacy":{"min_sample_size":8,"min_cohort_size":7,"suppressed_users":0},"coverage":{"total_requests":12,"classified_requests":10,"unclassified_requests":2,"classified_percent":83.33},"work_related":[{"work_related":"work","requests":10,"total_tokens":500,"value":500,"percent":83.33}],"categories":[{"category":"coding","work_related":"work","requests":10,"total_tokens":500,"value":500,"percent":83.33}],"departments":[],"roles":[{"role":"研发","user_count":8}],"users":[]}}"""
                : path.Contains("/records", StringComparison.Ordinal) && request.Method == HttpMethod.Get
                    ? """{"code":0,"message":"success","data":{"items":[],"total":0,"page":2,"page_size":30,"pages":0}}"""
                    : path.EndsWith("/reviews", StringComparison.Ordinal) && request.Method == HttpMethod.Get
                        ? """{"code":0,"message":"success","data":{"items":[],"total":0,"page":3,"page_size":25,"pages":0}}"""
                        : """{"code":0,"message":"success","data":{"id":7,"usage_log_id":91,"user_id":17,"email":"user@example.test","proposed_work_related":"work","proposed_category":"coding","reason_code":"incorrect_category","status":"pending","created_at":"2026-08-27T00:00:00Z"}}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);
}
