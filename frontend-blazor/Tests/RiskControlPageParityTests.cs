using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class RiskControlPageParityTests
{
    [Fact]
    public void RiskControlPageMatchesOfficialOverviewAndRuntimeSurface()
    {
        var markup = Read("Pages", "AdminRiskControl.razor");

        foreach (var text in new[] { "风控中心", "刷新状态", "风控设置", "运行状态", "API Key", "审计范围", "审核记录", "前置拦截同步状态", "审核 Key 负载", "Worker 运行状态" })
        {
            Assert.Contains(text, markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RiskControlSettingsContainAllOfficialTabsAndSecretManagement()
    {
        var markup = Read("Pages", "AdminRiskControl.razor");

        foreach (var text in new[] { "基础", "审计范围", "运行队列", "命中通知", "风险阈值", "关键词拦截", "日志保留", "增量添加", "覆盖保存", "测试输入区 Key", "测试已保存 Key", "清除已保存 Key" })
        {
            Assert.Contains(text, markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RiskControlPageConnectsOfficialBackendFeatures()
    {
        var markup = Read("Pages", "AdminRiskControl.razor");

        foreach (var method in new[] { "GetRiskConfigTypedAsync", "UpdateRiskConfigTypedAsync", "GetRiskStatusAsync", "TestRiskApiKeysAsync", "GetRiskLogsAsync", "UnbanRiskUserAsync", "DeleteRiskHashAsync", "ClearRiskHashesAsync" })
        {
            Assert.Contains($"Api.{method}", markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RiskControlClientUsesOfficialGoEndpointsAndFilters()
    {
        var client = Read("Services", "ApiClient.cs");

        foreach (var endpoint in new[] { "/admin/risk-control/config", "/admin/risk-control/status", "/admin/risk-control/api-keys/test", "/admin/risk-control/logs", "/admin/risk-control/users/", "/admin/risk-control/hashes" })
        {
            Assert.Contains(endpoint, client, StringComparison.Ordinal);
        }

        foreach (var query in new[] { "result=", "group_id=", "endpoint=", "from=", "to=" })
        {
            Assert.Contains(query, client, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RiskControlPageHasResponsiveOfficialStyleSurface()
    {
        var css = Read("Pages", "AdminRiskControl.razor.css");
        Assert.Contains(".overview-grid", css, StringComparison.Ordinal);
        Assert.Contains(".runtime-split", css, StringComparison.Ordinal);
        Assert.Contains(".api-key-layout", css, StringComparison.Ordinal);
        Assert.Contains(".threshold-grid", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 760px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RiskControlDtosNormalizeNullCollectionsReturnedByGo()
    {
        var config = JsonSerializer.Deserialize<RiskControlConfigDto>("""
            {
              "group_ids": null,
              "blocked_keywords": null,
              "api_key_masks": null,
              "api_key_statuses": null,
              "thresholds": null,
              "model_filter": { "type": "all", "models": null }
            }
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var status = JsonSerializer.Deserialize<RiskControlStatusDto>("""
            { "pre_block_api_key_loads": null, "api_key_statuses": null }
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        config.NormalizeCollections();
        status.NormalizeCollections();

        Assert.NotNull(config.GroupIds);
        Assert.NotNull(config.BlockedKeywords);
        Assert.NotNull(config.ApiKeyMasks);
        Assert.NotNull(config.ApiKeyStatuses);
        Assert.NotNull(config.Thresholds);
        Assert.NotNull(config.ModelFilter.Models);
        Assert.NotNull(status.PreBlockApiKeyLoads);
        Assert.NotNull(status.ApiKeyStatuses);
    }

    [Fact]
    public void RiskControlPageCatchesUnexpectedInitializationErrorsInsteadOfFreezing()
    {
        var markup = Read("Pages", "AdminRiskControl.razor");

        Assert.Contains("config.NormalizeCollections()", markup, StringComparison.Ordinal);
        Assert.Contains("内容审核页面加载失败", markup, StringComparison.Ordinal);
        Assert.Contains("finally { loading = false; }", markup, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }
}
