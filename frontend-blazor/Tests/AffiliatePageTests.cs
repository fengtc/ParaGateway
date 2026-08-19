using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AffiliatePageTests
{
    [Fact]
    public void AffiliatePageMatchesOfficialTypedSurface()
    {
        var page = ReadSource("Pages", "Affiliate.razor");
        var client = ReadSource("Services", "ApiClient.cs");
        var models = ReadSource("Models", "Dtos.cs");

        Assert.Contains("GetAffiliateDetailAsync", page, StringComparison.Ordinal);
        Assert.Contains("TransferAffiliateQuotaAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAffiliateDetailRawAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize", page, StringComparison.Ordinal);
        Assert.Contains("EffectiveRebateRatePercent", page, StringComparison.Ordinal);
        Assert.Contains("AffFrozenQuota", page, StringComparison.Ordinal);
        Assert.Contains("AffHistoryQuota", page, StringComparison.Ordinal);
        Assert.Contains("InviteLink", page, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard.writeText", page, StringComparison.Ordinal);
        Assert.Contains("detail.Invitees", page, StringComparison.Ordinal);
        Assert.Contains("Auth.RefreshAsync", page, StringComparison.Ordinal);

        Assert.Contains("Task<UserAffiliateDetailDto> GetAffiliateDetailAsync", client, StringComparison.Ordinal);
        Assert.Contains("Task<AffiliateTransferResponseDto> TransferAffiliateQuotaAsync", client, StringComparison.Ordinal);
        Assert.Contains("public sealed class UserAffiliateDetailDto", models, StringComparison.Ordinal);
        Assert.Contains("effective_rebate_rate_percent", models, StringComparison.Ordinal);
        Assert.Contains("public sealed class AffiliateInviteeDto", models, StringComparison.Ordinal);
        Assert.Contains("public sealed class AffiliateTransferResponseDto", models, StringComparison.Ordinal);
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
