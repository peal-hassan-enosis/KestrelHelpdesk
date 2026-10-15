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

        var download = await Page.RunAndWaitForDownloadAsync(async () =>
        {
            await Page.GetByRole(AriaRole.Button, new() { Name = "Export page" }).ClickAsync();
        });

        Assert.False(string.IsNullOrWhiteSpace(download.SuggestedFilename));
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