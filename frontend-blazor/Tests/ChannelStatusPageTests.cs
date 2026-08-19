using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ChannelStatusPageTests
{
    [Fact]
    public void WrapperSelectsTheOfficialMonitorModeFromPublicSettings()
    {
        var page = ReadProjectFile("Pages", "ChannelStatus.razor");

        Assert.Contains("Api.GetPublicSettingsAsync", page, StringComparison.Ordinal);
        Assert.Contains("settings.ChannelMonitorMode", page, StringComparison.Ordinal);
        Assert.Contains("<ChannelStatusV1Panel", page, StringComparison.Ordinal);
        Assert.Contains("Enabled=\"@settings.ChannelMonitorEnabled\"", page, StringComparison.Ordinal);
        Assert.Contains("DefaultIntervalSeconds=\"@settings.ChannelMonitorDefaultIntervalSeconds\"", page, StringComparison.Ordinal);
        Assert.Contains("<ChannelStatusV2Panel", page, StringComparison.Ordinal);
        Assert.Contains("HideThroughput=\"@settings.ChannelMonitorHideThroughput\"", page, StringComparison.Ordinal);
        Assert.Contains("IsAdmin=\"@(Auth.User?.IsAdmin == true)\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GetChannelMonitorsRawAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonElement", page, StringComparison.Ordinal);
    }

    [Fact]
    public void V1PanelMatchesTheOfficialCardsTimelineWindowsAndDetailFlow()
    {
        var panel = ReadProjectFile("Components", "ChannelStatusV1Panel.razor");
        var client = ReadProjectFile("Services", "ApiClient.cs");

        foreach (var text in new[]
                 {
                     "7 天", "15 天", "30 天", "对话延迟", "端点 PING", "可用性", "近 60 次记录",
                     "自动刷新间隔", "最新状态", "7 天可用率", "15 天可用率", "30 天可用率", "7 天平均延迟"
                 })
        {
            Assert.Contains(text, panel, StringComparison.Ordinal);
        }

        Assert.Contains("Api.GetChannelMonitorsAsync", panel, StringComparison.Ordinal);
        Assert.Contains("Api.GetChannelMonitorStatusAsync", panel, StringComparison.Ordinal);
        Assert.Contains("PeriodicTimer", panel, StringComparison.Ordinal);
        Assert.Contains("if (Enabled) StartAutoRefresh()", panel, StringComparison.Ordinal);
        Assert.Contains("<AppModal", panel, StringComparison.Ordinal);
        Assert.Contains("TimelineBars", panel, StringComparison.Ordinal);
        Assert.Contains("/channel-monitors", client, StringComparison.Ordinal);
        Assert.Contains("/channel-monitors/{id}/status", client, StringComparison.Ordinal);
        Assert.DoesNotContain("GetChannelMonitorsRawAsync", client, StringComparison.Ordinal);
    }

    [Fact]
    public void V2PanelSwitchesAdminAndUserReadScopesAndProvidesTheOfficialDrilldownSurface()
    {
        var panel = ReadProjectFile("Components", "ChannelStatusV2Panel.razor");
        var client = ReadProjectFile("Services", "ApiClient.cs");

        foreach (var range in new[] { "90 分钟", "24 小时", "7 天", "30 天" })
            Assert.Contains(range, panel, StringComparison.Ordinal);
        foreach (var text in new[]
                 {
                     "FilterLabel(\"平台\"", "FilterLabel(\"分组\"", "FilterLabel(\"模型\"", "：全部", "清除筛选", "按平台", "健康色块矩阵",
                     "趋势图", "成功率", "TTFT P50", "缓存率", "模型", "错误", "用户", "历史数据回填"
                 })
        {
            Assert.Contains(text, panel, StringComparison.Ordinal);
        }

        foreach (var method in new[]
                 {
                     "GetUserChannelMonitorV2DimensionsAsync", "GetUserChannelMonitorV2SnapshotAsync",
                     "GetUserChannelMonitorV2MatrixAsync", "GetUserChannelMonitorV2ModelsAsync",
                     "GetUserChannelMonitorV2ErrorsAsync", "GetUserChannelMonitorV2UsersAsync"
                 })
        {
            Assert.Contains($"Api.{method}", panel, StringComparison.Ordinal);
            Assert.Contains(method, client, StringComparison.Ordinal);
        }

        foreach (var method in new[]
                 {
                     "GetChannelMonitorV2DimensionsAsync", "GetChannelMonitorV2SnapshotAsync",
                     "GetChannelMonitorV2MatrixAsync", "GetChannelMonitorV2ModelsAsync",
                     "GetChannelMonitorV2ErrorsAsync", "GetChannelMonitorV2UsersAsync"
                 })
        {
            Assert.Contains($"Api.{method}", panel, StringComparison.Ordinal);
            Assert.Contains(method, client, StringComparison.Ordinal);
        }

        Assert.Contains("admin: false", client, StringComparison.Ordinal);
        Assert.Contains("group_by", client, StringComparison.Ordinal);
        Assert.DoesNotContain("/admin/channel-monitor-v2", panel, StringComparison.Ordinal);
        Assert.Contains("private Task<ChannelMonitorV2DimensionsDto> GetDimensionsAsync", panel, StringComparison.Ordinal);
        Assert.Contains("IsAdmin", panel, StringComparison.Ordinal);
        Assert.Contains("HideThroughput", panel, StringComparison.Ordinal);
        Assert.Contains("IsAdmin || !HideThroughput", panel, StringComparison.Ordinal);
        Assert.Contains("RestoreQueryState", panel, StringComparison.Ordinal);
        Assert.Contains("SyncQueryState", panel, StringComparison.Ordinal);
        foreach (var key in new[] { "range", "platform", "group", "model", "group_by", "health_mode", "trend_view", "tab" })
            Assert.Contains($"\"{key}\"", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedContractsDeserializeOfficialV1AndV2Payloads()
    {
        const string v1Json = """
            {
              "items": [{
                "id": 7,
                "name": "OpenAI 主渠道",
                "provider": "openai",
                "group_name": "默认分组",
                "primary_model": "gpt-5",
                "primary_status": "operational",
                "primary_latency_ms": 1234,
                "primary_ping_latency_ms": 88,
                "availability_7d": 99.95,
                "extra_models": [{ "model": "gpt-5-mini", "status": "degraded", "latency_ms": 1600 }],
                "timeline": [{
                  "status": "operational",
                  "latency_ms": 1234,
                  "ping_latency_ms": 88,
                  "checked_at": "2026-08-15T01:00:00Z"
                }]
              }]
            }
            """;
        const string v2Json = """
            {
              "config": { "version": 3, "enabled": true, "refresh_interval_seconds": 120 },
              "coverage": {
                "data_through": "2026-08-15T01:00:00Z",
                "computed_at": "2026-08-15T01:01:00Z",
                "coverage_complete": false,
                "bucket_seconds": 300,
                "bootstrap": { "active": true, "progress_percent": 42 }
              },
              "metrics": {
                "request_count": 100,
                "success_requests": 90,
                "error_requests": 10,
                "error_rate": 0.1,
                "success_rate": 0.9,
                "cache_rate": 0.25,
                "cache_rate_numerator": 25,
                "cache_rate_denominator": 100,
                "upstream_affected_requests": 8,
                "upstream_attempt_count": 109,
                "rpm": 12.5,
                "tpm": 600,
                "ttft": { "sample_count": 90, "p50_ms": 800, "p90_ms": 1400, "avg_ms": 920.5 }
              },
              "health": {
                "overall": "warning",
                "error_rate": "warning",
                "ttft": "healthy",
                "cache": "healthy",
                "score": 73,
                "error_rate_score": 60,
                "ttft_score": 90,
                "cache_score": 80,
                "minimum_sample": 50
              },
              "trend": [{
                "bucket_start": "2026-08-15T00:55:00Z",
                "metrics": { "request_count": 10, "error_rate": 0.1, "cache_rate": 0.2, "ttft": { "p50_ms": 750 } },
                "health": { "overall": "healthy", "score": 90 }
              }]
            }
            """;

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var v1 = JsonSerializer.Deserialize<UserMonitorListResponseDto>(v1Json, options);
        var v2 = JsonSerializer.Deserialize<ChannelMonitorV2SnapshotDto>(v2Json, options);

        Assert.NotNull(v1);
        Assert.Single(v1.Items);
        Assert.Equal("OpenAI 主渠道", v1.Items[0].Name);
        Assert.Equal(99.95, v1.Items[0].Availability7d);
        Assert.Equal(88, v1.Items[0].PrimaryPingLatencyMs);
        Assert.Single(v1.Items[0].Timeline);
        Assert.NotNull(v2);
        Assert.Equal(120, v2.Config.RefreshIntervalSeconds);
        Assert.Equal(42, v2.Coverage.Bootstrap?.ProgressPercent);
        Assert.Equal(25, v2.Metrics.CacheRateNumerator);
        Assert.Equal(109, v2.Metrics.UpstreamAttemptCount);
        Assert.Equal(73, v2.Health.Score);
        Assert.Single(v2.Trend);
        Assert.Equal(750, v2.Trend[0].Metrics.Ttft.P50Ms);
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var pageMarker = Path.Combine(directory.FullName, "Pages", "ChannelStatus.razor");
            if (File.Exists(pageMarker))
                return File.ReadAllText(Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray()));
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Blazor frontend project.");
    }
}
