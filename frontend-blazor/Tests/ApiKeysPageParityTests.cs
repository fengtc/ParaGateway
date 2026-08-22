using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ApiKeysPageParityTests
{
    [Fact]
    public void UserApiKeysPageMatchesOfficialListAndNeverSwitchesToAdminMode()
    {
        var page = Read("Pages", "ApiKeys.razor");

        Assert.Contains("@page \"/keys\"", page, StringComparison.Ordinal);
        Assert.Contains("@page \"/api-keys\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthSession", page, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAdmin", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GetApiKeysAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"page-header\"", page, StringComparison.Ordinal);

        foreach (var text in new[]
                 {
                     "搜索名称或 Key", "全部分组", "无分组", "全部状态", "列设置", "创建密钥",
                     "API 密钥", "当前并发", "用量", "速率限制", "上次使用时间", "最近使用 IP",
                     "使用密钥", "导入到 CCS", "停用", "编辑", "删除"
                 })
        {
            Assert.Contains(text, page, StringComparison.Ordinal);
        }

        Assert.Contains("ApiEndpointList", page, StringComparison.Ordinal);
        Assert.Contains("api-key-hidden-columns", page, StringComparison.Ordinal);
        Assert.Contains("api-key-column-settings-version", page, StringComparison.Ordinal);
        Assert.Contains("GetMyApiKeysPageAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetMyApiKeyUsageBatchAsync", page, StringComparison.Ordinal);
        Assert.Contains("ChangeSortAsync", page, StringComparison.Ordinal);
        Assert.Contains("PageSizeChangedAsync", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiKeyEditorAndActionsCoverEveryOfficialUserOperation()
    {
        var page = Read("Pages", "ApiKeys.razor");

        foreach (var text in new[]
                 {
                     "自定义密钥", "IP 限制", "IP 白名单", "IP 黑名单", "额度限制", "已用额度",
                     "5 小时限额", "日限额", "7 天限额", "重置速率限制用量", "密钥有效期",
                     "自定义", "永久有效"
                 })
        {
            Assert.Contains(text, page, StringComparison.Ordinal);
        }

        Assert.Contains("ExpirationDays = [7, 30, 90]", page, StringComparison.Ordinal);

        foreach (var method in new[]
                 {
                     "CreateMyApiKeyAsync", "UpdateMyApiKeyAsync", "DeleteMyApiKeyAsync",
                     "SetMyApiKeyStatusAsync", "ChangeMyApiKeyGroupAsync", "ResetMyApiKeyQuotaAsync",
                     "ResetMyApiKeyRateLimitAsync"
                 })
        {
            Assert.Contains($"Api.{method}", page, StringComparison.Ordinal);
        }

        Assert.Contains("<UseApiKeyModal", page, StringComparison.Ordinal);
        Assert.Contains("ccswitch://v1/import", page, StringComparison.Ordinal);
        Assert.Contains("gpt-5.5", page, StringComparison.Ordinal);
        Assert.Contains("grok-4.5", page, StringComparison.Ordinal);
        Assert.Contains("usageAutoInterval", page, StringComparison.Ordinal);
        Assert.Contains("HideCcsImportButton", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiKeyCopyUsesHttpCompatibleClipboardFallbackAndCopiesTheFullKey()
    {
        var page = Read("Pages", "ApiKeys.razor");
        var script = Read("wwwroot", "js", "paragateway.js");
        var index = Read("wwwroot", "index.html");

        Assert.Contains("JS.InvokeAsync<bool>(\"paraGateway.copyText\", row.Key)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("navigator.clipboard.writeText\", row.Key", page, StringComparison.Ordinal);
        Assert.Contains("window.isSecureContext", script, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard.writeText(text)", script, StringComparison.Ordinal);
        Assert.Contains("document.createElement('textarea')", script, StringComparison.Ordinal);
        Assert.Contains("document.execCommand('copy')", script, StringComparison.Ordinal);
        Assert.Contains("js/paragateway.js?v=20260820-account-row-menu-a", index, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiClientUsesOfficialUserKeyContracts()
    {
        var client = Read("Services", "ApiClient.cs");
        var models = Read("Models", "Dtos.cs");

        Assert.Contains("/keys?", client, StringComparison.Ordinal);
        Assert.Contains("/keys/{Uri.EscapeDataString(id)}", client, StringComparison.Ordinal);
        Assert.Contains("/usage/dashboard/api-keys-usage", client, StringComparison.Ordinal);
        foreach (var field in new[]
                 {
                     "group_id", "custom_key", "ip_whitelist", "ip_blacklist", "quota", "expires_in_days",
                     "expires_at", "rate_limit_5h", "rate_limit_1d", "rate_limit_7d", "reset_quota",
                     "reset_rate_limit_usage"
                 })
        {
            Assert.Contains(field, client, StringComparison.Ordinal);
        }

        Assert.Contains("class ApiKeyListQuery", models, StringComparison.Ordinal);
        Assert.Contains("class ApiKeyUsageBatchDto", models, StringComparison.Ordinal);
        Assert.Contains("IValidatableObject", models, StringComparison.Ordinal);
        Assert.Contains("自定义密钥至少需要 16 个字符", models, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        throw new FileNotFoundException(string.Join('/', parts));
    }
}
