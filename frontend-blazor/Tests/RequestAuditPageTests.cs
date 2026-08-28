using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class RequestAuditPageTests
{
    [Fact]
    public void PageExposesEnterprisePolicyRecordsAndProtectedRawContent()
    {
        var page = Read("Pages", "AdminRequestAudit.razor");

        Assert.Contains("@page \"/admin/request-audit\"", page, StringComparison.Ordinal);
        foreach (var label in new[]
                 {
                     "审计记录", "审计策略", "用户 ID", "模型", "HTTP 状态", "Request ID",
                     "开始时间", "结束时间", "全部请求", "仅异常请求", "普通请求抽样",
                     "保留周期", "保存请求正文", "保存响应正文", "保存加密原文",
                     "AES-256-GCM", "标准：隐藏凭据字段", "严格：不展示任何正文预览"
                 })
        {
            Assert.Contains(label, page, StringComparison.Ordinal);
        }

        Assert.Contains("RawContentAvailable", page, StringComparison.Ordinal);
        Assert.Contains("GetRequestAuditContentAsync", page, StringComparison.Ordinal);
        Assert.Contains("VerifyTotpStepUpAsync", page, StringComparison.Ordinal);
        Assert.Contains("STEP_UP_REQUIRED", page, StringComparison.Ordinal);
        Assert.Contains("!policy.EncryptionConfigured && !policy.StoreEncryptedContent", page, StringComparison.Ordinal);
        Assert.Contains("BuildApiFilters", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiClientUsesCompleteRequestAuditContract()
    {
        var client = Read("Services", "ApiClient.cs");
        foreach (var route in new[]
                 {
                     "/admin/request-audit/policy", "/admin/request-audit/runtime",
                     "/admin/request-audit/records?", "/admin/request-audit/records/{id}",
                     "/admin/request-audit/records/{id}/content"
                 })
        {
            Assert.Contains(route, client, StringComparison.Ordinal);
        }

        foreach (var query in new[]
                 {
                     "user_id", "api_key_id", "group_id", "status_code", "request_id",
                     "model", "q", "start_at", "end_at"
                 })
        {
            Assert.Contains($"\"{query}\"", client, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DtosDefaultToDisabledPreviewOnlyAndDeserializeWireNames()
    {
        var policy = new RequestAuditPolicyDto();
        Assert.False(policy.Enabled);
        Assert.False(policy.StoreEncryptedContent);
        Assert.Equal("standard", policy.RedactionLevel);

        const string json = """
                            {
                              "id": 42,
                              "request_id": "req-42",
                              "user_id": 7,
                              "api_key_name": "desktop",
                              "status_code": 429,
                              "latency_ms": 18,
                              "request_preview": "{\"model\":\"gpt-5.4\"}",
                              "response_preview": "{\"error\":\"rate limit\"}",
                              "raw_content_available": true,
                              "request_truncated": true,
                              "created_at": "2026-08-23T00:00:00Z"
                            }
                            """;
        var item = JsonSerializer.Deserialize<RequestAuditRecordDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(item);
        Assert.Equal(42, item.Id);
        Assert.Equal("req-42", item.RequestId);
        Assert.Equal(7, item.UserId);
        Assert.Equal("desktop", item.ApiKeyName);
        Assert.Equal(429, item.StatusCode);
        Assert.True(item.RawContentAvailable);
        Assert.True(item.RequestTruncated);
    }

    [Fact]
    public void PageUsesRiskControlVisualSystemAndResponsiveOperationalLayout()
    {
        var page = Read("Pages", "AdminRequestAudit.razor");
        var css = Read("Pages", "AdminRequestAudit.razor.css");

        Assert.Contains("class=\"audit-heading\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"overview-grid\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"audit-card records-section\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"audit-card policy-section\"", page, StringComparison.Ordinal);
        Assert.Contains("display: grid; gap: 20px", css, StringComparison.Ordinal);
        Assert.Contains(".overview-card", css, StringComparison.Ordinal);
        Assert.Contains(".audit-card", css, StringComparison.Ordinal);
        Assert.DoesNotContain("max-width: 1540px", css, StringComparison.Ordinal);
        Assert.Contains("table-layout: fixed", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", css, StringComparison.Ordinal);
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
