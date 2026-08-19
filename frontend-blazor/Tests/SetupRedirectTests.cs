using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class SetupRedirectTests
{
    [Fact]
    public void AppChecksSetupStatusAndLeavesSetupRouteAlone()
    {
        var app = ReadSource("App.razor");
        Assert.Contains("RedirectToSetupIfNeededAsync", app, StringComparison.Ordinal);
        Assert.Contains("GetSetupStatusAsync", app, StringComparison.Ordinal);
        Assert.Contains("path.Equals(\"setup\"", app, StringComparison.Ordinal);
        Assert.Contains("NavigateTo(\"/setup\"", app, StringComparison.Ordinal);
    }

    private static string ReadSource(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, name);
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate {name}");
    }
}
