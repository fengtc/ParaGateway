using System.Text.Json;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ModelPlazaPageParityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void PageMatchesOfficialFacetedModelPlazaSurface()
    {
        var page = ReadSource("Pages", "ModelPlaza.razor");

        Assert.Contains("按分组浏览可用模型与价格", page, StringComparison.Ordinal);
        Assert.Contains("登录后可查看你的专属分组与专属倍率", page, StringComparison.Ordinal);
        Assert.Contains("plaza-filter-label\">平台", page, StringComparison.Ordinal);
        Assert.Contains("plaza-filter-label\">分组", page, StringComparison.Ordinal);
        Assert.Contains("plaza-filter-label\">倍率", page, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"搜索模型名称\"", page, StringComparison.Ordinal);
        Assert.Contains("PlatformEnabled", page, StringComparison.Ordinal);
        Assert.Contains("GroupEnabled", page, StringComparison.Ordinal);
        Assert.Contains("RateEnabled", page, StringComparison.Ordinal);
        Assert.Contains("UserRateMultiplier", page, StringComparison.Ordinal);
        Assert.Contains("专属分组", page, StringComparison.Ordinal);
        Assert.Contains("高峰时段", page, StringComparison.Ordinal);
        Assert.Contains("ModelPlazaPricingTable", page, StringComparison.Ordinal);
        Assert.DoesNotContain("DxGrid", page, StringComparison.Ordinal);
        Assert.DoesNotContain("购买", page, StringComparison.Ordinal);
        Assert.DoesNotContain("支付", page, StringComparison.Ordinal);
    }

    [Fact]
    public void PricingTableSupportsEveryOfficialBillingPresentation()
    {
        var table = ReadSource("Components", "ModelPlazaPricingTable.razor");

        Assert.Contains("实付价格（折后）", table, StringComparison.Ordinal);
        Assert.Contains("官方价格", table, StringComparison.Ordinal);
        Assert.Contains("$ / 1M token", table, StringComparison.Ordinal);
        Assert.Contains("CacheWrite1hPrice", table, StringComparison.Ordinal);
        Assert.Contains("TokenIntervals", table, StringComparison.Ordinal);
        Assert.Contains("RequestIntervals", table, StringComparison.Ordinal);
        Assert.Contains("按图片计费", table, StringComparison.Ordinal);
        Assert.Contains("按次计费", table, StringComparison.Ordinal);
        Assert.Contains("ImageRateIndependent", table, StringComparison.Ordinal);
        Assert.Contains("PerUnitSuffix", table, StringComparison.Ordinal);
        Assert.Contains("HasCustomRate", table, StringComparison.Ordinal);
        Assert.Contains("TierLabel", table, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelPlazaContractPreservesRatesAndTypedPrices()
    {
        var value = JsonSerializer.Deserialize<ModelPlazaResponse>("""
            {
              "description": "**公开价格**",
              "groups": [{
                "id": 7,
                "name": "OpenAI Pro",
                "platform": "openai",
                "subscription_type": "subscription",
                "rate_multiplier": 0.8,
                "user_rate_multiplier": 0.6,
                "peak_rate_enabled": true,
                "peak_start": "18:00",
                "peak_end": "23:00",
                "peak_rate_multiplier": 1.5,
                "is_exclusive": true,
                "image_rate_independent": true,
                "image_rate_multiplier": 0.5,
                "models": [{
                  "name": "gpt-5.6",
                  "platform": "openai",
                  "pricing": {
                    "billing_mode": "token",
                    "input_price": 0.000001,
                    "output_price": 0.000004,
                    "cache_write_price": 0.00000125,
                    "cache_read_price": 0.0000001,
                    "intervals": [{
                      "min_tokens": 0,
                      "max_tokens": 200000,
                      "tier_label": "≤200K",
                      "input_price": 0.000001,
                      "output_price": 0.000004
                    }]
                  },
                  "official_pricing": {
                    "input_price": 0.00000125,
                    "output_price": 0.00001,
                    "cache_write_price": 0.00000125,
                    "cache_write_1h_price": 0.000002,
                    "cache_read_price": 0.0000001
                  }
                }]
              }]
            }
            """, Json);

        Assert.NotNull(value);
        var group = Assert.Single(value.Groups);
        Assert.Equal(0.6, group.UserRateMultiplier);
        Assert.True(group.PeakRateEnabled);
        Assert.True(group.ImageRateIndependent);
        Assert.Equal(0.5, group.ImageRateMultiplier);
        var model = Assert.Single(group.Models);
        Assert.Equal("token", model.Pricing?.BillingMode);
        Assert.Equal(0.000004, model.Pricing?.OutputPrice);
        Assert.Equal("≤200K", Assert.Single(model.Pricing!.Intervals).TierLabel);
        Assert.Equal(0.000002, model.OfficialPricing?.CacheWrite1hPrice);
    }

    [Fact]
    public void StandaloneAndEmbeddedRoutesUseOfficialLayoutsAndFeatureGate()
    {
        var guard = ReadSource("Components", "RouteGuard.razor");
        var layout = ReadSource("Layout", "MainLayout.razor");
        var page = ReadSource("Pages", "ModelPlaza.razor");

        Assert.Contains("typeof(BareLayout)", guard, StringComparison.Ordinal);
        Assert.Contains("IsEmbeddedModelPlaza", guard, StringComparison.Ordinal);
        Assert.Contains("!IsPublicPage && Auth.IsAuthenticated", guard, StringComparison.Ordinal);
        Assert.Contains("publicSettings?.ModelPlazaEnabled == true", layout, StringComparison.Ordinal);
        Assert.Contains("href=\"/model-plaza?embedded=1\"", layout, StringComparison.Ordinal);
        Assert.Contains("settings.ModelPlazaRequireAuth", page, StringComparison.Ordinal);
        Assert.Contains("SafeImageUrl", page, StringComparison.Ordinal);
        Assert.Contains("SiteLogo", ReadSource("Models", "Dtos.cs"), StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }
}
