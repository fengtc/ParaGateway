using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class UserUsagePageParityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void UserPageMatchesOfficialStatisticsChartsAndFilters()
    {
        var page = ReadSource("Components", "UserUsagePanel.razor");

        Assert.Contains("总请求数", page, StringComparison.Ordinal);
        Assert.Contains("总 Token", page, StringComparison.Ordinal);
        Assert.Contains("总费用", page, StringComparison.Ordinal);
        Assert.Contains("平均耗时", page, StringComparison.Ordinal);
        Assert.Contains("DxDateRangePicker", page, StringComparison.Ordinal);
        Assert.Contains("模型分布", page, StringComparison.Ordinal);
        Assert.Contains("分组分布", page, StringComparison.Ordinal);
        Assert.Contains("端点分布", page, StringComparison.Ordinal);
        Assert.Contains("Token 使用趋势", page, StringComparison.Ordinal);
        Assert.Contains("API Key", page, StringComparison.Ordinal);
        Assert.Contains("计费类型", page, StringComparison.Ordinal);
        Assert.Contains("计费模式", page, StringComparison.Ordinal);
        Assert.Contains("导出 CSV", page, StringComparison.Ordinal);
        Assert.Contains("user-usage-hidden-columns", page, StringComparison.Ordinal);
    }

    [Fact]
    public void UserUsageChartLegendsDoNotCoverChartCanvases()
    {
        var page = ReadSource("Components", "UserUsagePanel.razor");
        var styles = ReadSource("Components", "UserUsagePanel.razor.css");

        Assert.Equal(7, page.Split("<DxChartLegend Visible=\"false\" />", StringSplitOptions.None).Length - 1);
        Assert.Contains("class=\"usage-trend-legend\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Token 使用趋势图例\"", page, StringComparison.Ordinal);
        Assert.Contains(".usage-trend-layout", styles, StringComparison.Ordinal);
        Assert.Contains(".usage-trend-legend", styles, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap;", styles, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void UserErrorViewIsFeatureGatedAndHasSafeDetailSurface()
    {
        var page = ReadSource("Components", "UserUsagePanel.razor");
        var dto = ReadSource("Models", "Dtos.cs");

        Assert.Contains("AllowUserViewErrorRequests", page, StringComparison.Ordinal);
        Assert.Contains("错误请求", page, StringComparison.Ordinal);
        Assert.Contains("全部分类", page, StringComparison.Ordinal);
        Assert.Contains("全部状态", page, StringComparison.Ordinal);
        Assert.Contains("错误请求详情", page, StringComparison.Ordinal);
        Assert.Contains("响应正文", page, StringComparison.Ordinal);
        Assert.Contains("user-usage-error-hidden-columns", page, StringComparison.Ordinal);
        Assert.Contains("allow_user_view_error_requests", dto, StringComparison.Ordinal);
        Assert.DoesNotContain("UpstreamEndpoint", nameof(UserErrorRequestDetailDto), StringComparison.Ordinal);
    }

    [Fact]
    public void ClientConnectsEveryOfficialUserUsageEndpoint()
    {
        var client = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("/groups/available", client, StringComparison.Ordinal);
        Assert.Contains("/usage?{BuildUserUsageQuery", client, StringComparison.Ordinal);
        Assert.Contains("/usage/stats?{BuildUserUsageQuery", client, StringComparison.Ordinal);
        Assert.Contains("/usage/dashboard/models?{BuildUserUsageQuery", client, StringComparison.Ordinal);
        Assert.Contains("/usage/dashboard/snapshot-v2?{BuildUserUsageQuery", client, StringComparison.Ordinal);
        Assert.Contains("/usage/errors?{BuildUserErrorQuery", client, StringComparison.Ordinal);
        Assert.Contains("/usage/errors/{id}", client, StringComparison.Ordinal);
        Assert.Contains("request_type", client, StringComparison.Ordinal);
        Assert.Contains("billing_mode", client, StringComparison.Ordinal);
        Assert.Contains("billing_type", client, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageContractPreservesOfficialBillingAndLatencyFields()
    {
        var row = JsonSerializer.Deserialize<GoUsageLog>("""
            {
              "id": 19,
              "user_id": 3,
              "api_key_id": 7,
              "request_id": "req_demo",
              "model": "gpt-5.6",
              "reasoning_effort": "high",
              "inbound_endpoint": "/v1/responses",
              "group_id": 9,
              "input_tokens": 1200,
              "output_tokens": 300,
              "cache_creation_tokens": 200,
              "cache_read_tokens": 500,
              "cache_creation_1h_tokens": 100,
              "total_cost": 0.015,
              "actual_cost": 0.009,
              "rate_multiplier": 0.6,
              "long_context_billing_applied": true,
              "billing_type": 1,
              "billing_mode": "token",
              "request_type": "stream",
              "stream": true,
              "first_token_ms": 320,
              "duration_ms": 1840,
              "ip_address": "203.0.113.10",
              "user_agent": "official-client/1.0",
              "api_key": { "id": 7, "name": "Primary", "status": "active" },
              "group": { "id": 9, "name": "OpenAI Pro", "platform": "openai" },
              "created_at": "2026-08-15T08:30:00Z"
            }
            """, Json);

        Assert.NotNull(row);
        Assert.Equal("req_demo", row.RequestId);
        Assert.Equal("high", row.ReasoningEffort);
        Assert.Equal(100, row.CacheCreation1hTokens);
        Assert.Equal(0.009, row.ActualCost);
        Assert.Equal(0.6, row.RateMultiplier);
        Assert.True(row.LongContextBillingApplied);
        Assert.Equal(320, row.FirstTokenMs);
        Assert.Equal("Primary", row.ApiKey?.Name);
        Assert.Equal("OpenAI Pro", row.Group?.Name);
    }

    [Fact]
    public void AdminAndUserRoutesUsePathInsteadOfRole()
    {
        var page = ReadSource("Pages", "Usage.razor");

        Assert.Contains("AbsolutePath.TrimEnd('/').Equals(\"/admin/usage\"", page, StringComparison.Ordinal);
        Assert.Contains("<AdminUsagePanel />", page, StringComparison.Ordinal);
        Assert.Contains("<UserUsagePanel />", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Auth.User?.IsAdmin", page, StringComparison.Ordinal);
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
