using Bunit;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Web.Components.Helper;
using KestrelHelpdesk.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Security.Claims;
using Xunit;

namespace KestrelHelpdesk.Tests.Components;

public class TicketFormTests : BunitContext
{
    public TicketFormTests()
    {
        Services.AddSingleton<ITicketAttachmentService, TestTicketAttachmentService>();
    }

    [Fact]
    public void InvalidForm_ShowsDataAnnotationMessages_AndDoesNotSubmit()
    {
        ConfigureJsInterop();
        var submitted = false;
        var model = new Ticket
        {
            Title = string.Empty,
            Description = string.Empty,
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
        Assert.False(submitted);
    }

    [Fact]
    public void CreateForm_RequiresCategorySelection()
    {
        ConfigureJsInterop();
        var submitted = false;
        var model = new Ticket
        {
            Title = "Cannot sign in",
            Description = "I cannot sign in to the application.",
            Priority = TicketPriority.High,
            Status = TicketStatus.Open
        };

        var cut = Render<TicketForm>(parameters => parameters
            .Add(p => p.TicketModel, model)
            .Add(p => p.IsCreateMode, true)
            .Add(p => p.OnValidSubmit, EventCallback.Factory.Create<TicketFormSubmission>(
                new object(),
                _ => submitted = true)));

        cut.Find("form").Submit();

        Assert.Contains("Category is required.", cut.Find(".ticket-form").TextContent);
        Assert.False(submitted);
    }

    [Fact]
    public void CreateForm_UsesSelectedCategoryWhenSubmitting()
    {
        ConfigureJsInterop();
        TicketFormSubmission? submitted = null;
        var model = new Ticket
        {
            Title = "Cannot sign in",
            Description = "I cannot sign in to the application.",
            Priority = TicketPriority.High,
            Status = TicketStatus.Open
        };

        var cut = Render<TicketForm>(parameters => parameters
            .Add(p => p.TicketModel, model)
            .Add(p => p.IsCreateMode, true)
            .Add(p => p.OnValidSubmit, EventCallback.Factory.Create<TicketFormSubmission>(
                new object(),
                value => submitted = value)));

        cut.Find("#ticket-category").Change(nameof(TicketCategory.Software));
        cut.Find("form").Submit();

        Assert.NotNull(submitted);
        Assert.Equal(TicketCategory.Software, submitted!.Ticket.Category);
    }

    private void ConfigureJsInterop()
    {
        var module = JSInterop.SetupModule("./js/ticket-form.js");
        module.SetupVoid("focusElement", _ => true);
    }

    private sealed class TestTicketAttachmentService : ITicketAttachmentService
    {
        public long MaxFileSize => TicketAttachmentPolicy.MaxFileSize;
        public int MaxFilesPerTicket => TicketAttachmentPolicy.MaxFilesPerTicket;

        public Task<IReadOnlyList<string>> ValidateFilesAsync(
            int ticketId,
            IReadOnlyList<IBrowserFile> files,
            IReadOnlyCollection<int> attachmentIdsToDelete,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task SaveTicketChangesAsync(
            Ticket ticket,
            bool isCreateMode,
            IReadOnlyList<IBrowserFile> files,
            IReadOnlyCollection<int> attachmentIdsToDelete,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(Stream Content, string ContentType, string FileName)> OpenDownloadAsync(
            int ticketId,
            int attachmentId,
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
