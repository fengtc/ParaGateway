using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ChannelTimePricingTests
{
    [Fact]
    public void ValidTimePricingAcceptsSecondAndLegacyMinutePrecision()
    {
        const string json = """
            [{
              "models": ["gpt-5"],
              "billing_mode": "token",
              "time_pricing": {
                "timezone": "Asia/Shanghai",
                "periods": [
                  { "start_time": "09:00", "end_time": "12:00", "multiplier": 2 },
                  { "start_time": "12:00:00", "end_time": "18:00:00", "multiplier": 1.25 },
                  { "start_time": "22:00:00", "end_time": "00:00:00", "multiplier": 0.75 }
                ]
              }
            }]
            """;

        Assert.Null(ChannelTimePricingRules.ValidateModelPricingJson(json));
        Assert.Equal("09:00:00", ChannelTimePricingRules.NormalizeClockTime("09:00"));
        Assert.Equal("00:00:00", ChannelTimePricingRules.NormalizeClockTime("24：00：00"));
    }

    [Theory]
    [InlineData("", "", "1.00", true)]
    [InlineData("09:00:00", "", "1.00", true)]
    [InlineData("09:00:00", "12:00:00", "", true)]
    [InlineData("09:00:00", "12:00:00", "1.00", false)]
    public void DraftPeriodRequiresAllFieldsBeforeItCanBeSynchronized(
        string startTime,
        string endTime,
        string multiplier,
        bool expected)
    {
        Assert.Equal(expected, ChannelTimePricingRules.IsDraftPeriodIncomplete(startTime, endTime, multiplier));
    }

    [Theory]
    [MemberData(nameof(InvalidConfigurations))]
    public void InvalidTimePricingReturnsSpecificValidationError(string json, string expected)
    {
        var error = ChannelTimePricingRules.ValidateModelPricingJson(json);

        Assert.NotNull(error);
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> InvalidConfigurations()
    {
        yield return [Pricing("UTC+8", Period("09:00:00", "12:00:00", "2")), "时区"];
        yield return [Pricing("Asia/Shanghai", Period("9:00", "12:00:00", "2")), "HH:mm"];
        yield return [Pricing("Asia/Shanghai", Period("12:00:00", "09:00:00", "2")), "开始时间"];
        yield return [Pricing("Asia/Shanghai", Period("09:00:00", "12:00:00", "2") + "," + Period("11:59:59", "14:00:00", "1")), "不能重叠"];
        yield return [Pricing("Asia/Shanghai", Period("09:00:00", "12:00:00", "0.001")), "最多保留两位"];
        yield return [Pricing("Asia/Shanghai", Period("09:00:00", "12:00:00", "1.001")), "最多保留两位"];
        yield return [Pricing("Asia/Shanghai", Period("09:00:00", "12:00:00", "2"), "per_request"), "只有 Token"];
    }

    [Fact]
    public async Task ChannelApiPreservesValidTimePricingAndRejectsInvalidBeforeRequest()
    {
        var handler = new ChannelHandler();
        var api = CreateApi(handler);
        const string valid = """
            [{"models":["gpt-5"],"billing_mode":"token","time_pricing":{"timezone":"Asia/Shanghai","periods":[{"start_time":"09:00:00","end_time":"12:00:00","multiplier":1.5}]}}]
            """;

        await api.CreateChannelAsync(new ChannelInput { Name = "主渠道", ModelPricingJson = valid });

        Assert.Equal(1, handler.RequestCount);
        using (var body = JsonDocument.Parse(handler.LastBody))
        {
            var timePricing = body.RootElement.GetProperty("model_pricing")[0].GetProperty("time_pricing");
            Assert.Equal("Asia/Shanghai", timePricing.GetProperty("timezone").GetString());
            Assert.Equal(1.5, timePricing.GetProperty("periods")[0].GetProperty("multiplier").GetDouble());
        }

        var invalid = Pricing("Asia/Shanghai", Period("09:00:00", "12:00:00", "1.001"));
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            api.CreateChannelAsync(new ChannelInput { Name = "无效渠道", ModelPricingJson = invalid }));
        Assert.Contains("最多保留两位小数", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public void ChannelPageUsesStructuredEditorAndApiLevelValidation()
    {
        var page = Read("Pages", "Channels.razor");
        var editor = Read("Components", "ChannelTimePricingEditor.razor");
        var client = Read("Services", "ApiClient.cs");

        Assert.Contains("<ChannelTimePricingEditor", page, StringComparison.Ordinal);
        Assert.Contains("ValidateAndSyncAsync", page, StringComparison.Ordinal);
        Assert.Contains("IANA 时区", editor, StringComparison.Ordinal);
        Assert.Contains("添加时段", editor, StringComparison.Ordinal);
        Assert.Contains("start_time", editor, StringComparison.Ordinal);
        Assert.Contains("end_time", editor, StringComparison.Ordinal);
        Assert.Contains("multiplier", editor, StringComparison.Ordinal);
        Assert.Contains("ApplyChangesAsync(validateIncomplete: true)", editor, StringComparison.Ordinal);
        Assert.Contains("IsDraftPeriodIncomplete", editor, StringComparison.Ordinal);
        Assert.Contains("private void AddPeriod", editor, StringComparison.Ordinal);
        Assert.Contains("ChannelTimePricingRules.ValidateModelPricingJson(input.ModelPricingJson)", client, StringComparison.Ordinal);
    }

    private static string Period(string start, string end, string multiplier) =>
        "{\"start_time\":\"" + start + "\",\"end_time\":\"" + end + "\",\"multiplier\":" + multiplier + "}";

    private static string Pricing(string timezone, string periods, string billingMode = "token") =>
        "[{\"billing_mode\":\"" + billingMode + "\",\"time_pricing\":{\"timezone\":\"" + timezone + "\",\"periods\":[" + periods + "]}}]";

    private static ApiClient CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://paragateway.test") }, new NullJsRuntime());

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private sealed class NullJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class ChannelHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"code\":0,\"message\":\"success\",\"data\":{\"id\":1,\"name\":\"主渠道\",\"status\":\"active\",\"group_ids\":[],\"model_pricing\":[]}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
