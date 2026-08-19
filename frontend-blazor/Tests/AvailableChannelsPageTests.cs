using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AvailableChannelsPageTests
{
    [Fact]
    public void AvailableChannelsPageCoversOfficialGroupedChannelSurface()
    {
        var page = ReadSource("Pages", "AvailableChannels.razor");
        var client = ReadSource("Services", "ApiClient.cs");
        var models = ReadSource("Models", "Dtos.cs");
        var groupList = ReadSource("Components", "AvailableGroupList.razor");
        var modelChip = ReadSource("Components", "SupportedModelChip.razor");

        Assert.Contains("Api.GetAvailableChannelsAsync", page, StringComparison.Ordinal);
        Assert.Contains("Api.GetUserGroupRatesAsync", page, StringComparison.Ordinal);
        Assert.Contains("搜索渠道或模型", page, StringComparison.Ordinal);
        Assert.Contains("section.Groups.Any", page, StringComparison.Ordinal);
        Assert.Contains("section.SupportedModels.Any", page, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"desktop-channels\"", page, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"mobile-channels\"", page, StringComparison.Ordinal);
        Assert.Contains("AvailableGroupList", page, StringComparison.Ordinal);
        Assert.Contains("SupportedModelChip", page, StringComparison.Ordinal);

        Assert.DoesNotContain("GetAvailableChannelsRawAsync", client, StringComparison.Ordinal);
        Assert.Contains("Task<List<UserAvailableChannelDto>> GetAvailableChannelsAsync", client, StringComparison.Ordinal);
        Assert.Contains("Task<Dictionary<long, double>> GetUserGroupRatesAsync", client, StringComparison.Ordinal);
        Assert.Contains("public sealed class UserAvailableChannelDto", models, StringComparison.Ordinal);
        Assert.Contains("public sealed class UserSupportedModelPricingDto", models, StringComparison.Ordinal);
        Assert.Contains("supported_models", models, StringComparison.Ordinal);
        Assert.Contains("peak_rate_multiplier", models, StringComparison.Ordinal);

        Assert.Contains("管理员授权给你的专属分组", groupList, StringComparison.Ordinal);
        Assert.Contains("对所有用户公开的分组", groupList, StringComparison.Ordinal);
        Assert.Contains("TryGetCustomRate", groupList, StringComparison.Ordinal);
        Assert.Contains("高峰倍率", groupList, StringComparison.Ordinal);
        Assert.Contains("阶梯定价", modelChip, StringComparison.Ordinal);
        Assert.Contains("按 Token", modelChip, StringComparison.Ordinal);
        Assert.Contains("图片输出", modelChip, StringComparison.Ordinal);
    }

    [Fact]
    public void AvailableChannelContractDeserializesGroupsModelsPricingAndUserRates()
    {
        const string json = """
        [
          {
            "name": "主渠道",
            "description": "生产渠道",
            "platforms": [
              {
                "platform": "openai",
                "groups": [
                  {
                    "id": 42,
                    "name": "专属组",
                    "platform": "openai",
                    "subscription_type": "subscription",
                    "rate_multiplier": 1.2,
                    "peak_rate_enabled": true,
                    "peak_start": "18:00",
                    "peak_end": "22:00",
                    "peak_rate_multiplier": 1.5,
                    "is_exclusive": true
                  }
                ],
                "supported_models": [
                  {
                    "name": "gpt-test",
                    "platform": "openai",
                    "pricing": {
                      "billing_mode": "token",
                      "input_price": 0.000003,
                      "output_price": 0.000015,
                      "cache_write_price": null,
                      "cache_read_price": 0.0000003,
                      "image_input_price": null,
                      "image_output_price": null,
                      "per_request_price": null,
                      "intervals": [
                        {
                          "min_tokens": 0,
                          "max_tokens": 200000,
                          "tier_label": "标准",
                          "input_price": 0.000003,
                          "output_price": 0.000015,
                          "cache_write_price": null,
                          "cache_read_price": 0.0000003,
                          "per_request_price": null
                        }
                      ]
                    }
                  }
                ]
              }
            ]
          }
        ]
        """;

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var channels = JsonSerializer.Deserialize<List<UserAvailableChannelDto>>(json, options);
        var rates = JsonSerializer.Deserialize<Dictionary<long, double>>("{\"42\":0.8}", options);

        var channel = Assert.Single(channels!);
        var section = Assert.Single(channel.Platforms);
        var group = Assert.Single(section.Groups);
        var model = Assert.Single(section.SupportedModels);
        var interval = Assert.Single(model.Pricing!.Intervals);

        Assert.Equal("主渠道", channel.Name);
        Assert.Equal("subscription", group.SubscriptionType);
        Assert.True(group.IsExclusive);
        Assert.True(group.PeakRateEnabled);
        Assert.Equal("gpt-test", model.Name);
        Assert.Equal("token", model.Pricing.BillingMode);
        Assert.Equal(200000, interval.MaxTokens);
        Assert.Equal(0.8, rates![42]);
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }
}
