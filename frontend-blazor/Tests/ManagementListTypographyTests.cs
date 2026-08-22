using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ManagementListTypographyTests
{
    [Theory]
    [InlineData("Subscriptions.razor.css", ".subscriptions-data-table", ".subscriptions-data-table th", ".subscriptions-pagination")]
    [InlineData("Users.razor.css", ".users-data-table", ".users-data-table th", ".table-pagination")]
    [InlineData("ApiKeys.razor.css", ".keys-data-table", ".keys-data-table th", ".keys-pagination")]
    [InlineData("Providers.razor.css", ".accounts-data-table", ".accounts-data-table th", ".accounts-pagination")]
    [InlineData("AuditLogs.razor.css", ".audit-table", ".audit-table th", ".audit-pagination")]
    public void StandardManagementTablesUseTheGroupManagementTypographyScale(
        string fileName,
        string tableSelector,
        string headerSelector,
        string paginationSelector)
    {
        var css = Read("Pages", fileName);

        Assert.Matches($@"{RegexEscape(tableSelector)}[^{{}}]*\{{[^{{}}]*font-size:\s*\.83rem", css);
        Assert.Matches($@"{RegexEscape(headerSelector)}[^{{}}]*\{{[^{{}}]*font-size:\s*\.76rem", css);
        Assert.Matches($@"{RegexEscape(paginationSelector)}[^{{}}]*\{{[^{{}}]*font-size:\s*\.8rem", css);
    }

    [Fact]
    public void GlobalPageListsCannotBeShrunkByIsolatedPageStyles()
    {
        var css = Read("wwwroot", "css", "app.css");

        Assert.Contains("--management-list-body-font: .83rem;", css, StringComparison.Ordinal);
        Assert.Contains("--management-list-header-font: .76rem;", css, StringComparison.Ordinal);
        Assert.Contains("--management-list-primary-font: .88rem;", css, StringComparison.Ordinal);
        Assert.Contains("--management-list-secondary-font: .75rem;", css, StringComparison.Ordinal);
        Assert.Contains("--management-list-pagination-font: .8rem;", css, StringComparison.Ordinal);
        Assert.Contains(".page-content table {", css, StringComparison.Ordinal);
        Assert.Contains("font-size: var(--management-list-body-font) !important;", css, StringComparison.Ordinal);
        Assert.Contains(".page-content table thead th,", css, StringComparison.Ordinal);
        Assert.Contains("font-size: var(--management-list-header-font) !important;", css, StringComparison.Ordinal);
        Assert.Contains(".page-content table tbody td :where(*)", css, StringComparison.Ordinal);
        Assert.Contains("font-size: inherit !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PromptAuditEndpointAndEventListsUseTheSameTypographyScale()
    {
        var css = Read("Pages", "AdminPromptAudit.razor.css");

        Assert.Contains(".endpoint-head, .events-table th { font-size: .76rem; }", css);
        Assert.Contains(".endpoint-table article > div, .events-table td { font-size: .83rem; }", css);
        Assert.Contains(".endpoint-name b, .endpoint-table article > div > b { font-size: .88rem; }", css);
        Assert.Contains(".event-pagination { font-size: .8rem; }", css);
    }

    [Fact]
    public void PromptAuditEntireWorkspaceUsesTheReadableManagementHierarchy()
    {
        var css = Read("Pages", "AdminPromptAudit.razor.css");

        Assert.Contains(".prompt-heading p, .section-header-row p, .policy-section header > p { font-size: .83rem; }", css);
        Assert.Contains(".runtime-summary strong, .runtime-details h3, .guard-grid b, .latest-time { font-size: .83rem !important; }", css);
        Assert.Contains(".policy-layout legend, .radio-row label, .scanner-grid label { font-size: .8rem; }", css);
        Assert.Contains(".audit-groups > div label, .policy-layout aside label, .event-filters > label { font-size: .75rem; }", css);
        Assert.Contains(".endpoint-form label { font-size: .75rem; }", css);
        Assert.Contains(".detail-tabs button { font-size: .8rem; }", css);
        Assert.Contains(".delete-filter > p, .delete-filter legend, .custom-range label, .delete-basic label, .delete-filter summary, .delete-preview > b { font-size: .75rem; }", css);
    }

    [Fact]
    public void IsolatedStylesheetVersionChangesWithTheManagementListTypographyRelease()
    {
        var index = Read("wwwroot", "index.html");

        Assert.Contains("ParaGateway.Frontend.styles.css?v=20260821-settings-heading-a", index);
    }

    private static string RegexEscape(string value) => System.Text.RegularExpressions.Regex.Escape(value);

    private static string Read(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine([root, .. segments])).ReplaceLineEndings("\n");
    }
}
