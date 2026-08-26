using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AccountPageParityTests
{
    [Fact]
    public void AccountPageMatchesOfficialFilterAndActionSurface()
    {
        var markup = Read("Pages", "Providers.razor");

        Assert.Contains("@page \"/admin/accounts\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/admin/upstream-accounts\"", markup, StringComparison.Ordinal);
        Assert.Contains("搜索账号名称...", markup, StringComparison.Ordinal);
        Assert.Contains("全部平台", markup, StringComparison.Ordinal);
        Assert.Contains("全部类型", markup, StringComparison.Ordinal);
        Assert.Contains("全部状态", markup, StringComparison.Ordinal);
        Assert.Contains("全部Privacy状态", markup, StringComparison.Ordinal);
        Assert.Contains("全部分组", markup, StringComparison.Ordinal);
        Assert.Contains("自动刷新", markup, StringComparison.Ordinal);
        Assert.Contains("更多操作", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("compact oauth-entry", markup, StringComparison.Ordinal);
        Assert.Contains("OAuth 接入", markup, StringComparison.Ordinal);
        Assert.Contains("新建账号", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountPageContainsOfficialTableBulkAndEmptyStates()
    {
        var markup = Read("Pages", "Providers.razor");

        foreach (var text in new[] { "平台/类型", "API 地址", "认证", "容量", "用量窗口", "实时调度评分", "计费倍率", "上游声明倍率", "暂无账号", "通过官方 OAuth 接入平台账号" })
        {
            Assert.Contains(text, markup, StringComparison.Ordinal);
        }
        Assert.Contains("选择全部", markup, StringComparison.Ordinal);
        Assert.Contains("BatchDeleteAccountsAsync", markup, StringComparison.Ordinal);
        Assert.Contains("BulkSetAccountsSchedulableAsync", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OAuthAccountPlatformCellShowsSubscriptionPlanAndExpiry()
    {
        var markup = Read("Pages", "Providers.razor");
        var css = Read("Pages", "Providers.razor.css");
        var dto = Read("Models", "Dtos.cs");

        Assert.Contains("OAuthSubscriptionDisplay.From(row)", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"platform-meta-row\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"subscription-meta-row\"", markup, StringComparison.Ordinal);
        Assert.Contains("subscription-plan-@subscription.PlanTone", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"subscription-expiry\"", markup, StringComparison.Ordinal);
        Assert.Contains("@subscription.ExpiryLabel", markup, StringComparison.Ordinal);
        Assert.Contains(".subscription-plan-pro", css, StringComparison.Ordinal);
        Assert.Contains(".platform-stack > .subscription-expiry", css, StringComparison.Ordinal);
        Assert.Contains("parent_plan_type", dto, StringComparison.Ordinal);
        Assert.Contains("parent_subscription_expires_at", dto, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountToolbarAndSchedulingControlsStayAligned()
    {
        var markup = Read("Pages", "Providers.razor");
        var css = Read("Pages", "Providers.razor.css");

        Assert.Contains("class=\"search-box account-search\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"schedule-column\"", markup, StringComparison.Ordinal);
        Assert.Contains(".schedule-column .table-sort", css, StringComparison.Ordinal);
        Assert.Contains(".account-name-cell strong { overflow: hidden; max-width: 240px; text-overflow: ellipsis; font-size: .85rem; font-weight: 650; }", css, StringComparison.Ordinal);
        Assert.Contains(".auth-cell strong { color: var(--ink); font-size: .85rem; font-weight: 650; }", css, StringComparison.Ordinal);
        Assert.Contains(".schedule-switch::after", css, StringComparison.Ordinal);
        Assert.Contains("top: 3px; left: 3px; width: 16px; height: 16px", css, StringComparison.Ordinal);
        Assert.Contains(".schedule-switch.on::after { transform: translateX(16px); }", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".schedule-switch span", css, StringComparison.Ordinal);
        Assert.Contains("row.Schedulable = requested;", markup, StringComparison.Ordinal);
        Assert.Contains("var saved = await Api.SetAccountSchedulableAsync(row.Id, requested);", markup, StringComparison.Ordinal);
        Assert.Contains("row.Schedulable = saved.Schedulable;", markup, StringComparison.Ordinal);
        Assert.Contains("row.Schedulable = previous;", markup, StringComparison.Ordinal);
        Assert.Contains("margin: 0 auto", css, StringComparison.Ordinal);
        Assert.DoesNotContain(":deep(", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountRowActionMenuUsesViewportOverlayInsteadOfTableOverflow()
    {
        var markup = Read("Pages", "Providers.razor");
        var css = Read("Pages", "Providers.razor.css");
        var script = Read("wwwroot", "js", "paragateway.js");

        Assert.Contains("class=\"row-action-menu-backdrop\"", markup, StringComparison.Ordinal);
        Assert.Contains("ToggleRowMenuAsync(row, e)", markup, StringComparison.Ordinal);
        Assert.Contains("paraGateway.positionFloatingMenu", markup, StringComparison.Ordinal);
        Assert.Contains(".row-action-menu { display: grid; position: fixed;", css, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(100dvh - 16px)", css, StringComparison.Ordinal);
        Assert.Contains("gateway.positionFloatingMenu", script, StringComparison.Ordinal);
        Assert.DoesNotContain("rowMenuId == row.Id) { <div class=\"row-action-menu\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountPageConnectsOfficialDataAndRowActions()
    {
        var markup = Read("Pages", "Providers.razor");
        var statsModal = Read("Components", "AccountStatsModal.razor");

        foreach (var method in new[] { "DuplicateAccountAsync", "RecoverAccountStateAsync", "ResetAccountQuotaAsync", "ClearAccountRateLimitAsync", "GetAccountsDataAsync", "ImportAccountsDataAsync", "PreviewAccountsFromCrsAsync", "SyncAccountsFromCrsAsync" })
        {
            Assert.Contains($"Api.{method}", markup, StringComparison.Ordinal);
        }
        Assert.Contains("Api.GetAccountStatsAsync", statsModal, StringComparison.Ordinal);
        Assert.Contains("<AccountStatsModal", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("statsJson", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<pre class=\"json-preview\">", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/provider-oauth\"", markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/error-passthrough\"", markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin/tls-fingerprints\"", markup, StringComparison.Ordinal);
        Assert.Contains("Api.PreviewAccountModelsAsync", markup, StringComparison.Ordinal);
        Assert.Contains("读取平台模型", markup, StringComparison.Ordinal);
        Assert.Contains("调度负载因子（可选）", markup, StringComparison.Ordinal);
        Assert.Contains("CanRefreshToken(row)", markup, StringComparison.Ordinal);
        Assert.Contains("!CanBulkRefresh", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/provider-oauth\">重新授权", markup, StringComparison.Ordinal);
        Assert.Contains("编辑已有账号时不可更改平台或认证类型", markup, StringComparison.Ordinal);
        Assert.Contains("数值越小越优先", markup, StringComparison.Ordinal);
        Assert.Contains("启用账号调度", markup, StringComparison.Ordinal);
        Assert.Contains("SetAccountSchedulableAsync(saved.Id, form.Schedulable)", markup, StringComparison.Ordinal);
        Assert.Contains("AccountGroupSelectionPolicy.IsSelectable", markup, StringComparison.Ordinal);
        Assert.Contains("editorContext.Validate()", markup, StringComparison.Ordinal);
        Assert.Contains("editorContext.GetValidationMessages()", markup, StringComparison.Ordinal);
        Assert.Contains("@onclick=\"SubmitEditorAsync\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"submit\" form=\"account-editor\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountClientUsesOfficialGoEndpointsAndServerPaging()
    {
        var client = Read("Services", "ApiClient.cs");

        Assert.Contains("GetAccountsPageAsync", client, StringComparison.Ordinal);
        Assert.Contains("include_scheduler_score=true", client, StringComparison.Ordinal);
        Assert.Contains("/admin/accounts/{Uri.EscapeDataString(id)}/duplicate", client, StringComparison.Ordinal);
        Assert.Contains("/admin/accounts/batch-delete", client, StringComparison.Ordinal);
        Assert.Contains("/admin/accounts/bulk-update", client, StringComparison.Ordinal);
        Assert.Contains("/admin/accounts/data", client, StringComparison.Ordinal);
        Assert.Contains("/admin/accounts/sync/crs/preview", client, StringComparison.Ordinal);
        Assert.Contains("/admin/accounts/sync/crs", client, StringComparison.Ordinal);
        Assert.Contains("/admin/accounts/models/sync-upstream-preview", client, StringComparison.Ordinal);
        Assert.Contains("ParseAccountTestEvents", client, StringComparison.Ordinal);
        Assert.Contains("Dictionary<string, List<string>>", client, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountShellUsesOfficialTitleDescription()
    {
        var layout = Read("Layout", "MainLayout.razor");
        Assert.Contains("new(\"账号管理\", \"管理官方平台账号、认证凭据、分组与调度状态\")", layout, StringComparison.Ordinal);
        Assert.Contains("new(\"兼容上游连接\", \"管理独立的 OpenAI、Claude 兼容连接与调度策略\")", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeCreateModalMatchesOfficialAccountTypeSurface()
    {
        var markup = Read("Components", "NativeAccountCreateModal.razor");

        foreach (var text in new[]
        {
            "Claude Code", "Claude Console", "AWS Bedrock", "Vertex",
            "ChatGPT OAuth", "Responses API", "OAuth 授权（Gemini）",
            "API 密钥（AI Studio）", "Antigravity OAuth", "Grok OAuth",
            "GitHub Copilot", "Device OAuth"
        })
        {
            Assert.Contains(text, markup, StringComparison.Ordinal);
        }

        Assert.Contains("platform-@platform", markup, StringComparison.Ordinal);
        Assert.Contains("https://cloudcode-pa.googleapis.com", markup, StringComparison.Ordinal);
        Assert.Contains("\"antigravity\" => \"sk-...\"", markup, StringComparison.Ordinal);
        Assert.Contains("请输入 Base URL", markup, StringComparison.Ordinal);
        Assert.Contains("_ => await Api.StartOpenAIOAuthAsync()", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("StartOpenAIOAuthAsync(redirectUri)", markup, StringComparison.Ordinal);
        Assert.Contains("RedirectUri = platform == \"grok\"", markup, StringComparison.Ordinal);
        Assert.Contains("localhost:1455", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void CopilotCreateAndEditFlowsKeepTheCustomizedAccountContract()
    {
        var create = Read("Components", "NativeAccountCreateModal.razor");
        var edit = Read("Pages", "Providers.razor");
        var client = Read("Services", "ApiClient.cs");

        foreach (var value in new[]
        {
            "Device OAuth", "GitHub Token", "GitHub Billing 用户名", "Billing PAT",
            "AI Credits 月额度", "安全余量（AI Credits）", "关闭 Billing API 预防停调度",
            "Api.StartCopilotOAuthAsync", "Api.CreateCopilotAccountAsync",
            "Api.ValidateCopilotBillingPatAsync", "Api.CancelCopilotOAuthAsync",
            "CancelCopilotFlowBestEffortAsync", "ClearCopilotSensitiveInputs",
            "Notes = string.IsNullOrWhiteSpace(form.Notes)",
            "platform == \"copilot\" ? \"openai\" : platform",
            "new(\"copilot\", \"GitHub Copilot\", \"zap\")",
            "copilotPollingGeneration", "IsCopilotFlowCurrent",
            "var pollingTask = copilotPollingTask",
            "await copilotPollGate.WaitAsync();"
        })
        {
            Assert.Contains(value, create, StringComparison.Ordinal);
        }

        Assert.Contains("id=\"native-copilot-github-token\" type=\"password\"", create, StringComparison.Ordinal);
        Assert.Contains("id=\"native-copilot-billing-pat\" type=\"password\"", create, StringComparison.Ordinal);
        Assert.Contains("min=\"0\" step=\"1\" value=\"@form.BillingSafetyMargin\"", create, StringComparison.Ordinal);
        var closeStart = create.IndexOf("private async Task CloseAsync()", StringComparison.Ordinal);
        var closeEnd = create.IndexOf("private async Task BackToFirstStepAsync()", closeStart, StringComparison.Ordinal);
        Assert.True(closeStart >= 0 && closeEnd > closeStart);
        Assert.Contains("ClearCopilotSensitiveInputs();", create[closeStart..closeEnd], StringComparison.Ordinal);
        var disposeStart = create.IndexOf("public async ValueTask DisposeAsync()", StringComparison.Ordinal);
        var disposeEnd = create.IndexOf("private sealed record PlatformOption", disposeStart, StringComparison.Ordinal);
        Assert.True(disposeStart >= 0 && disposeEnd > disposeStart);
        var disposeBody = create[disposeStart..disposeEnd];
        Assert.True(disposeBody.IndexOf("await pollingTask", StringComparison.Ordinal)
            < disposeBody.IndexOf("await CancelCopilotFlowBestEffortAsync();", StringComparison.Ordinal));
        Assert.Contains("oauth_profile", edit, StringComparison.Ordinal);
        Assert.Contains("BillingUsername = editingIsCopilot ? ReadCopilotBillingUsername(account)", edit, StringComparison.Ordinal);
        Assert.Contains("ReadCredential(row, \"github_login\")", edit, StringComparison.Ordinal);
        Assert.Contains("HasBillingPat = editingIsCopilot && HasCredentialStatus", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("BillingPat = editingIsCopilot ? ReadCredential", edit, StringComparison.Ordinal);
        Assert.Contains("RestrictionPlatformOverride=\"@(editingIsCopilot ? \"copilot\" : null)\"", edit, StringComparison.Ordinal);
        Assert.Contains("<input id=\"account-copilot-billing-username\"", edit, StringComparison.Ordinal);
        Assert.Contains("<input id=\"account-copilot-billing-pat\" type=\"password\"", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("<InputText id=\"account-copilot-billing-", edit, StringComparison.Ordinal);
        Assert.Contains("留空表示保留原值", edit, StringComparison.Ordinal);
        Assert.Contains("/admin/accounts/copilot-billing-pat/validate", client, StringComparison.Ordinal);
        Assert.Contains("credentials[\"oauth_profile\"] = \"github_copilot\"", client, StringComparison.Ordinal);
        Assert.Contains("ValidateCopilotEditBillingIdentity(input);", client, StringComparison.Ordinal);

        var fingerprintStart = edit.IndexOf("private string EditorConnectionFingerprint()", StringComparison.Ordinal);
        var fingerprintEnd = edit.IndexOf("private void ExpiryChanged", fingerprintStart, StringComparison.Ordinal);
        Assert.True(fingerprintStart >= 0 && fingerprintEnd > fingerprintStart);
        Assert.DoesNotContain("form.BillingPat", edit[fingerprintStart..fingerprintEnd], StringComparison.Ordinal);
    }

    [Fact]
    public void AccountPageContainsV0178ProviderQuotaAndOpenAIBulkSettingsSurface()
    {
        var page = Read("Pages", "Providers.razor");
        var createModal = Read("Components", "NativeAccountCreateModal.razor");
        var quotaCell = Read("Components", "AccountQuotaUsageCell.razor");

        foreach (var platform in new[] { "kimi", "zhipu", "deepseek" })
        {
            Assert.Contains($"value=\"{platform}\"", page, StringComparison.Ordinal);
            Assert.Contains($"new(\"{platform}\"", createModal, StringComparison.Ordinal);
        }

        Assert.Contains("账号模式", createModal, StringComparison.Ordinal);
        Assert.Contains("API 协议", createModal, StringComparison.Ordinal);
        Assert.Contains("https://api.kimi.com/coding/v1", createModal, StringComparison.Ordinal);
        Assert.Contains("https://open.bigmodel.cn/api/coding/paas/v4", createModal, StringComparison.Ordinal);
        Assert.Contains("https://api.deepseek.com/anthropic", createModal, StringComparison.Ordinal);
        Assert.Contains("<AccountQuotaUsageCell Account=\"row\"", page, StringComparison.Ordinal);
        Assert.Contains("GetAccountUsageBatchAsync", page, StringComparison.Ordinal);
        Assert.Contains("usageByAccountId", page, StringComparison.Ordinal);
        Assert.Contains("GetCNProviderQuotaAsync", quotaCell, StringComparison.Ordinal);
        Assert.Contains("GetCNProviderBalanceAsync", quotaCell, StringComparison.Ordinal);
        Assert.Contains("RefreshOllamaCloudUsageAsync", quotaCell, StringComparison.Ordinal);
        Assert.Contains("GetAccountUsageAsync", quotaCell, StringComparison.Ordinal);
        Assert.Contains("RefreshOpenAIQuotaAsync", quotaCell, StringComparison.Ordinal);
        Assert.Contains("ResetOpenAIQuotaAsync", quotaCell, StringComparison.Ordinal);
        Assert.Contains("five_hour", Read("Models", "Dtos.cs"), StringComparison.Ordinal);
        Assert.Contains("seven_day", Read("Models", "Dtos.cs"), StringComparison.Ordinal);
        Assert.Contains("window_stats", Read("Models", "Dtos.cs"), StringComparison.Ordinal);
        Assert.Contains("openai_long_context_billing_enabled", page, StringComparison.Ordinal);
        Assert.Contains("openai_capabilities", page, StringComparison.Ordinal);
        Assert.Contains("openai_responses_mode", page, StringComparison.Ordinal);
        Assert.Contains("LongContextInheritedCount", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AllAccountUsageWindowsRenderTodayAccountAndUserCosts()
    {
        var page = Read("Pages", "Providers.razor");
        var quotaCell = Read("Components", "AccountQuotaUsageCell.razor");

        Assert.Contains("TodayStats=\"@(todayStats.TryGetValue(row.Id, out var usageTodayStats) ? usageTodayStats : null)\"", page, StringComparison.Ordinal);
        Assert.Contains("TodayStatsLoading=\"@todayStatsLoading\"", page, StringComparison.Ordinal);
        Assert.Contains("!IsColumnVisible(\"today\") && !IsColumnVisible(\"usage\")", page, StringComparison.Ordinal);
        Assert.Contains("var todayStats = TodayStats ?? new AccountTodayStatsDto();", quotaCell, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowsTodayStatsInUsageWindow", quotaCell, StringComparison.Ordinal);
        Assert.DoesNotContain("!TodayStatsLoading && TodayStats is null", quotaCell, StringComparison.Ordinal);
        Assert.Contains("UiFormat.Usd(0)", page, StringComparison.Ordinal);

        foreach (var value in new[] { "Requests req", "FormatCompact(todayStats.Tokens)", "A @UiFormat.Usd(todayStats.Cost)", "U @UiFormat.Usd(todayStats.UserCost)" })
        {
            Assert.Contains(value, quotaCell, StringComparison.Ordinal);
        }

        Assert.Contains("A @UiFormat.Usd(stats.Cost)", quotaCell, StringComparison.Ordinal);
        Assert.Contains("var stats = item.Window.WindowStats ?? new AccountUsageWindowStatsDto();", quotaCell, StringComparison.Ordinal);
        Assert.Contains("\"openai\" => account.Type == \"oauth\" && !IsCopilot(account)", page, StringComparison.Ordinal);
        Assert.Contains("U @UiFormat.Usd(stats.UserCost)", quotaCell, StringComparison.Ordinal);
        Assert.DoesNotContain("UiFormat.Usd(todayStats.StandardCost)", quotaCell, StringComparison.Ordinal);
        Assert.DoesNotContain("UiFormat.Usd(stats.StandardCost)", quotaCell, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountUsageWindowsUseCompactNonTruncatingStyles()
    {
        var styles = Read("Components", "AccountQuotaUsageCell.razor.css");

        Assert.Contains("min-width: 235px; gap: 3px;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 38px 72px 38px minmax(54px, 1fr);", styles, StringComparison.Ordinal);
        Assert.Contains("font-size: .68rem;", styles, StringComparison.Ordinal);
        Assert.Contains("height: 6px;", styles, StringComparison.Ordinal);
        Assert.Contains("padding: 2px 5px;", styles, StringComparison.Ordinal);
        Assert.Contains("font-size: .64rem;", styles, StringComparison.Ordinal);
        Assert.Contains("min-height: 21px;", styles, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void CopilotUsesBillingCapacityAndHidesOfficialUsageWindows()
    {
        var page = Read("Pages", "Providers.razor");
        var quotaCell = Read("Components", "AccountQuotaUsageCell.razor");
        var dto = Read("Models", "Dtos.cs");
        Assert.Contains("copilot_billing_usage", dto, StringComparison.Ordinal);
        Assert.Contains("CopilotBillingUsageDto", dto, StringComparison.Ordinal);
        Assert.Contains("FormatCopilotCredits(CopilotBillingUsed(row)) / @FormatCopilotCredits(CopilotBillingLimit(row)) AI", page, StringComparison.Ordinal);
        Assert.Contains("DefaultCopilotBillingCreditLimit = 20_000", page, StringComparison.Ordinal);
        Assert.Contains("@if (IsOfficialUsageAccount && !IsCopilot)", quotaCell, StringComparison.Ordinal);
        Assert.Contains("\"openai\" => Account.Type == \"oauth\" && !IsCopilot", quotaCell, StringComparison.Ordinal);
        Assert.Contains("else if (!IsCopilot && HasLegacyQuota)", quotaCell, StringComparison.Ordinal);
        Assert.Contains("SupportsUsageWindows", page, StringComparison.Ordinal);
        Assert.DoesNotContain("CopilotBillingUsage", quotaCell, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "$0.00")]
    [InlineData(0.0049, "$0.00")]
    [InlineData(0.00001234, "$0.00")]
    [InlineData(1.239, "$1.24")]
    [InlineData(1234.5, "$1,234.50")]
    public void AccountUsageCostsRenderWithTwoDecimals(double value, string expected)
    {
        Assert.Equal(expected, UiFormat.Usd(value));
        Assert.Equal("$0.00", UiFormat.Money(4_900));
        Assert.Equal("$1,234.50", UiFormat.Money(1_234_500_000));
    }

    [Fact]
    public void CopilotAccountsUseCredentialLabelAndRedactedStatus()
    {
        var markup = Read("Pages", "Providers.razor");

        Assert.Contains("string.Equals(row.Platform, \"copilot\"", markup, StringComparison.Ordinal);
        Assert.Contains("if (IsCopilot(row)) return \"GitHub Copilot 凭据\";", markup, StringComparison.Ordinal);
        Assert.Contains("row.CredentialsStatus?.Values.Any(value => value) == true", markup, StringComparison.Ordinal);
        Assert.Contains("CredentialConfigured(row) ? \"凭据已配置\" : \"凭据待补充\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("if (profile == \"github_copilot\") return \"GitHub Device OAuth\";", markup, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }
}
