using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AccountModelRestrictionParityTests
{
    [Fact]
    public void CreateAndEditAccountSurfacesUseSharedModelRestrictionEditor()
    {
        var create = Read("Components", "NativeAccountCreateModal.razor");
        var edit = Read("Pages", "Providers.razor");
        var component = Read("Components", "AccountModelRestrictionEditor.razor");

        Assert.Contains("<AccountModelRestrictionEditor Model=\"form\" />", create, StringComparison.Ordinal);
        Assert.Contains("<AccountModelRestrictionEditor Model=\"form\" AccountId=\"editingId\"", edit, StringComparison.Ordinal);
        Assert.Contains("RestrictionPlatformOverride=\"@(editingIsCopilot ? \"copilot\" : null)\"", edit, StringComparison.Ordinal);
        Assert.Contains("模型限制（可选）", component, StringComparison.Ordinal);
        Assert.Contains("模型白名单", component, StringComparison.Ordinal);
        Assert.Contains("模型映射", component, StringComparison.Ordinal);
        Assert.Contains("同步最新支持模型", component, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. parts]));
    }
}
