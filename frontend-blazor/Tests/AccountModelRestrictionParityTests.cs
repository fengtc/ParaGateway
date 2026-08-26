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

        Assert.Contains("@if (AccountModelRestrictions.ShouldShow(form.Platform, form.Type))", create, StringComparison.Ordinal);
        Assert.Contains("<AccountModelRestrictionEditor @key=\"ModelRestrictionEditorKey\" Model=\"form\" />", create, StringComparison.Ordinal);
        Assert.Contains("<AccountModelRestrictionEditor Model=\"form\" AccountId=\"editingId\"", edit, StringComparison.Ordinal);
        Assert.Contains("RestrictionPlatformOverride=\"@(editingIsCopilot ? \"copilot\" : null)\"", edit, StringComparison.Ordinal);
        Assert.Contains("\"apikey\" => \"apikey\"", create, StringComparison.Ordinal);
        Assert.Contains("模型限制（可选）", component, StringComparison.Ordinal);
        Assert.Contains("模型白名单", component, StringComparison.Ordinal);
        Assert.Contains("模型映射", component, StringComparison.Ordinal);
        Assert.Contains("同步最新支持模型", component, StringComparison.Ordinal);
        Assert.Contains("填入相关模型", component, StringComparison.Ordinal);
        Assert.Contains("清除所有模型", component, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiKeyCreateAndEditKeepTheOfficialInlineFieldOrderWithoutUpstreamRedirect()
    {
        var create = Read("Components", "NativeAccountCreateModal.razor");
        var edit = Read("Pages", "Providers.razor");

        AssertApiKeyFieldOrder(create, "native-base-url", "native-api-key", "AccountModelRestrictionEditor");
        AssertApiKeyFieldOrder(edit, "account-base-url", "account-api-key", "AccountModelRestrictionEditor");
        Assert.DoesNotContain("前往兼容上游连接", create, StringComparison.Ordinal);
        Assert.DoesNotContain("前往兼容上游连接", edit, StringComparison.Ordinal);
        Assert.DoesNotContain("Worker 风格的连接", edit, StringComparison.Ordinal);
    }

    private static void AssertApiKeyFieldOrder(string source, string baseUrlId, string apiKeyId, string restrictionEditor)
    {
        var baseUrl = source.IndexOf(baseUrlId, StringComparison.Ordinal);
        var apiKey = source.IndexOf(apiKeyId, StringComparison.Ordinal);
        var restrictions = source.IndexOf(restrictionEditor, StringComparison.Ordinal);

        Assert.True(baseUrl >= 0, $"Missing {baseUrlId}");
        Assert.True(apiKey > baseUrl, $"Expected {apiKeyId} after {baseUrlId}");
        Assert.True(restrictions > apiKey, $"Expected {restrictionEditor} after {apiKeyId}");
    }

    private static string Read(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. parts]));
    }
}
