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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            LastRequestPath = path;
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

        private static HttpResponseMessage OfficialAccountResponse() =>
            JsonResponse("{\"code\":0,\"message\":\"success\",\"data\":{\"id\":42,\"name\":\"runtime-policy\",\"platform\":\"openai\",\"type\":\"apikey\",\"status\":\"active\",\"schedulable\":true,\"concurrency\":8,\"priority\":100}}");

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
