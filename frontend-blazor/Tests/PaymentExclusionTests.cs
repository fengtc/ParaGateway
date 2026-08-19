using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class PaymentExclusionTests
{
    [Fact]
    public void BlazorSurfaceDoesNotExposeOfficialPaymentRoutesOrProviders()
    {
        var root = FindFrontendRoot();
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "legal" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetExtension(path) is ".razor" or ".cs" or ".js" or ".html")
            .ToArray();

        var source = string.Join("\n", files.Select(File.ReadAllText));

        Assert.DoesNotContain("@page \"/purchase\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@page \"/orders\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@page \"/payment/", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stripe", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("airwallex", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/auth/wechat/payment", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/payment/", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubscriptionAndRedeemPagesRemainAvailableWithoutPurchaseFlow()
    {
        var root = FindFrontendRoot();
        var subscriptions = File.ReadAllText(Path.Combine(root, "Pages", "Subscriptions.razor"));
        var redeem = File.ReadAllText(Path.Combine(root, "Pages", "Redeem.razor"));

        Assert.Contains("@page \"/subscriptions\"", subscriptions, StringComparison.Ordinal);
        Assert.Contains("GetMySubscriptionsAsync", subscriptions, StringComparison.Ordinal);
        Assert.Contains("@page \"/redeem\"", redeem, StringComparison.Ordinal);
        Assert.Contains("RedeemAsync", redeem, StringComparison.Ordinal);
    }

    [Fact]
    public void PromoCodePageUsesTypedCrudSurfaceWithoutPaymentRoutes()
    {
        var root = FindFrontendRoot();
        var page = File.ReadAllText(Path.Combine(root, "Pages", "AdminPromoCodes.razor"));
        var client = File.ReadAllText(Path.Combine(root, "Services", "ApiClient.cs"));

        Assert.Contains("@page \"/admin/promo-codes\"", page, StringComparison.Ordinal);
        Assert.Contains("<DxGrid", page, StringComparison.Ordinal);
        Assert.Contains("CreateAdminPromoCodeAsync", page, StringComparison.Ordinal);
        Assert.Contains("UpdateAdminPromoCodeAsync", page, StringComparison.Ordinal);
        Assert.Contains("DeleteAdminPromoCodeAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetAdminPromoCodeUsagesAsync", page, StringComparison.Ordinal);
        Assert.Contains("/admin/promo-codes", client, StringComparison.Ordinal);
        Assert.DoesNotContain("/payment/", page, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindFrontendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ParaGateway.Frontend.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Blazor frontend root.");
    }
}
