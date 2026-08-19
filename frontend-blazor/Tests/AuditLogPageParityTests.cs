using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AuditLogPageParityTests
{
    [Fact]
    public void PageIncludesAllOfficialServerFilters()
    {
        var page = Read("Pages", "AuditLogs.razor");

        foreach (var value in new[]
                 {
                     "全文搜索", "操作者邮箱", "动作", "客户端 IP", "HTTP 方法", "认证方式", "执行结果", "时间范围",
                     "最近 30 分钟", "最近 1 小时", "最近 6 小时", "最近 24 小时", "最近 7 天", "最近 30 天", "自定义范围"
                 })
        {
            Assert.Contains(value, page, StringComparison.Ordinal);
        }

        Assert.Contains("Api.GetAuditLogsAsync(page, pageSize", page, StringComparison.Ordinal);
        Assert.Contains("ToRfc3339", page, StringComparison.Ordinal);
    }

    [Fact]
    public void PageUsesServerPaginationAndFullDetailDialog()
    {
        var page = Read("Pages", "AuditLogs.razor");

        Assert.Contains("logsPage.Total", page, StringComparison.Ordinal);
        Assert.Contains("logsPage.Pages", page, StringComparison.Ordinal);
        Assert.Contains("PageSizeChangedAsync", page, StringComparison.Ordinal);
        Assert.Contains("Api.GetAuditLogAsync", page, StringComparison.Ordinal);
        Assert.Contains("请求正文（已脱敏）", page, StringComparison.Ordinal);
        Assert.Contains("附加信息", page, StringComparison.Ordinal);
        Assert.Contains("User-Agent", page, StringComparison.Ordinal);
        Assert.Contains("CredentialMasked", page, StringComparison.Ordinal);
        Assert.Contains("Request ID", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearAllRequiresFreshTotpAndPreservesTraceMessage()
    {
        var page = Read("Pages", "AuditLogs.razor");

        Assert.Contains("Api.GetTotpStatusAsync", page, StringComparison.Ordinal);
        Assert.Contains("status.FeatureEnabled", page, StringComparison.Ordinal);
        Assert.Contains("status.Enabled", page, StringComparison.Ordinal);
        Assert.Contains("ValidTotpCode", page, StringComparison.Ordinal);
        Assert.Contains("Api.ClearAuditLogsAsync(totpCode)", page, StringComparison.Ordinal);
        Assert.Contains("不能复用已有 step-up 状态", page, StringComparison.Ordinal);
        Assert.Contains("保留本次清空留痕", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiClientUsesOfficialAuditEndpointsAndQueryNames()
    {
        var client = Read("Services", "ApiClient.cs");

        Assert.Contains("/admin/audit-logs?", client, StringComparison.Ordinal);
        Assert.Contains("/admin/audit-logs/{id}", client, StringComparison.Ordinal);
        Assert.Contains("/admin/audit-logs/clear", client, StringComparison.Ordinal);
        foreach (var query in new[] { "q", "actor_email", "action", "client_ip", "method", "auth_method", "success", "start_time", "end_time" })
        {
            Assert.Contains($"\"{query}\"", client, StringComparison.Ordinal);
        }
        Assert.Contains("actor_user_id=", client, StringComparison.Ordinal);
        Assert.Contains("totp_code = totpCode.Trim()", client, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditDtoMatchesCompleteGoWireContract()
    {
        var json = """
                   {
                     "id": 9,
                     "created_at": "2026-08-14T08:00:00Z",
                     "actor_user_id": 3,
                     "actor_email": "admin@example.com",
                     "actor_role": "admin",
                     "auth_method": "jwt",
                     "credential_masked": "abcdef****wxyz",
                     "action": "admin.accounts.update",
                     "method": "PUT",
                     "path": "/api/v1/admin/accounts/1",
                     "request_id": "request-1",
                     "client_ip": "127.0.0.1",
                     "user_agent": "browser",
                     "request_body": "{\"api_key\":\"***\"}",
                     "status_code": 200,
                     "latency_ms": 12,
                     "extra": { "result": "success" }
                   }
                   """;

        var item = JsonSerializer.Deserialize<AuditLogDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(item);
        Assert.Equal(3, item.ActorUserId);
        Assert.Equal("admin", item.ActorRole);
        Assert.Equal("jwt", item.AuthMethod);
        Assert.Equal("127.0.0.1", item.ClientIp);
        Assert.Equal("browser", item.UserAgent);
        Assert.Equal("success", item.Extra["result"].GetString());
    }

    [Fact]
    public void PageHasResponsiveOfficialLikeLayout()
    {
        var css = Read("Pages", "AuditLogs.razor.css");

        Assert.Contains(".audit-filter-grid", css, StringComparison.Ordinal);
        Assert.Contains(".audit-table", css, StringComparison.Ordinal);
        Assert.Contains(".detail-overview", css, StringComparison.Ordinal);
        Assert.Contains(".status-pill", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1080px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 700px)", css, StringComparison.Ordinal);
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
