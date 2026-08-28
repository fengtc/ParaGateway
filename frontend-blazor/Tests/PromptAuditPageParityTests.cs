using System.Reflection;
using System.Text.Json;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class PromptAuditPageParityTests
{
    [Fact]
    public void PageMatchesOfficialRuntimeAndTabStructure()
    {
        var page = Read("Pages", "AdminPromptAudit.razor");

        Assert.Contains("@page \"/admin/prompt-audit\"", page, StringComparison.Ordinal);
        Assert.Contains("private string activeTab = \"events\"", page, StringComparison.Ordinal);
        Assert.Contains("运行概览", page, StringComparison.Ordinal);
        Assert.Contains("同步 Guard 指标", page, StringComparison.Ordinal);
        Assert.Contains("runtime.ActiveConfigVersion", page, StringComparison.Ordinal);
        Assert.Contains("runtime.ExpectedConfigVersion", page, StringComparison.Ordinal);
        Assert.Contains("runtime.GuardMetrics.LatencyP95Ms", page, StringComparison.Ordinal);
        Assert.Contains("runtime.Endpoints", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationIncludesEndpointPoolPoliciesAndAllScanners()
    {
        var page = Read("Pages", "AdminPromptAudit.razor");

        Assert.Contains("审计池", page, StringComparison.Ordinal);
        Assert.Contains("Api.ProbePromptAuditEndpointAsync", page, StringComparison.Ordinal);
        Assert.Contains("expected_config_version", page, StringComparison.Ordinal);
        Assert.Contains("blocking_latest_turn_only", page, StringComparison.Ordinal);
        Assert.Contains("store_pass_events", page, StringComparison.Ordinal);
        Assert.Contains("GetPromptAuditGroupsAsync", page, StringComparison.Ordinal);

        foreach (var scanner in new[]
                 {
                     "violent", "non_violent_illegal_acts", "sexual_content_or_sexual_acts", "pii",
                     "suicide_and_self_harm", "unethical_acts", "politically_sensitive_topics",
                     "copyright_violation", "jailbreak"
                 })
        {
            Assert.Contains($"(\"{scanner}\"", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EventWorkspaceIncludesOfficialFiltersDetailsAndPaging()
    {
        var page = Read("Pages", "AdminPromptAudit.razor");

        foreach (var label in new[]
                 {
                     "判定", "风险等级", "入口", "分组 ID", "用户 ID", "API Key ID", "Request ID",
                     "Prompt SHA-256", "关键词", "开始时间", "结束时间"
                 })
        {
            Assert.Contains(label, page, StringComparison.Ordinal);
        }

        Assert.Contains("GetPromptAuditEventAsync", page, StringComparison.Ordinal);
        Assert.Contains("审计摘要", page, StringComparison.Ordinal);
        Assert.Contains("具体风险", page, StringComparison.Ordinal);
        Assert.Contains("技术信息", page, StringComparison.Ordinal);
        Assert.Contains("完整提示词（未脱敏）", page, StringComparison.Ordinal);
        Assert.Contains("PageSizeChangedAsync", page, StringComparison.Ordinal);
    }

    [Fact]
    public void DestructiveEventActionsUsePreviewAndHighWaterConfirmation()
    {
        var page = Read("Pages", "AdminPromptAudit.razor");

        Assert.Contains("BatchDeletePromptAuditEventsAsync", page, StringComparison.Ordinal);
        Assert.Contains("PreviewPromptAuditDeleteAsync", page, StringComparison.Ordinal);
        Assert.Contains("DeletePromptAuditEventsByFilterAsync", page, StringComparison.Ordinal);
        Assert.Contains("服务端快照匹配", page, StringComparison.Ordinal);
        Assert.Contains("快照最大事件 ID", page, StringComparison.Ordinal);
        Assert.Contains("Filter SHA-256", page, StringComparison.Ordinal);
        Assert.Contains("预览后产生的新事件会保留", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiUsesAllOfficialRoutesAndTypedFilterPayload()
    {
        var client = Read("Services", "ApiClient.cs");
        foreach (var route in new[]
                 {
                     "/admin/prompt-audit/config", "/admin/prompt-audit/runtime",
                     "/admin/prompt-audit/endpoints/probe", "/admin/prompt-audit/events",
                     "/admin/prompt-audit/events/batch-delete", "/admin/prompt-audit/events/delete-preview",
                     "/admin/prompt-audit/events/delete-by-filter"
                 })
        {
            Assert.Contains(route, client, StringComparison.Ordinal);
        }

        var builder = typeof(ApiClient).GetMethod(
            "BuildPromptAuditFilterPayload",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(builder);

        var payload = Assert.IsType<Dictionary<string, object>>(builder.Invoke(null,
        [
            new PromptAuditEventFiltersDto
            {
                Decision = " flag ", GroupId = "42", UserId = string.Empty, ApiKeyId = "invalid",
                Keyword = " abuse ", StartAt = "2026-08-01T00:00:00Z", EndAt = "2026-08-02T00:00:00Z"
            }
        ]));

        Assert.Equal("flag", payload["decision"]);
        Assert.Equal(42L, payload["group_id"]);
        Assert.Equal("abuse", payload["keyword"]);
        Assert.DoesNotContain("user_id", payload.Keys);
        Assert.DoesNotContain("api_key_id", payload.Keys);

        var json = JsonSerializer.Serialize(payload);
        Assert.Contains("\"group_id\":42", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"group_id\":\"42\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PageHasResponsiveOfficialLikeLayout()
    {
        var page = Read("Pages", "AdminPromptAudit.razor");
        var css = Read("Pages", "AdminPromptAudit.razor.css");

        Assert.Contains("class=\"prompt-heading\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"prompt-actions\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"overview-grid\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"prompt-card runtime-section\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"prompt-card events-section\"", page, StringComparison.Ordinal);
        Assert.Contains(".prompt-heading", css, StringComparison.Ordinal);
        Assert.Contains(".overview-card", css, StringComparison.Ordinal);
        Assert.Contains(".runtime-summary", css, StringComparison.Ordinal);
        Assert.Contains(".endpoint-table", css, StringComparison.Ordinal);
        Assert.Contains(".event-filters", css, StringComparison.Ordinal);
        Assert.Contains("table-layout: fixed", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", css, StringComparison.Ordinal);
        Assert.Contains(".events-section ::deep(.state-panel)", css, StringComparison.Ordinal);
        Assert.DoesNotContain("width: min(100%, 1600px)", css, StringComparison.Ordinal);
        Assert.Contains(".save-bar", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1180px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 760px)", css, StringComparison.Ordinal);
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

        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}.");
    }
}
