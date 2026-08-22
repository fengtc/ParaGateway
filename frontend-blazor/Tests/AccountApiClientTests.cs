using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AccountApiClientTests
{
    [Fact]
    public async Task OpenAIOAuthUsesBackendDefaultRedirectWithoutClientOverride()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);

        var started = await api.StartOpenAIOAuthAsync();

        Assert.Equal("/api/v1/admin/openai/generate-auth-url", handler.LastRequestPath);
        Assert.Equal("{}", handler.LastRequestBody);
        Assert.Equal("http://localhost:1455/auth/callback", new Uri(started.AuthorizationUrl).GetQueryValue("redirect_uri"));

        await api.CreateAccountFromOAuthAsync("openai", new OAuthExchangeInput
        {
            SessionId = started.SessionId,
            Code = "openai-code-1",
            State = "state-1"
        }, "OpenAI 主账号", 8, 100, []);

        Assert.Equal("/api/v1/admin/openai/create-from-oauth", handler.LastRequestPath);
        using var request = JsonDocument.Parse(handler.LastRequestBody);
        Assert.False(request.RootElement.TryGetProperty("redirect_uri", out _));
    }

    [Fact]
    public async Task AnthropicOAuthUsesClaudeAccountRoutesAndStrictPayloads()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);

        var started = await api.StartAnthropicOAuthAsync();

        Assert.Equal("/api/v1/admin/accounts/generate-auth-url", handler.LastRequestPath);
        Assert.Equal("https://platform.claude.com/oauth/authorize?state=state-1", started.AuthorizationUrl);
        Assert.Equal("claude-session-1", started.SessionId);
        Assert.Equal("{}", handler.LastRequestBody);

        var token = await api.ExchangeAnthropicOAuthAsync(new OAuthExchangeInput
        {
            SessionId = started.SessionId,
            Code = "claude-code-1",
            State = "must-not-be-sent",
            RedirectUri = "https://paragateway.test/auth/callback"
        });

        Assert.Equal("/api/v1/admin/accounts/exchange-code", handler.LastRequestPath);
        using var request = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal(2, request.RootElement.EnumerateObject().Count());
        Assert.Equal("claude-session-1", request.RootElement.GetProperty("session_id").GetString());
        Assert.Equal("claude-code-1", request.RootElement.GetProperty("code").GetString());
        Assert.False(request.RootElement.TryGetProperty("state", out _));
        Assert.False(request.RootElement.TryGetProperty("redirect_uri", out _));
        Assert.Equal("claude-access-1", token["access_token"].GetString());
    }

    [Fact]
    public void AnthropicOAuthCredentialsKeepRefreshAndExpirationMetadata()
    {
        var token = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"access_token":"access","refresh_token":"refresh","token_type":"Bearer","expires_in":3600,"expires_at":1770000000,"org_uuid":"org-1","account_uuid":"account-1","email_address":"claude@example.test"}""")!;

        var credentials = ApiClient.BuildOAuthCredentials("anthropic", token);

        Assert.Equal("access", Assert.IsType<JsonElement>(credentials["access_token"]).GetString());
        Assert.Equal("refresh", Assert.IsType<JsonElement>(credentials["refresh_token"]).GetString());
        Assert.Equal(3600, Assert.IsType<JsonElement>(credentials["expires_in"]).GetInt32());
        Assert.Equal(1770000000, Assert.IsType<JsonElement>(credentials["expires_at"]).GetInt64());
        Assert.Equal("org-1", Assert.IsType<JsonElement>(credentials["org_uuid"]).GetString());
        Assert.Equal("account-1", Assert.IsType<JsonElement>(credentials["account_uuid"]).GetString());
        Assert.Equal("claude@example.test", Assert.IsType<JsonElement>(credentials["email_address"]).GetString());
    }

    [Fact]
    public async Task OfficialAccountCreateAndUpdateExcludeIndependentUpstreamPolicyFields()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);
        var input = new AccountInput
        {
            Name = "runtime-policy",
            Platform = "openai",
            Type = "apikey",
            ApiKey = "sk-official-account",
            GroupIds = [11, 23]
        };

        var created = await api.CreateAccountAsync(input);
        var createBody = handler.LastRequestBody;

        Assert.Equal("/api/v1/admin/accounts", handler.LastRequestPath);
        Assert.Equal("runtime-policy", created.Name);
        AssertOfficialAccountPayloadDoesNotContainUpstreamPolicy(createBody);

        var updated = await api.UpdateAccountAsync("42", input);

        Assert.Equal("/api/v1/admin/accounts/42", handler.LastRequestPath);
        Assert.Equal("runtime-policy", updated.Name);
        AssertOfficialAccountPayloadDoesNotContainUpstreamPolicy(handler.LastRequestBody);
        using var updateBody = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal([11L, 23L], updateBody.RootElement.GetProperty("group_ids").EnumerateArray().Select(value => value.GetInt64()).ToArray());
    }

    [Fact]
    public async Task CNProviderCreatePersistsModeProtocolAndPresetEndpointInCredentials()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);

        await api.CreateAccountAsync(new AccountInput
        {
            Name = "kimi-coding",
            Platform = "kimi",
            Type = "apikey",
            ApiKey = "sk-kimi-test",
            BaseUrl = "https://api.kimi.com/coding",
            AccountMode = "coding",
            ApiProtocol = "anthropic"
        });

        Assert.Equal("/api/v1/admin/accounts", handler.LastRequestPath);
        using var request = JsonDocument.Parse(handler.LastRequestBody);
        var credentials = request.RootElement.GetProperty("credentials");
        Assert.Equal("sk-kimi-test", credentials.GetProperty("api_key").GetString());
        Assert.Equal("https://api.kimi.com/coding", credentials.GetProperty("base_url").GetString());
        Assert.Equal("coding", credentials.GetProperty("account_mode").GetString());
        Assert.Equal("anthropic", credentials.GetProperty("api_protocol").GetString());
    }

    [Fact]
    public async Task AdaptiveCNProviderCreateSerializesEveryProtocolBaseUrl()
    {
        var handler = new AccountHandler(string.Empty);
        var api = CreateApi(handler);

        await api.CreateAccountAsync(new AccountInput
        {
            Name = "deepseek-adaptive",
            Platform = "deepseek",
            Type = "apikey",
            ApiKey = "sk-deepseek-test",
            BaseUrl = "https://stale.example/v1",
            AccountMode = "payg",
            ApiProtocol = "adaptive",
            AdaptiveChatCompletionsBaseUrl = " https://api.deepseek.com ",
            AdaptiveAnthropicBaseUrl = "https://api.deepseek.com/anthropic",
            AdaptiveResponsesBaseUrl = "https://api.deepseek.com/responses"
        });

        Assert.Equal("/api/v1/admin/accounts", handler.LastRequestPath);
        using var request = JsonDocument.Parse(handler.LastRequestBody);
        var credentials = request.RootElement.GetProperty("credentials");
        var baseUrls = credentials.GetProperty("api_base_urls");
        Assert.Equal("adaptive", credentials.GetProperty("api_protocol").GetString());
        Assert.Equal("https://api.deepseek.com", credentials.GetProperty("base_url").GetString());
        Assert.Equal("https://api.deepseek.com", baseUrls.GetProperty("chat_completions").GetString());
        Assert.Equal("https://api.deepseek.com/anthropic", baseUrls.GetProperty("anthropic").GetString());
        Assert.Equal("https://api.deepseek.com/responses", baseUrls.GetProperty("responses").GetString());
        Assert.Equal(3, baseUrls.EnumerateObject().Count());
    }

    [Fact]
    public async Task GroupCreateAndUpdateSendLongContextPricingFlag()
    {
        var handler = new AccountHandler(string.Empty);
        var api = CreateApi(handler);
        var input = new GroupInput
        {
            Name = "OpenAI 计费分组",
            Platform = "openai",
            LongContextPricingEnabled = true,
            AdvancedJson = """{"advanced_marker":"keep"}"""
        };

        await api.CreateGroupAsync(input);

        Assert.Equal("/api/v1/admin/groups", handler.LastRequestPath);
        using (var request = JsonDocument.Parse(handler.LastRequestBody))
        {
            Assert.True(request.RootElement.GetProperty("long_context_pricing_enabled").GetBoolean());
            Assert.Equal("keep", request.RootElement.GetProperty("advanced_marker").GetString());
        }

        input.LongContextPricingEnabled = false;
        await api.UpdateGroupAsync("12", input, active: false);

        Assert.Equal("/api/v1/admin/groups/12", handler.LastRequestPath);
        using var update = JsonDocument.Parse(handler.LastRequestBody);
        Assert.False(update.RootElement.GetProperty("long_context_pricing_enabled").GetBoolean());
        Assert.Equal("inactive", update.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AccountCreateAndEditSerializeModelRestrictionsIntoCredentials()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);
        var input = new AccountInput
        {
            Name = "restricted-openai",
            Platform = "openai",
            Type = "apikey",
            ApiKey = "sk-test",
            ModelRestrictionMode = "whitelist",
            AllowedModels = ["gpt-5.4", "gpt-5.6-sol"]
        };

        await api.CreateAccountAsync(input);

        using (var createRequest = JsonDocument.Parse(handler.LastRequestBody))
        {
            var mapping = createRequest.RootElement.GetProperty("credentials").GetProperty("model_mapping");
            Assert.Equal("gpt-5.4", mapping.GetProperty("gpt-5.4").GetString());
            Assert.Equal("gpt-5.6-sol", mapping.GetProperty("gpt-5.6-sol").GetString());
        }

        input.IsEditing = true;
        input.ApiKey = string.Empty;
        input.AllowedModels.Clear();
        await api.UpdateAccountAsync("42", input);

        using var updateRequest = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal(JsonValueKind.Object, updateRequest.RootElement.GetProperty("credentials").GetProperty("model_mapping").ValueKind);
        Assert.Empty(updateRequest.RootElement.GetProperty("credentials").GetProperty("model_mapping").EnumerateObject());
    }

    [Fact]
    public async Task OAuthCreateSendsOnlyModelMappingAsCredentialExtras()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);
        var settings = new AccountInput
        {
            Platform = "openai",
            Type = "oauth",
            AccessToken = "must-not-be-sent",
            RefreshToken = "must-not-be-sent",
            ModelRestrictionMode = "mapping",
            ModelMappings = [new ModelMappingInput { From = "claude-*", To = "gpt-5.6-sol" }]
        };

        await api.CreateAccountFromOAuthAsync("openai", new OAuthExchangeInput
        {
            SessionId = "session-1",
            Code = "code-1",
            State = "state-1"
        }, "OpenAI OAuth", 8, 100, [], settings);

        using var request = JsonDocument.Parse(handler.LastRequestBody);
        var extras = request.RootElement.GetProperty("credential_extras");
        Assert.Equal("gpt-5.6-sol", extras.GetProperty("model_mapping").GetProperty("claude-*").GetString());
        Assert.False(extras.TryGetProperty("access_token", out _));
        Assert.False(extras.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task IndependentUpstreamCreateUsesDedicatedRouteAndPolicyContract()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);
        var input = new UpstreamAccountInput
        {
            Name = "openai-wif",
            ProviderType = "openai",
            AuthType = "wif",
            WifClientSecret = "client-secret",
            WifSubjectTokenUrl = "https://issuer.example.net/oauth/token",
            WifClientId = "client-id",
            WifClientAuthMethod = "client_secret_post",
            WifAudience = "openai-audience",
            WifScope = "api.read",
            WifIdentityProviderId = "idp_123",
            WifServiceAccountId = "sa_123",
            BaseUrl = "https://api.openai.com",
            Weight = 250,
            RpmLimit = 120,
            CircuitBreakerThreshold = 4,
            CircuitBreakerCooldownSeconds = 90
        };

        await api.CreateUpstreamAccountAsync(input);

        Assert.Equal("/api/v1/admin/upstream-accounts", handler.LastRequestPath);
        using var request = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal("wif", request.RootElement.GetProperty("auth_type").GetString());
        Assert.Equal("client-secret", request.RootElement.GetProperty("wif_client_secret").GetString());
        Assert.Equal("https://issuer.example.net/oauth/token", request.RootElement.GetProperty("wif_subject_token_url").GetString());
        Assert.Equal("client-id", request.RootElement.GetProperty("wif_client_id").GetString());
        Assert.Equal(250, request.RootElement.GetProperty("weight").GetInt32());
        Assert.Equal(120, request.RootElement.GetProperty("rpm_limit").GetInt32());
        Assert.Equal(4, request.RootElement.GetProperty("circuit_breaker_threshold").GetInt32());
        Assert.Equal(90, request.RootElement.GetProperty("circuit_breaker_cooldown_seconds").GetInt32());
    }

    [Fact]
    public async Task IndependentUpstreamSavedTestUsesDedicatedRoute()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);
        var result = await api.TestUpstreamAccountSavedAsync("upstream-42");

        Assert.True(result.Success);
        Assert.Equal("/api/v1/admin/upstream-accounts/upstream-42/test-connection", handler.LastRequestPath);
    }

    [Fact]
    public async Task SavedAccountTestParsesGoSseEvents()
    {
        const string sse = """
            data: {"type":"test_start","model":"gpt-5.2"}

            data: {"type":"content","text":"ok"}

            data: {"type":"test_complete","success":true}

            """;
        var api = CreateApi(new AccountHandler(sse));

        var result = await api.TestAccountAsync("42");

        Assert.True(result.Success);
        Assert.Equal("连接成功", result.Message);
        Assert.NotNull(result.LatencyMs);
    }

    [Fact]
    public async Task SavedAccountTestReturnsSafeSseFailure()
    {
        const string sse = "data: {\"type\":\"error\",\"error\":\"invalid upstream credential\"}\n\n";
        var api = CreateApi(new AccountHandler(sse));

        var result = await api.TestAccountAsync("42");

        Assert.False(result.Success);
        Assert.Equal("invalid upstream credential", result.Message);
    }

    [Fact]
    public async Task SavedAccountTestRequiresTerminalSseEvent()
    {
        const string sse = "data: {\"type\":\"content\",\"text\":\"partial\"}\n\n";
        var api = CreateApi(new AccountHandler(sse));

        var result = await api.TestAccountAsync("42");

        Assert.False(result.Success);
        Assert.Equal("连接测试流未返回完成事件。", result.Message);
    }

    [Fact]
    public async Task DraftModelReadRequiresApiKeyBeforeSendingRequest()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => api.PreviewAccountModelsAsync(new AccountInput
        {
            Platform = "openai",
            Type = "apikey",
            BaseUrl = "https://upstream.example"
        }));

        Assert.Equal("请先填写 API Key。", error.Message);
        Assert.Equal(string.Empty, handler.LastRequestBody);
    }

    [Fact]
    public async Task DraftAndSavedModelSyncReadStringModelArrays()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);

        var preview = await api.PreviewAccountModelsAsync(new AccountInput
        {
            Platform = "openai",
            Type = "apikey",
            ApiKey = "sk-test",
            BaseUrl = "https://upstream.example"
        });
        var previewRequestBody = handler.LastRequestBody;
        var saved = await api.SyncAccountModelsAsync("42");

        Assert.Equal(new[] { "gpt-5.2", "gpt-5.3-codex" }, preview);
        Assert.Equal(new[] { "gpt-5.2", "gpt-5.3-codex" }, saved.Select(model => model.Id));
        Assert.Contains("\"api_key\":\"sk-test\"", previewRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"base_url\":\"https://upstream.example\"", previewRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountStatsUsesOfficialTypedThirtyDayContract()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);

        var result = await api.GetAccountStatsAsync("42");

        Assert.Equal("/api/v1/admin/accounts/42/stats", handler.LastRequestPath);
        Assert.Equal("?days=30", handler.LastRequestQuery);
        Assert.Equal(30, result.Summary.Days);
        Assert.Equal(128, result.Summary.TotalRequests);
        Assert.Equal(12.34, result.Summary.TotalCost, 2);
        Assert.Equal("08-20", Assert.Single(result.History).Label);
        Assert.Equal("gpt-5.6-sol", Assert.Single(result.Models).Model);
        Assert.Equal("/v1/responses", Assert.Single(result.Endpoints).Endpoint);
        Assert.Equal("/backend-api/codex/responses", Assert.Single(result.UpstreamEndpoints).Endpoint);
    }

    [Fact]
    public async Task AccountUsageBatchUsesOfficialTypedWindowContract()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);

        var result = await api.GetAccountUsageBatchAsync(["42", "43"]);

        Assert.Equal("/api/v1/admin/accounts/usage/batch", handler.LastRequestPath);
        using var request = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal([42L, 43L], request.RootElement.GetProperty("account_ids").EnumerateArray().Select(value => value.GetInt64()).ToArray());
        Assert.False(request.RootElement.GetProperty("force").GetBoolean());
        var usage = result.Usage["42"];
        Assert.Equal(12.5, usage.FiveHour!.Utilization, 1);
        Assert.Equal(48, usage.FiveHour.WindowStats!.Requests);
        Assert.Equal(3_700_000, usage.FiveHour.WindowStats.Tokens);
        Assert.Equal(2.77, usage.FiveHour.WindowStats.StandardCost, 2);
        Assert.Equal(34, usage.SevenDay!.Utilization);
    }

    [Fact]
    public async Task AccountActiveUsageAndOpenAIQuotaUseDedicatedOfficialRoutes()
    {
        var handler = new AccountHandler("data: {\"type\":\"test_complete\",\"success\":true}\n\n");
        var api = CreateApi(handler);

        var usage = await api.GetAccountUsageAsync("42", true, "active");
        Assert.Equal("/api/v1/admin/accounts/42/usage", handler.LastRequestPath);
        Assert.Equal("?source=active&force=true", handler.LastRequestQuery);
        Assert.Equal(12.5, usage.FiveHour!.Utilization, 1);

        var quota = await api.RefreshOpenAIQuotaAsync("42");
        Assert.Equal("/api/v1/admin/openai/accounts/42/quota/refresh", handler.LastRequestPath);
        Assert.True(quota.CachePersisted);
        Assert.Equal(2, quota.RateLimitResetCredits!.AvailableCount);

        var reset = await api.ResetOpenAIQuotaAsync("42");
        Assert.Equal("/api/v1/admin/openai/accounts/42/reset-quota", handler.LastRequestPath);
        Assert.Equal(2, reset.WindowsReset);
        Assert.True(reset.CacheRefreshed);
        Assert.Equal(1, reset.Quota!.RateLimitResetCredits!.AvailableCount);
    }

    private static ApiClient CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());

    private static void AssertOfficialAccountPayloadDoesNotContainUpstreamPolicy(string body)
    {
        using var request = JsonDocument.Parse(body);
        Assert.False(request.RootElement.TryGetProperty("weight", out _));
        Assert.False(request.RootElement.TryGetProperty("rpm_limit", out _));
        Assert.False(request.RootElement.TryGetProperty("circuit_breaker_threshold", out _));
        Assert.False(request.RootElement.TryGetProperty("circuit_breaker_cooldown_seconds", out _));
        Assert.False(request.RootElement.GetProperty("extra").TryGetProperty("auth_type", out _));
    }

    private sealed class NullJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }

    private sealed class AccountHandler(string testSse) : HttpMessageHandler
    {
        public string LastRequestBody { get; private set; } = string.Empty;
        public string LastRequestPath { get; private set; } = string.Empty;
        public string LastRequestQuery { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            LastRequestPath = path;
            LastRequestQuery = request.RequestUri?.Query ?? string.Empty;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/accounts/generate-auth-url")
            {
                return JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"auth_url\":\"https://platform.claude.com/oauth/authorize?state=state-1\",\"session_id\":\"claude-session-1\"}}");
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/openai/generate-auth-url")
            {
                return JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"auth_url\":\"https://auth.openai.com/oauth/authorize?client_id=codex-client&redirect_uri=http%3A%2F%2Flocalhost%3A1455%2Fauth%2Fcallback&state=state-1\",\"session_id\":\"openai-session-1\"}}");
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/accounts/exchange-code")
            {
                return JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"access_token\":\"claude-access-1\",\"refresh_token\":\"claude-refresh-1\",\"expires_in\":3600,\"expires_at\":1770000000}}");
            }

            if ((request.Method == HttpMethod.Post && path == "/api/v1/admin/groups")
                || (request.Method == HttpMethod.Put && path == "/api/v1/admin/groups/12"))
            {
                return GroupResponse();
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/accounts")
            {
                return OfficialAccountResponse();
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/openai/create-from-oauth")
            {
                return OfficialAccountResponse();
            }

            if (request.Method == HttpMethod.Put && path == "/api/v1/admin/accounts/42")
            {
                return OfficialAccountResponse();
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/upstream-accounts")
            {
                return UpstreamAccountResponse();
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/upstream-accounts/upstream-42/test-connection")
            {
                return JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"success\":true,\"code\":\"connection_succeeded\",\"message\":\"连接成功\",\"latency_ms\":12}}");
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/accounts/42/test")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(testSse, Encoding.UTF8, "text/event-stream")
                };
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/accounts/usage/batch")
            {
                return JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"usage\":{\"42\":{\"source\":\"passive\",\"five_hour\":{\"utilization\":12.5,\"remaining_seconds\":3600,\"window_stats\":{\"requests\":48,\"tokens\":3700000,\"cost\":2.77,\"standard_cost\":2.77,\"user_cost\":2.77}},\"seven_day\":{\"utilization\":34}}},\"errors\":{}}}");
            }

            if (request.Method == HttpMethod.Get && path == "/api/v1/admin/accounts/42/usage")
            {
                return JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"source\":\"active\",\"five_hour\":{\"utilization\":12.5},\"seven_day\":{\"utilization\":34}}}");
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/openai/accounts/42/quota/refresh")
            {
                return JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"fetched_at\":1770000000,\"cache_persisted\":true,\"rate_limit_reset_credits\":{\"available_count\":2,\"credits\":[{\"expires_at\":\"2026-08-21T00:00:00Z\"}]}}}");
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/openai/accounts/42/reset-quota")
            {
                return JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"code\":\"ok\",\"windows_reset\":2,\"cache_refreshed\":true,\"account_state_recovered\":true,\"quota\":{\"fetched_at\":1770000001,\"rate_limit_reset_credits\":{\"available_count\":1}}}}");
            }

            if (request.Method == HttpMethod.Get && path == "/api/v1/admin/accounts/42/stats")
            {
                return AccountStatsResponse();
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/accounts/models/sync-upstream-preview")
            {
                return JsonModelsResponse();
            }

            if (request.Method == HttpMethod.Post && path == "/api/v1/admin/accounts/42/models/sync-upstream")
            {
                return JsonModelsResponse();
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"code\":404,\"message\":\"not found\"}", Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage JsonModelsResponse() => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"code\":0,\"message\":\"success\",\"data\":{\"models\":[\"gpt-5.2\",\"gpt-5.3-codex\"]}}",
                Encoding.UTF8,
                "application/json")
        };

        private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        private static HttpResponseMessage GroupResponse() =>
            JsonResponse("""{"code":0,"message":"success","data":{"id":12,"name":"OpenAI 计费分组","platform":"openai","status":"active","long_context_pricing_enabled":true}}""");

        private static HttpResponseMessage OfficialAccountResponse() =>
            JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"id\":42,\"name\":\"runtime-policy\",\"platform\":\"openai\",\"type\":\"apikey\",\"status\":\"active\",\"schedulable\":true,\"concurrency\":8,\"priority\":100}}");

        private static HttpResponseMessage AccountStatsResponse() =>
            JsonResponse("""
                {"code":0,"message":"success","data":{
                  "history":[{"date":"2026-08-20","label":"08-20","requests":12,"tokens":3400,"cost":1.5,"actual_cost":1.2,"user_cost":1.8}],
                  "summary":{"days":30,"actual_days_used":4,"total_cost":12.34,"total_user_cost":15.2,"total_standard_cost":14.1,"total_requests":128,"total_tokens":54321,"avg_daily_cost":3.085,"avg_daily_user_cost":3.8,"avg_daily_requests":32,"avg_daily_tokens":13580.25,"avg_duration_ms":824,"today":{"date":"2026-08-20","cost":1.2,"user_cost":1.8,"requests":12,"tokens":3400},"highest_cost_day":{"date":"2026-08-18","label":"08-18","cost":5.4,"user_cost":6.1,"requests":44},"highest_request_day":{"date":"2026-08-19","label":"08-19","requests":52,"cost":4.7,"user_cost":5.5}},
                  "models":[{"model":"gpt-5.6-sol","requests":128,"input_tokens":30000,"output_tokens":12000,"cache_creation_tokens":5000,"cache_read_tokens":7321,"total_tokens":54321,"cost":14.1,"actual_cost":15.2,"account_cost":12.34}],
                  "endpoints":[{"endpoint":"/v1/responses","requests":128,"total_tokens":54321,"cost":14.1,"actual_cost":15.2}],
                  "upstream_endpoints":[{"endpoint":"/backend-api/codex/responses","requests":128,"total_tokens":54321,"cost":14.1,"actual_cost":15.2}]
                }}
                """);

        private static HttpResponseMessage UpstreamAccountResponse() =>
            JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"id\":\"upstream-42\",\"name\":\"openai-wif\",\"provider_type\":\"openai\",\"base_url\":\"https://api.openai.com\",\"auth_type\":\"wif\",\"masked_credential\":\"********cret\",\"is_active\":true,\"priority\":100,\"weight\":250,\"max_concurrency\":8,\"rpm_limit\":120,\"circuit_breaker_threshold\":4,\"circuit_breaker_cooldown_seconds\":90,\"quota_status\":\"unknown\",\"usage_windows\":{},\"created_at\":\"2026-08-15T00:00:00Z\",\"updated_at\":\"2026-08-15T00:00:00Z\"}}");
    }
}

internal static class UriQueryTestExtensions
{
    public static string? GetQueryValue(this Uri uri, string key)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]).Equals(key, StringComparison.Ordinal))
            {
                return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }
        }

        return null;
    }
}
