using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace KestrelHelpdesk.E2E;

public sealed class ExportImportAndNavigationTests : E2ETestBase
{
    [Fact]
    public async Task Employee_CanExportCurrentTicketPage()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);
        await Page.GotoAsync("/tickets");

        // The export implementation creates the PDF as a Blob and starts the
        // browser download from JavaScript. Verify the application reports that
        // the export started instead of relying on Playwright's Download event,
        // which is not guaranteed for Blob-backed downloads.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Export page" }).ClickAsync();
        await Expect(Page.GetByText("PDF export started for", new() { Exact = false })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Employee_CanOpenExcelImportPage()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword, "/tickets/import-excel");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/tickets/import-excel/?$"));
        await Expect(Page.GetByText("Import", new() { Exact = false }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Employee_CanNavigateFromBoardToTicketDetailsAndBack()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);
        await CreateTicketAsync("E2E-Navigation");

        await Page.GotoAsync("/tickets");
        // The board search matches title/description, not the numeric ticket ID.
        await Page.GetByPlaceholder("Title or description").FillAsync("E2E-Navigation");
        await Page.GetByRole(AriaRole.Link, new() { Name = "View" }).First.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/tickets/\d+/?$"));
        await Page.GetByRole(AriaRole.Link, new() { Name = "Back to board" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/tickets/?$"));
    }
}