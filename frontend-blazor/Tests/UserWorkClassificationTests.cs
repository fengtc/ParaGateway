using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class UserWorkClassificationTests
{
    [Fact]
    public void UserUsagePanelProvidesStructuredClassificationAndAppealSurface()
    {
        var page = Read("Components", "UserUsagePanel.razor");

        Assert.Contains("工作分类", page, StringComparison.Ordinal);
        Assert.Contains("ShowWorkClassificationsAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetMyWorkClassificationsAsync", page, StringComparison.Ordinal);
        Assert.Contains("CreateMyWorkClassificationAppealAsync", page, StringComparison.Ordinal);
        Assert.Contains("WorkDistributionLabels.WorkRelated(row.Classification?.WorkRelated)", page, StringComparison.Ordinal);
        Assert.Contains("WorkDistributionLabels.Category(row.Classification?.Category)", page, StringComparison.Ordinal);
        Assert.Contains("ReviewStatusLabel(row.ReviewStatus)", page, StringComparison.Ordinal);
        Assert.Contains("已有待处理申诉", page, StringComparison.Ordinal);
        Assert.Contains("appealRequest.WorkRelated != \"work\"", page, StringComparison.Ordinal);
        Assert.Contains("IsValidAppealPair", page, StringComparison.Ordinal);
        Assert.Contains("\"uncertain\" => \"unclassified\"", page, StringComparison.Ordinal);
        Assert.Contains("仅显示项目、仓库、提交类型和分类结果", page, StringComparison.Ordinal);
        Assert.Contains("不显示密钥、提示词、请求正文或完整源代码", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<textarea", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiClientUsesExactPersonalClassificationContracts()
    {
        var handler = new WorkClassificationHandler();
        var api = new ApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") },
            new NullJsRuntime());

        var page = await api.GetMyWorkClassificationsAsync(3, 50);

        var item = Assert.Single(page.Items);
        Assert.Equal(81, item.UsageLogId);
        Assert.Equal("uncertain", item.Classification?.WorkRelated);
        Assert.Equal("unclassified", item.Classification?.Category);
        Assert.Equal("pending", item.ReviewStatus);
        Assert.Equal("/api/v1/usage/work-classifications", handler.Requests[0].Uri.AbsolutePath);
        Assert.Contains("page=3", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("page_size=50", handler.Requests[0].Uri.Query, StringComparison.Ordinal);

        var appeal = await api.CreateMyWorkClassificationAppealAsync(81, new UserWorkClassificationAppealRequestDto
        {
            WorkRelated = "work",
            Category = "coding",
            ReasonCode = "missing_classification"
        });

        Assert.Equal("pending", appeal.Status);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("/api/v1/usage/work-classifications/81/appeals", handler.Requests[1].Uri.AbsolutePath);
        using var body = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal("work", body.RootElement.GetProperty("work_related").GetString());
        Assert.Equal("coding", body.RootElement.GetProperty("category").GetString());
        Assert.Equal("missing_classification", body.RootElement.GetProperty("reason_code").GetString());
        Assert.Equal(3, body.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void PersonalDtosAreStructuredAndDoNotExposeRequestContent()
    {
        var wireNames = typeof(UserWorkClassificationDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Prompt", wireNames);
        Assert.DoesNotContain("RequestBody", wireNames);
        Assert.DoesNotContain("ResponseBody", wireNames);
        Assert.DoesNotContain("SourceCode", wireNames);
        Assert.DoesNotContain("Secret", wireNames);
        Assert.Equal("非工作", WorkDistributionLabels.Category("non_work"));

        var privacy = JsonSerializer.Deserialize<WorkDistributionPrivacyDto>(
            """{"min_sample_size":5,"min_cohort_size":8,"suppressed_users":2}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(privacy);
        Assert.Equal(8, privacy.MinCohortSize);
    }

    [Fact]
    public void ClassificationTableAndAppealModalAreResponsiveAndBounded()
    {
        var css = Read("Components", "UserUsagePanel.razor.css");

        Assert.Contains(".work-classification-table { min-width: 1120px; table-layout: fixed; }", css, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden", css, StringComparison.Ordinal);
        Assert.Contains("text-overflow: ellipsis", css, StringComparison.Ordinal);
        Assert.Contains(".appeal-form-grid", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 560px)", css, StringComparison.Ordinal);
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

    private sealed class WorkClassificationHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
            var payload = request.Method == HttpMethod.Get
                ? """{"code":0,"message":"success","data":{"items":[{"usage_log_id":81,"user_id":4,"email":"user@example.test","department":"研发","role":"user","total_tokens":1200,"created_at":"2026-08-27T03:00:00Z","metadata":{"project_ref":"gateway","repository_ref":"fengtc/ParaGateway","submission_type":"coding","source":"header"},"classification":{"work_related":"uncertain","category":"unclassified","weight":1200,"confidence":0.3,"classification_source":"unclassified","classifier_version":"local-rules-v1","updated_at":"2026-08-27T03:00:01Z"},"review_status":"pending"}],"total":1,"page":3,"page_size":50,"pages":1}}"""
                : """{"code":0,"message":"success","data":{"id":9,"usage_log_id":81,"user_id":4,"email":"user@example.test","proposed_work_related":"work","proposed_category":"coding","reason_code":"missing_classification","status":"pending","requested_by":4,"created_at":"2026-08-27T03:02:00Z"}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class NullJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string Body);
}
