using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class NativeAccountCreateModalStyleTests
{
    [Fact]
    public void CreateAccountUsesTheSharedSystemModalAndButtons()
    {
        var markup = Read("Components", "NativeAccountCreateModal.razor");

        Assert.Contains("<AppModal Open=\"@Open\" Title=\"添加账号\" MaxWidth=\"980px\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"button primary\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"button secondary\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("native-account-backdrop", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("native-account-dialog", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("native-account-footer", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("native-button", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateAccountStylesUseTheSharedThemeAndFormMetrics()
    {
        var styles = Read("Components", "NativeAccountCreateModal.razor.css");

        Assert.Contains("var(--form-control-height)", styles, StringComparison.Ordinal);
        Assert.Contains("var(--form-control-bg)", styles, StringComparison.Ordinal);
        Assert.Contains("var(--form-control-radius)", styles, StringComparison.Ordinal);
        Assert.Contains("var(--surface)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 560px)", styles, StringComparison.Ordinal);
        Assert.DoesNotContain(".native-account-backdrop", styles, StringComparison.Ordinal);
        Assert.DoesNotContain(".native-account-dialog", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("#1e293b", styles, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }
}
