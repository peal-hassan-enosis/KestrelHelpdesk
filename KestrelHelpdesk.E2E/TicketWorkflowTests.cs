using Microsoft.Playwright;
using Xunit;

namespace KestrelHelpdesk.E2E;

public sealed class TicketWorkflowTests : E2ETestBase
{
    [Fact]
    public async Task Employee_CreatesTicket_AndInitialStatusIsOpen()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);
        await CreateTicketAsync("E2E-Create");

        await Expect(Page.GetByText("Open", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Employee_CanEditOwnTicket_AndChangeStatus()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);
        var ticketId = await CreateTicketAsync("E2E-Edit");

        await Page.GotoAsync($"/tickets/{ticketId}/edit");
        await Page.GetByLabel("Ticket title").FillAsync($"E2E-Edit-Updated-{Guid.NewGuid():N}");
        await Page.GetByLabel("Status").SelectOptionAsync("InProgress");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save changes" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/tickets/{ticketId}/?$"));
        await Expect(Page.GetByText("In Progress", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Employee_CanPostCommentOnOwnTicket()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);
        await CreateTicketAsync("E2E-Comment");

        var comment = $"Playwright comment {Guid.NewGuid():N}";
        await Page.GetByPlaceholder("Write a response...").FillAsync(comment);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Post Comment" }).ClickAsync();

        await Expect(Page.GetByText(comment, new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Employee_CanUploadAndDownloadAttachment()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);
        await Page.GotoAsync("/tickets/new");
        var title = $"E2E-Attachment-{Guid.NewGuid():N}";

        await Page.GetByLabel("Ticket title").FillAsync(title);
        await Page.GetByLabel("Category").SelectOptionAsync("Access");
        await Page.GetByLabel("Priority").SelectOptionAsync("Medium");
        await Page.GetByLabel("Description").FillAsync("Attachment end-to-end test.");
        await Page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        {
            Name = "e2e-note.txt",
            MimeType = "text/plain",
            Buffer = System.Text.Encoding.UTF8.GetBytes("Playwright attachment test.")
        });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Create ticket" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = title })).ToBeVisibleAsync();
        await Expect(Page.GetByText("e2e-note.txt", new() { Exact = true })).ToBeVisibleAsync();

        var download = await Page.RunAndWaitForDownloadAsync(async () =>
        {
            await Page.GetByRole(AriaRole.Link, new() { Name = "Download" }).ClickAsync();
        });

        Assert.Equal("e2e-note.txt", download.SuggestedFilename);
    }
}