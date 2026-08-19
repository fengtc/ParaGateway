using Xunit;
using System.Text.RegularExpressions;

namespace ParaGateway.Frontend.Tests;

public sealed class FormControlConsistencyTests
{
    [Fact]
    public void GlobalFormContractStylesTextInputsSelectsAndTextareasConsistently()
    {
        var css = Read("wwwroot", "css", "app.css");

        Assert.Contains("--form-control-height: 40px;", css);
        Assert.Contains("--form-control-border:", css);
        Assert.Contains("--form-control-radius:", css);
        Assert.Contains(".form-field input,\n.form-field select,\n.form-field textarea", css);
        Assert.Contains(".form-field textarea {", css);
        Assert.Contains(".form-field textarea::placeholder", css);
        Assert.Contains(".form-field textarea:focus", css);
        Assert.Contains("height: var(--form-control-height);", css);
    }

    [Fact]
    public void GlobalFormAndSettingsGridsStartAlignTheirChildren()
    {
        var css = Read("wwwroot", "css", "app.css");

        Assert.Contains(".form-grid {\n    display: grid;\n    grid-template-columns: repeat(2, minmax(0, 1fr));\n    align-items: start;", css);
        Assert.Contains(".settings-grid {\n    display: grid;\n    grid-template-columns: repeat(2, minmax(0, 1fr));\n    align-items: start;", css);
        Assert.Contains("align-self: start;\n    align-content: start;", css);
    }

    [Fact]
    public void SharedFieldAppliesTheSameControlContractAcrossIsolatedComponents()
    {
        var css = Read("Components", "Field.razor.css");

        Assert.Contains(".settings-field-control ::deep input,\n.settings-field-control ::deep select,\n.settings-field-control ::deep textarea", css);
        Assert.Contains("min-height: var(--form-control-height);", css);
        Assert.Contains("border: 1px solid var(--form-control-border);", css);
        Assert.Contains("border-radius: var(--form-control-radius);", css);
        Assert.Contains("box-shadow: 0 0 0 3px var(--form-control-focus-ring);", css);
    }

    [Fact]
    public void AdvancedFormSurfacesExplicitlyStartAlignTheirGrids()
    {
        var files = new[]
        {
            (new[] { "Pages", "AdminSettings.razor.css" }, ".settings-grid"),
            (new[] { "Pages", "AdminDataManagement.razor.css" }, ".modal-form-grid"),
            (new[] { "Pages", "AdminRiskControl.razor.css" }, ".risk-form"),
            (new[] { "Pages", "AdminPromptAudit.razor.css" }, ".endpoint-form"),
            (new[] { "Components", "AccountUsageRuntimeSettings.razor.css" }, ".runtime-grid"),
            (new[] { "Components", "AgreementDocumentsEditor.razor.css" }, ".document-grid"),
            (new[] { "Components", "BetaPolicySettings.razor.css" }, ".beta-grid"),
            (new[] { "Components", "CustomNavigationSettings.razor.css" }, ".endpoint-grid"),
            (new[] { "Components", "GatewayAdvancedSettings.razor.css" }, ".advanced-grid"),
            (new[] { "Components", "WebSearchSettings.razor.css" }, ".provider-grid"),
            (new[] { "Components", "EmailTemplateEditor.razor.css" }, ".template-selectors"),
            (new[] { "Components", "SecurityAdvancedSettings.razor.css" }, ".security-provider-grid"),
            (new[] { "Components", "CompositeRoutesModal.razor.css" }, ".route-form-grid")
        };

        foreach (var (path, selector) in files)
        {
            var css = Read(path);
            var pattern = $@"{Regex.Escape(selector)}[^{{}}]*\{{[^{{}}]*align-items\s*:\s*start";
            Assert.Matches(pattern, css);
        }
    }

    [Fact]
    public void GroupEditorUsesTheSameFormFieldContractForNameDescriptionPlatformAndStatus()
    {
        var page = Read("Pages", "Groups.razor");

        Assert.Contains("<div class=\"form-field full\"><label for=\"group-name\">名称</label>", page);
        Assert.Contains("<div class=\"form-field full\"><label for=\"group-description\">说明</label><InputTextArea", page);
        Assert.Contains("<div class=\"form-field\"><label for=\"group-platform\">平台</label><InputSelect", page);
        Assert.Contains("<div class=\"form-field\"><label for=\"group-status\">状态</label><select", page);
    }

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments])).ReplaceLineEndings("\n");
    }
}
