using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ModelSyncPageTests
{
    [Fact]
    public void ModelsPageExposesAccountSelectionAndUpstreamSyncWorkflow()
    {
        var source = File.ReadAllText(FindSourceFile("Pages", "Models.razor"));

        Assert.Contains("同步上游模型", source, StringComparison.Ordinal);
        Assert.Contains("Api.SyncAccountModelsAsync", source, StringComparison.Ordinal);
        Assert.Contains("Api.GetAccountModelsAsync", source, StringComparison.Ordinal);
        Assert.Contains("请选择账号", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiClientExposesOfficialAccountModelEndpoints()
    {
        var source = File.ReadAllText(FindSourceFile("Services", "ApiClient.cs"));

        Assert.Contains("/admin/accounts/", source, StringComparison.Ordinal);
        Assert.Contains("/models/sync-upstream", source, StringComparison.Ordinal);
    }

    private static string FindSourceFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[] { directory.FullName }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }
}
