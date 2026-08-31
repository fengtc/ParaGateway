using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AdminParityContractTests
{
    [Fact]
    public void AffiliateAdminPageUsesTypedOfficialSurfaces()
    {
        var page = ReadPage("AdminAffiliates.razor");
        var client = ReadSource("Services", "ApiClient.cs");

        Assert.Contains("GetAffiliateAdminUsersAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetAffiliateInvitesAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetAffiliateRebatesAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetAffiliateTransfersAsync", page, StringComparison.Ordinal);
        Assert.Contains("LookupAffiliateUsersAsync", page, StringComparison.Ordinal);
        Assert.Contains("BatchSetAffiliateRateAsync", page, StringComparison.Ordinal);
        Assert.Contains("clear_rebate_rate", page, StringComparison.Ordinal);
        Assert.Contains("<DxGrid", page, StringComparison.Ordinal);
        Assert.Contains("/admin/affiliates/users", client, StringComparison.Ordinal);
        Assert.Contains("BuildAffiliateRecordUrl(\"invites\"", client, StringComparison.Ordinal);
        Assert.Contains("BuildAffiliateRecordUrl(\"rebates\"", client, StringComparison.Ordinal);
        Assert.Contains("BuildAffiliateRecordUrl(\"transfers\"", client, StringComparison.Ordinal);
    }

    [Fact]
    public void ChannelMonitorPageCoversV1AndV2OfficialRoutes()
    {
        var page = ReadPage("AdminChannelMonitor.razor");
        var client = ReadSource("Services", "ApiClient.cs");

        foreach (var range in new[] { "90m", "24h", "7d", "30d" })
            Assert.Contains($"value=\"{range}\"", page, StringComparison.Ordinal);
        Assert.Contains("GetAdminChannelMonitorsAsync", page, StringComparison.Ordinal);
        Assert.Contains("DuplicateAdminChannelMonitorAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetAdminChannelMonitorHistoryAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetChannelMonitorV2SnapshotAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetChannelMonitorV2ModelsAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetChannelMonitorV2ErrorsAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetChannelMonitorV2UsersAsync", page, StringComparison.Ordinal);
        Assert.Contains("channel-monitor-v2/config", client, StringComparison.Ordinal);
        Assert.Contains("channel-monitor-templates", client, StringComparison.Ordinal);
        Assert.Contains("GetChannelMonitorTemplateMonitorsAsync", page, StringComparison.Ordinal);
        Assert.Contains("ApplyChannelMonitorTemplateAsync", page, StringComparison.Ordinal);
        Assert.Contains("extra_headers", page, StringComparison.Ordinal);
        Assert.Contains("body_override_mode", page, StringComparison.Ordinal);
        Assert.Contains("template_id", page, StringComparison.Ordinal);
    }

    [Fact]
    public void DeprecatedDataManagementRouteRedirectsToBackupManagement()
    {
        var page = ReadPage("AdminDataManagement.razor");
        Assert.Contains("@page \"/admin/data-management\"", page, StringComparison.Ordinal);
        Assert.Contains("Navigation.NavigateTo(\"/admin/backups\", replace: true)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAdminDataManagementHealthAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateAdminDataManagementConfigAsync", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminSurfacesDoNotAddPaymentPurchaseRoutes()
    {
        var page = ReadPage("AdminAffiliates.razor");
        Assert.DoesNotContain("@page \"/purchase\"", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@page \"/orders\"", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/payment/", page, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadPage(string name) => ReadSource("Pages", name);

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
