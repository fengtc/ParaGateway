using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class OpsPageParityTests
{
    [Fact]
    public void OpsPageMatchesOfficialToolbarAndOverviewSurface()
    {
        var markup = Read("Pages", "AdminOps.razor");

        foreach (var text in new[] { "全部平台", "全部分组", "自定义", "预警规则", "设置", "实时信息", "健康评分", "请求错误", "上游错误" })
        {
            Assert.Contains(text, markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OpsPageContainsOfficialAnalysisAndOperationalSections()
    {
        var markup = Read("Pages", "AdminOps.razor");

        foreach (var text in new[] { "并发 / 排队", "账号切换率趋势", "吞吐趋势", "请求时长分布", "完整响应耗时（E2E）", "首 Token 延迟（TTFT）", "错误分布", "错误趋势", "OpenAI Token 请求统计", "告警事件", "系统日志" })
        {
            Assert.Contains(text, markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OpsPageExposesLatencySplitAndModelAccountFilters()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var client = Read("Services", "ApiClient.cs");
        var dtoSource = Read("Models", "AdminDtos.cs");
        foreach (var text in new[] { "ops-model-filter", "ModelChangedAsync", "AccountChangedAsync", "FilteredAccounts", "model", "account_id", "duration_buckets", "ttft_buckets" })
        {
            Assert.Contains(text, markup + client + dtoSource, StringComparison.Ordinal);
        }
        Assert.Contains("latency.EffectiveDurationTotalRequests <= 0", markup, StringComparison.Ordinal);
        Assert.Contains("latency.TtftTotalRequests <= 0", markup, StringComparison.Ordinal);
        Assert.Contains("DurationBucketMax <= 0", markup, StringComparison.Ordinal);
        Assert.Contains("TtftBucketMax <= 0", markup, StringComparison.Ordinal);
        Assert.Contains("value <= 0 || max <= 0 ? 0", markup, StringComparison.Ordinal);
        Assert.Contains("string.Equals(x.Platform, platform, StringComparison.OrdinalIgnoreCase)", markup, StringComparison.Ordinal);
        Assert.Contains("accountId.HasValue ? \"指定账号\"", markup, StringComparison.Ordinal);
        Assert.Contains("Where(x => x.AccountId == accountId.Value)", markup, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<OpsLatencyHistogramDto>(
            """
            {
              "total_requests": 4,
              "duration_total_requests": 4,
              "duration_buckets": [{"range":"0-1000ms","count":3}],
              "ttft_total_requests": 2,
              "ttft_buckets": [{"range":"0-1000ms","count":2}]
            }
            """);
        Assert.NotNull(dto);
        Assert.Equal(4, dto!.EffectiveDurationTotalRequests);
        Assert.Single(dto.EffectiveDurationBuckets);
        Assert.Equal(2, dto.TtftTotalRequests);
        Assert.Single(dto.EffectiveTtftBuckets);
    }

    [Fact]
    public void OpsLatencyDtoTreatsNullBucketArraysAsEmpty()
    {
        var dto = JsonSerializer.Deserialize<OpsLatencyHistogramDto>(
            """{"total_requests":0,"buckets":null,"duration_buckets":null,"ttft_buckets":null}""");

        Assert.NotNull(dto);
        Assert.Empty(dto!.EffectiveDurationBuckets);
        Assert.Empty(dto.EffectiveTtftBuckets);
    }

    [Fact]
    public void OpsTtftDrilldownUsesFirstTokenSortAndGroupSelectionClearsInvalidAccount()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var backendModel = ReadBackend("internal", "service", "ops_request_details.go");
        var backendRepository = ReadBackend("internal", "repository", "ops_repo_request_details.go");

        Assert.Contains("OpenRequestsAsync(\"all\", \"first_token_desc\")", markup, StringComparison.Ordinal);
        Assert.Contains("value=\"first_token_desc\"", markup, StringComparison.Ordinal);
        Assert.Contains("ClearAccountOutsideCurrentFilters();", markup, StringComparison.Ordinal);
        Assert.Contains("SelectBreakdownPlatformAsync(string value) { platform = value; groupId = null; ClearAccountOutsideCurrentFilters();", markup, StringComparison.Ordinal);
        Assert.Matches("groupId = AlertDimensionLong\\(selectedAlertEvent, \\\"group_id\\\"\\);\\s+ClearAccountOutsideCurrentFilters\\(\\);", markup);
        Assert.Contains("!groupId.HasValue || (account.GroupIds?.Contains(groupId.Value) ?? false)", markup, StringComparison.Ordinal);
        Assert.Contains("FirstTokenMs", markup, StringComparison.Ordinal);
        Assert.Contains("first_token_ms", backendModel, StringComparison.Ordinal);
        Assert.Contains("ORDER BY first_token_ms DESC NULLS LAST", backendRepository, StringComparison.Ordinal);
        Assert.Contains("ORDER BY created_at ASC", backendRepository, StringComparison.Ordinal);
        Assert.Contains("ORDER BY duration_ms ASC NULLS LAST", backendRepository, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsPageConnectsDrilldownsRulesSettingsAndLogs()
    {
        var markup = Read("Pages", "AdminOps.razor");

        foreach (var method in new[] { "GetAdminOpsRequestDetailsAsync", "GetAdminOpsErrorsAsync", "GetAdminOpsAlertRulesAsync", "CreateAdminOpsAlertRuleAsync", "UpdateAdminOpsAdvancedSettingsAsync", "UpdateAdminOpsMetricThresholdsAsync", "GetAdminOpsSystemLogsAsync", "CleanupAdminOpsSystemLogsAsync" })
        {
            Assert.Contains($"Api.{method}", markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OpsClientUsesOfficialGoEndpoints()
    {
        var client = Read("Services", "ApiClient.cs");

        foreach (var endpoint in new[] { "/admin/ops/dashboard/snapshot-v2", "/admin/ops/dashboard/latency-histogram", "/admin/ops/dashboard/error-distribution", "/admin/ops/concurrency", "/admin/ops/account-availability", "/admin/ops/realtime-traffic", "/admin/ops/alert-rules", "/admin/ops/alert-events", "/admin/ops/system-logs", "/admin/ops/runtime/logging" })
        {
            Assert.Contains(endpoint, client, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OpsPageHasResponsiveScopedStyles()
    {
        var css = Read("Pages", "AdminOps.razor.css");
        Assert.Contains(".ops-overview-grid", css, StringComparison.Ordinal);
        Assert.Contains(".ops-analysis-row", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 640px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsPageSynchronizesOfficialRouteStateAndFullscreenBehavior()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var script = Read("wwwroot", "js", "paragateway.js");

        Assert.Contains("Api.GetAdminSettingsTypedAsync", markup, StringComparison.Ordinal);
        Assert.Contains("OpsMonitoringEnabled", markup, StringComparison.Ordinal);
        Assert.Contains("Navigation.NavigateTo(\"/admin/settings\", replace: true)", markup, StringComparison.Ordinal);
        Assert.Contains("Navigation.LocationChanged += OnLocationChanged", markup, StringComparison.Ordinal);
        Assert.Contains("Navigation.LocationChanged -= OnLocationChanged", markup, StringComparison.Ordinal);
        foreach (var key in new[] { "\"tr\"", "\"platform\"", "\"group_id\"", "\"model\"", "\"account_id\"", "\"mode\"", "\"fullscreen\"", "\"open_error_details\"", "\"error_type\"", "\"alert_rule_id\"", "\"open_alert_rules\"" })
        {
            Assert.Contains(key, markup, StringComparison.Ordinal);
        }
        Assert.Contains("[JSInvokable]", markup, StringComparison.Ordinal);
        Assert.Contains("ExitFullscreenFromKeyboard", markup, StringComparison.Ordinal);
        Assert.Contains("paraGateway.registerEscapeHandler", markup, StringComparison.Ordinal);
        Assert.Contains("paraGateway.unregisterEscapeHandler", markup, StringComparison.Ordinal);
        Assert.Contains("gateway.registerEscapeHandler", script, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener('keydown'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsPageUsesIndependentFiveHourSwitchTrendAndOfficialThroughputUnits()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var css = Read("Pages", "AdminOps.razor.css");

        Assert.Contains("switchTrendEnd.AddHours(-5)", markup, StringComparison.Ordinal);
        Assert.Contains("Api.GetAdminOpsThroughputTrendAsync(\"custom\"", markup, StringComparison.Ordinal);
        Assert.Contains("x.Qps, x.Tps / 1000d", markup, StringComparison.Ordinal);
        Assert.Contains("Name=\"QPS\"", markup, StringComparison.Ordinal);
        Assert.Contains("Name=\"TPS/1K\"", markup, StringComparison.Ordinal);
        Assert.Contains("ThroughputTrend.TopGroups", markup, StringComparison.Ordinal);
        Assert.Contains("ThroughputTrend.ByPlatform", markup, StringComparison.Ordinal);
        Assert.Contains(".ops-breakdown-chips", css, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsChartsKeepLegendsOutsideTheChartCanvases()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var css = Read("Pages", "AdminOps.razor.css");

        Assert.Equal(3, markup.Split("<DxChartLegend Visible=\"false\" />", StringSplitOptions.None).Length - 1);
        Assert.Contains("aria-label=\"吞吐趋势图例\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"错误趋势图例\"", markup, StringComparison.Ordinal);
        Assert.Contains(".ops-chart-layout", css, StringComparison.Ordinal);
        Assert.Contains(".ops-chart-legend", css, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap;", css, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsErrorTrendUsesOfficialExcluding429And529FieldName()
    {
        var point = JsonSerializer.Deserialize<OpsErrorPointDto>("""{"upstream_error_count_excl_429_529":7}""");

        Assert.NotNull(point);
        Assert.Equal(7, point.UpstreamErrorCountExcl429529);
    }

    [Fact]
    public void OpsRequestAndErrorDrilldownsUseTypedFiltersAndExactDetailEndpoints()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var client = Read("Services", "ApiClient.cs");

        Assert.Contains("new OpsRequestDetailsQueryDto", markup, StringComparison.Ordinal);
        Assert.Contains("new OpsErrorListQueryDto", markup, StringComparison.Ordinal);
        Assert.Contains("StartTime = timeRange == \"custom\" ? customStart : null", markup, StringComparison.Ordinal);
        Assert.Contains("EndTime = timeRange == \"custom\" ? customEnd : null", markup, StringComparison.Ordinal);
        Assert.Contains("Api.GetAdminOpsErrorDetailAsync", markup, StringComparison.Ordinal);
        Assert.Contains("Api.GetAdminOpsCorrelatedUpstreamErrorsAsync", markup, StringComparison.Ordinal);
        Assert.Contains("CopyRequestIdAsync", markup, StringComparison.Ordinal);
        Assert.Contains("/admin/ops/{resource}/{id}", client, StringComparison.Ordinal);
        Assert.Contains("/admin/ops/request-errors/{requestErrorId}/upstream-errors", client, StringComparison.Ordinal);
        Assert.Contains("include_detail=1", client, StringComparison.Ordinal);
    }

    [Fact]
    public void V0178OpsFixesCustomErrorTimeCategoriesAndEmptyWindowSla()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var client = Read("Services", "ApiClient.cs");

        Assert.Contains("private bool HasSlaSample => overview.RequestCountSla > 0", markup, StringComparison.Ordinal);
        Assert.Contains("HasSlaSample ? Percent(overview.Sla, 3) : \"-\"", markup, StringComparison.Ordinal);
        Assert.Contains("HasSlaSample ? ThresholdTone", markup, StringComparison.Ordinal);
        Assert.Contains("BuildErrorDistributionRows(errorDistribution.Items)", markup, StringComparison.Ordinal);
        foreach (var label in new[] { "上游", "客户端", "系统", "其他" })
        {
            Assert.Contains($"new ErrorDistributionRow(\"{label}\"", markup, StringComparison.Ordinal);
        }
        Assert.Contains("string.Equals(timeRange, \"custom\"", client, StringComparison.Ordinal);
        Assert.Contains("AddOpsQuery(query, \"time_range\", \"1h\")", client, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpsErrorCustomTimeUsesExplicitBoundsAndFallsBackToOneHour()
    {
        var handler = new OpsQueryHandler();
        var api = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());
        var start = new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.FromHours(8));
        var end = start.AddHours(2);

        await api.GetAdminOpsErrorsAsync("request", new OpsErrorListQueryDto
        {
            TimeRange = "custom",
            StartTime = start,
            EndTime = end
        });

        Assert.Contains("start_time=", handler.LastQuery, StringComparison.Ordinal);
        Assert.Contains("end_time=", handler.LastQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("time_range=", handler.LastQuery, StringComparison.Ordinal);

        await api.GetAdminOpsErrorsAsync("request", new OpsErrorListQueryDto { TimeRange = "custom" });

        Assert.Contains("time_range=1h", handler.LastQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("time_range=custom", handler.LastQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpsDashboardQueryCarriesModelAndAccountFilters()
    {
        var handler = new OpsQueryHandler();
        var api = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());

        await api.GetAdminOpsLatencyHistogramAsync("1h", "openai", 12, "raw", model: "gpt-5.6-sol", accountId: 34);

        Assert.Contains("platform=openai", handler.LastQuery, StringComparison.Ordinal);
        Assert.Contains("group_id=12", handler.LastQuery, StringComparison.Ordinal);
        Assert.Contains("model=gpt-5.6-sol", handler.LastQuery, StringComparison.Ordinal);
        Assert.Contains("account_id=34", handler.LastQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpsRealtimeQueryCarriesModelAndAccountFilters()
    {
        var handler = new OpsQueryHandler();
        var api = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());

        await api.GetAdminOpsRealtimeTrafficAsync("5min", "openai", 12, "gpt-5.6-sol", 34);

        Assert.Contains("window=5min", handler.LastQuery, StringComparison.Ordinal);
        Assert.Contains("platform=openai", handler.LastQuery, StringComparison.Ordinal);
        Assert.Contains("group_id=12", handler.LastQuery, StringComparison.Ordinal);
        Assert.Contains("model=gpt-5.6-sol", handler.LastQuery, StringComparison.Ordinal);
        Assert.Contains("account_id=34", handler.LastQuery, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsSystemLogsExposeOfficialFiltersHealthAndCleanupScope()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var client = Read("Services", "ApiClient.cs");

        foreach (var field in new[] { "logHost", "logLevel", "logComponent", "logRequestId", "logClientRequestId", "logUserId", "logApiKeyId", "logAccountId", "logPlatform", "logModel", "logSearch" })
        {
            Assert.Contains(field, markup, StringComparison.Ordinal);
        }
        foreach (var health in new[] { "QueueDepth", "QueueCapacity", "WrittenCount", "DroppedCount", "WriteFailedCount", "LastError" })
        {
            Assert.Contains($"logHealth.{health}", markup, StringComparison.Ordinal);
        }
        foreach (var queryName in new[] { "host", "level", "component", "request_id", "client_request_id", "user_id", "api_key_id", "account_id", "platform", "model", "q" })
        {
            Assert.Contains($"\"{queryName}\"", client, StringComparison.Ordinal);
        }
        Assert.Contains("EffectiveLogCleanupBounds", markup, StringComparison.Ordinal);
        Assert.Contains("CleanupAdminOpsSystemLogsAsync(new", markup, StringComparison.Ordinal);
        Assert.Contains("FormatSystemLogDetail", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsAlertRulesExposeEveryOfficialMetricAndRequireGroupScope()
    {
        var markup = Read("Pages", "AdminOps.razor");

        foreach (var metric in new[]
        {
            "success_rate", "error_rate", "upstream_error_rate", "cpu_usage_percent", "memory_usage_percent",
            "concurrency_queue_depth", "group_available_accounts", "group_available_ratio", "group_rate_limit_ratio",
            "account_rate_limited_count", "account_error_count", "account_error_ratio",
            "account_temp_unscheduled_count", "overload_account_count"
        })
        {
            Assert.Contains($"value=\"{metric}\"", markup, StringComparison.Ordinal);
        }
        Assert.Contains("GroupAlertMetricTypes", markup, StringComparison.Ordinal);
        Assert.Contains("ruleDraft.Filters[\"group_id\"]", markup, StringComparison.Ordinal);
        Assert.Contains("分组级指标必须选择作用分组", markup, StringComparison.Ordinal);
        Assert.Contains("alert_rule_id", markup, StringComparison.Ordinal);
        Assert.Contains("EditRule(rule)", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsAlertEventsProvideFiltersDetailHistorySilenceAndResolution()
    {
        var markup = Read("Pages", "AdminOps.razor");
        var client = Read("Services", "ApiClient.cs");

        foreach (var field in new[] { "alertTimeRange", "alertSeverity", "alertStatusFilter", "alertEmailSent", "BeforeFiredAt", "BeforeId" })
        {
            Assert.Contains(field, markup, StringComparison.Ordinal);
        }
        foreach (var call in new[] { "GetAdminOpsAlertEventAsync", "CreateAdminOpsAlertSilenceAsync", "UpdateAdminOpsAlertEventStatusAsync" })
        {
            Assert.Contains($"Api.{call}", markup, StringComparison.Ordinal);
        }
        Assert.Contains("alertHistoryRange", markup, StringComparison.Ordinal);
        Assert.Contains("AlertDimensionsSummary", markup, StringComparison.Ordinal);
        Assert.Contains("/admin/ops/alert-events/{id}", client, StringComparison.Ordinal);
        Assert.Contains("/admin/ops/alert-silences", client, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsOpenAiTokenStatsProvideOfficialRangesTopNAndPagination()
    {
        var markup = Read("Pages", "AdminOps.razor");

        foreach (var range in new[] { "30m", "1h", "1d", "15d", "30d" })
        {
            Assert.Contains($"option value=\"{range}\"", markup, StringComparison.Ordinal);
        }
        Assert.Contains("tokenViewMode", markup, StringComparison.Ordinal);
        Assert.Contains("tokenTopN", markup, StringComparison.Ordinal);
        Assert.Contains("tokenPageSize", markup, StringComparison.Ordinal);
        Assert.Contains("RequestsWithFirstToken", markup, StringComparison.Ordinal);
        Assert.Contains("Api.GetAdminOpsOpenAiTokenStatsAsync(tokenTimeRange", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsSettingsExposeReportSchedulesAndQuotaAutoPauseThresholds()
    {
        var markup = Read("Pages", "AdminOps.razor");

        foreach (var field in new[] { "DailySummaryEnabled", "DailySummarySchedule", "WeeklySummaryEnabled", "WeeklySummarySchedule", "QuotaAutoPause5HPercent", "QuotaAutoPause7DPercent" })
        {
            Assert.Contains(field, markup, StringComparison.Ordinal);
        }
        Assert.Contains("DefaultThreshold5H", markup, StringComparison.Ordinal);
        Assert.Contains("DefaultThreshold7D", markup, StringComparison.Ordinal);
        Assert.Contains("OpenAI 配额自动暂停阈值必须在 0 到 100% 之间", markup, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private static string ReadBackend(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend"));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private sealed class NullJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class OpsQueryHandler : HttpMessageHandler
    {
        public string LastQuery { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastQuery = request.RequestUri?.Query ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":0,\"message\":\"success\",\"data\":{\"items\":[],\"total\":0,\"page\":1,\"page_size\":20,\"pages\":0}}", Encoding.UTF8, "application/json")
            });
        }
    }
}
