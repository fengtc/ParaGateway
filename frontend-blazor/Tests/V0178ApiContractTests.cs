using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class V0178ApiContractTests
{
    [Fact]
    public async Task CNProviderQuotaAndBalanceUseOfficialRoutesAndTypedPayloads()
    {
        var handler = new V0178Handler();
        var api = CreateApi(handler);

        var quota = await api.GetCNProviderQuotaAsync("42");
        Assert.Equal("/api/v1/admin/cn-providers/accounts/42/quota", handler.LastPath);
        Assert.True(quota.Success);
        Assert.True(quota.CredentialValid);
        Assert.Equal("weekly", quota.Tiers[1].Window);

        var balance = await api.GetCNProviderBalanceAsync("42");
        Assert.Equal("/api/v1/admin/cn-providers/accounts/42/balance", handler.LastPath);
        Assert.True(balance.Available);
        Assert.Equal(2, balance.Balances.Count);
        Assert.Equal("USD", balance.Balances[1].Currency);
    }

    [Fact]
    public async Task OllamaRefreshUsesAccountRouteAndMapsEmbeddedUsageState()
    {
        var handler = new V0178Handler();
        var api = CreateApi(handler);

        var state = await api.RefreshOllamaCloudUsageAsync("42");

        Assert.Equal("/api/v1/admin/accounts/42/ollama-cloud-usage/refresh", handler.LastPath);
        Assert.True(state.Eligible);
        Assert.True(state.Configured);
        Assert.Equal(35.5, state.Snapshot?.Data?.FiveHour?.UsedPercent);
        Assert.Equal("qwen3-coder", state.Snapshot?.Data?.Models.Single().Model);
    }

    [Fact]
    public async Task BulkUpdatePreservesNestedOpenAISettingsAndOfficialResultNames()
    {
        var handler = new V0178Handler();
        var api = CreateApi(handler);
        var result = await api.BulkUpdateAccountsAsync(["42", "43"], new Dictionary<string, object?>
        {
            ["credentials"] = new Dictionary<string, object?>
            {
                ["openai_capabilities"] = new[] { "chat_completions", "embeddings" }
            },
            ["extra"] = new Dictionary<string, object?>
            {
                ["openai_long_context_billing_enabled"] = true,
                ["openai_responses_mode"] = "force_responses"
            }
        });

        Assert.Equal("/api/v1/admin/accounts/bulk-update", handler.LastPath);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.LongContextInheritedCount);
        using var body = JsonDocument.Parse(handler.LastBody);
        Assert.Equal([42L, 43L], body.RootElement.GetProperty("account_ids").EnumerateArray().Select(x => x.GetInt64()).ToArray());
        Assert.True(body.RootElement.GetProperty("extra").GetProperty("openai_long_context_billing_enabled").GetBoolean());
        Assert.Equal("force_responses", body.RootElement.GetProperty("extra").GetProperty("openai_responses_mode").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("credentials").GetProperty("openai_capabilities").GetArrayLength());
    }

    [Fact]
    public async Task GroupUsageSummaryUsesServerTimezoneAndMapsYesterdayCost()
    {
        var handler = new V0178Handler();
        var api = CreateApi(handler);

        var usage = await api.GetAdminGroupUsageSummaryAsync();

        Assert.Equal("/api/v1/admin/groups/usage-summary", handler.LastPath);
        Assert.Equal(string.Empty, handler.LastQuery);
        Assert.Equal(2.5, usage.Single().YesterdayCost);
    }

    [Fact]
    public void MonitorAndAccountResponsesMapV0178QuotaFields()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var monitor = JsonSerializer.Deserialize<ChannelMonitorDto>("""
            {
              "id": 7,
              "name": "Kimi 配额",
              "provider": "kimi",
              "check_mode": "quota",
              "account_id": 42,
              "latest_quota": {
                "source": "cn_quota",
                "success": true,
                "tiers": [{ "window": "5h", "used_percent": 25 }],
                "fetched_at": "2026-08-19T01:00:00Z"
              }
            }
            """, options);
        var account = JsonSerializer.Deserialize<GoAccount>("""
            {
              "id": 42,
              "name": "Ollama",
              "platform": "openai",
              "type": "apikey",
              "ollama_cloud_usage": {
                "account_id": 42,
                "eligible": true,
                "configured": true,
                "auto_refresh_enabled": true,
                "encryption_key_configured": true
              }
            }
            """, options);

        Assert.NotNull(monitor);
        Assert.Equal("quota", monitor.CheckMode);
        Assert.Equal(42, monitor.AccountId);
        Assert.Equal(25, monitor.LatestQuota?.Tiers.Single().UsedPercent);
        Assert.NotNull(account);
        Assert.True(AccountDto.From(account).OllamaCloudUsage?.AutoRefreshEnabled);
    }

    private static ApiClient CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());

    private sealed class NullJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }

    private sealed class V0178Handler : HttpMessageHandler
    {
        public string LastPath { get; private set; } = string.Empty;
        public string LastQuery { get; private set; } = string.Empty;
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.AbsolutePath ?? string.Empty;
            LastQuery = request.RequestUri?.Query ?? string.Empty;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var data = LastPath switch
            {
                "/api/v1/admin/cn-providers/accounts/42/quota" =>
                    "{\"provider\":\"kimi\",\"source\":\"upstream\",\"success\":true,\"credential_valid\":true,\"tiers\":[{\"window\":\"5h\",\"used_percent\":20},{\"window\":\"weekly\",\"used_percent\":30}],\"fetched_at\":1770000000,\"persisted\":true}",
                "/api/v1/admin/cn-providers/accounts/42/balance" =>
                    "{\"provider\":\"deepseek\",\"success\":true,\"balance\":10,\"currency\":\"CNY\",\"balances\":[{\"currency\":\"CNY\",\"balance\":10},{\"currency\":\"USD\",\"balance\":2}],\"available\":true,\"fetched_at\":1770000000,\"persisted\":true}",
                "/api/v1/admin/accounts/42/ollama-cloud-usage/refresh" =>
                    "{\"account_id\":42,\"eligible\":true,\"configured\":true,\"auto_refresh_enabled\":true,\"encryption_key_configured\":true,\"snapshot\":{\"status\":\"ok\",\"data\":{\"plan\":\"pro\",\"five_hour\":{\"used_percent\":35.5},\"models\":[{\"model\":\"qwen3-coder\",\"window\":\"five_hour\",\"requests\":8}]},\"last_attempt_at\":\"2026-08-19T01:00:00Z\",\"next_refresh_at\":\"2026-08-19T02:00:00Z\"}}",
                "/api/v1/admin/accounts/bulk-update" =>
                    "{\"success\":2,\"failed\":0,\"success_ids\":[42,43],\"failed_ids\":[],\"long_context_inherited_count\":1,\"results\":[{\"account_id\":42,\"success\":true},{\"account_id\":43,\"success\":true}]}",
                "/api/v1/admin/groups/usage-summary" =>
                    "[{\"group_id\":7,\"today_cost\":1.25,\"yesterday_cost\":2.5,\"total_cost\":9.75}]",
                _ => throw new InvalidOperationException($"Unexpected contract path: {LastPath}")
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"code\":0,\"message\":\"success\",\"data\":{data}}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
