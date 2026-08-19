using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ChannelMonitorQuotaParityTests
{
    [Fact]
    public void EditorCoversAllProvidersModesAndLinkedAccountHydration()
    {
        var page = ReadProjectFile("Pages", "AdminChannelMonitor.razor");

        foreach (var provider in new[] { "openai", "anthropic", "gemini", "grok", "antigravity", "kimi", "zhipu", "deepseek" })
            Assert.Contains($"value=\"{provider}\"", page, StringComparison.Ordinal);
        foreach (var mode in new[] { "probe", "quota", "quota_probe" })
            Assert.Contains($"SetMonitorCheckModeAsync(\"{mode}\")", page, StringComparison.Ordinal);

        Assert.Contains("GetAccountsPageAsync", page, StringComparison.Ordinal);
        Assert.Contains("Platform = monitorForm.Provider", page, StringComparison.Ordinal);
        Assert.Contains("GetAccountAsync", page, StringComparison.Ordinal);
        Assert.Contains("selected.Platform, monitorForm.Provider", page, StringComparison.Ordinal);
        Assert.Contains("if (next == \"antigravity\") monitorForm.CheckMode = \"quota\";", page, StringComparison.Ordinal);
        Assert.Contains("MonitorUsesQuota && monitorForm.AccountId is null or <= 0", page, StringComparison.Ordinal);
    }

    [Fact]
    public void SavePayloadUsesOfficialConditionalFieldsAndClearAccountSentinel()
    {
        var page = ReadProjectFile("Pages", "AdminChannelMonitor.razor");

        Assert.Contains("[\"check_mode\"] = monitorForm.CheckMode", page, StringComparison.Ordinal);
        Assert.Contains("[\"account_id\"] = MonitorUsesQuota ? monitorForm.AccountId", page, StringComparison.Ordinal);
        Assert.Contains("editingMonitor is null ? null : (long?)0", page, StringComparison.Ordinal);
        Assert.Contains("[\"endpoint\"] = MonitorUsesProbe", page, StringComparison.Ordinal);
        Assert.Contains("[\"primary_model\"] = MonitorUsesProbe", page, StringComparison.Ordinal);
        Assert.Contains("[\"extra_models\"] = MonitorUsesProbe", page, StringComparison.Ordinal);
        Assert.Contains("if (MonitorUsesProbe && !string.IsNullOrWhiteSpace(monitorForm.ApiKey))", page, StringComparison.Ordinal);
        Assert.Contains("Antigravity 仅支持配额检测模式", page, StringComparison.Ordinal);
    }

    [Fact]
    public void QuotaSnapshotIsRenderedInListRunResultAndHistory()
    {
        var page = ReadProjectFile("Pages", "AdminChannelMonitor.razor");
        var component = ReadProjectFile("Components", "MonitorQuotaView.razor");

        Assert.True(Count(page, "<MonitorQuotaView") >= 3);
        Assert.Contains("runResultOpen", page, StringComparison.Ordinal);
        Assert.Contains("GetAdminChannelMonitorHistoryAsync", page, StringComparison.Ordinal);
        Assert.Contains("Snapshot=\"@row.Quota\"", page, StringComparison.Ordinal);
        foreach (var value in new[] { "PlanLevel", "Snapshot.Tiers", "Snapshot.Balances", "Snapshot.Balance", "UsedPercent", "配额暂不可用" })
            Assert.Contains(value, component, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuotaContractsMapMultiTierAndMultiCurrencyPayload()
    {
        const string json = """
            {
              "source": "cn_balance",
              "success": true,
              "tiers": [
                { "window": "5h", "used_percent": 25.5, "used": 255, "limit": 1000 },
                { "window": "weekly", "label": "tokens", "used_percent": 76 }
              ],
              "balances": [
                { "currency": "CNY", "balance": 18.25 },
                { "currency": "USD", "balance": 2.5 }
              ],
              "plan_level": "pro",
              "fetched_at": "2026-08-19T01:00:00Z"
            }
            """;

        var snapshot = JsonSerializer.Deserialize<MonitorQuotaSnapshotDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(snapshot);
        Assert.Equal("pro", snapshot.PlanLevel);
        Assert.Equal(2, snapshot.Tiers.Count);
        Assert.Equal(25.5, snapshot.Tiers[0].UsedPercent);
        Assert.Equal("tokens", snapshot.Tiers[1].Label);
        Assert.Equal(2, snapshot.Balances.Count);
        Assert.Equal("USD", snapshot.Balances[1].Currency);
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var marker = Path.Combine(directory.FullName, "Pages", "AdminChannelMonitor.razor");
            if (File.Exists(marker))
                return File.ReadAllText(Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray()));
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Blazor frontend project.");
    }
}
