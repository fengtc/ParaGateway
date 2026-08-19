using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class RedeemPageTests
{
    [Fact]
    public void RedeemPageUsesOfficialTypedContractAndCompleteUserSurface()
    {
        var page = ReadSource("Pages", "Redeem.razor");
        var client = ReadSource("Services", "ApiClient.cs");
        var models = ReadSource("Models", "Dtos.cs");

        Assert.Contains("Auth.User?.Balance", page, StringComparison.Ordinal);
        Assert.Contains("Auth.User?.Concurrency", page, StringComparison.Ordinal);
        Assert.Contains("GetRedeemHistoryAsync", page, StringComparison.Ordinal);
        Assert.Contains("Api.RedeemAsync", page, StringComparison.Ordinal);
        Assert.Contains("GetPublicSettingsAsync", page, StringComparison.Ordinal);
        Assert.Contains("ContactInfo", page, StringComparison.Ordinal);
        Assert.Contains("Auth.RefreshAsync", page, StringComparison.Ordinal);
        Assert.Contains("ResultTitle", page, StringComparison.Ordinal);
        Assert.Contains("FormatHistoryValue", page, StringComparison.Ordinal);
        Assert.Contains("admin_balance", page, StringComparison.Ordinal);
        Assert.Contains("admin_concurrency", page, StringComparison.Ordinal);
        Assert.Contains("subscription", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRedeemHistoryRawAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonElement", page, StringComparison.Ordinal);

        Assert.Contains("Task<RedeemCodeDto> RedeemAsync", client, StringComparison.Ordinal);
        Assert.Contains("Task<List<RedeemCodeDto>> GetRedeemHistoryAsync", client, StringComparison.Ordinal);
        Assert.Contains("public sealed class RedeemCodeDto", models, StringComparison.Ordinal);
        Assert.Contains("public int Concurrency", models, StringComparison.Ordinal);
        Assert.Contains("contact_info", models, StringComparison.Ordinal);
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
