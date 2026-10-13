using Bunit;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Web.Components.Pages;
using KestrelHelpdesk.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;

namespace KestrelHelpdesk.Tests.Components;

public class TicketFormTests : BunitContext
{
    public TicketFormTests()
    {
        Services.AddSingleton<ITicketAttachmentService, TestTicketAttachmentService>();
    }

    [Fact]
    public void InvalidForm_ShowsValidationMessages_AndDoesNotSubmit()
    {
        ConfigureJsInterop();
        var submitted = false;
        var model = new Ticket
        {
            Title = string.Empty,
            Description = string.Empty,
            Category = null,
            Priority = TicketPriority.Low,
            Status = TicketStatus.Open
        };

        var cut = Render<TicketForm>(parameters => parameters
            .Add(p => p.TicketModel, model)
            .Add(p => p.IsCreateMode, true)
            .Add(p => p.OnValidSubmit, EventCallback.Factory.Create<TicketFormSubmission>(
                new object(),
                _ => submitted = true)));

        cut.Find("form").Submit();

        var validationSummary = cut.Find("ul.alert-danger").TextContent;
        Assert.Contains("Title is required.", validationSummary);
        Assert.Contains("Description is required.", validationSummary);
        Assert.Contains("Category is required.", validationSummary);
        Assert.False(submitted);
    }

    private void ConfigureJsInterop()
    {
        var module = JSInterop.SetupModule("./js/ticket-form.js");
        module.SetupVoid("focusElement", _ => true);
    }

    private sealed class TestTicketAttachmentService : ITicketAttachmentService
    {
        public long MaxFileSize => 10 * 1024 * 1024;
        public int MaxFilesPerTicket => 3;

        public Task<IReadOnlyList<string>> ValidateFilesAsync(
            int ticketId,
            IReadOnlyList<IBrowserFile> files,
            IReadOnlyCollection<int> attachmentIdsToDelete,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task ApplyChangesAsync(
            int ticketId,
            IReadOnlyList<IBrowserFile> files,
            IReadOnlyCollection<int> attachmentIdsToDelete,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(Stream Content, string ContentType, string FileName)> OpenDownloadAsync(
            int ticketId,
            int attachmentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
