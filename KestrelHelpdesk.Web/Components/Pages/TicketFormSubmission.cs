using Microsoft.AspNetCore.Components.Forms;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Web.Components.Pages;

public sealed record TicketFormSubmission(
    Ticket Ticket,
    IReadOnlyList<IBrowserFile> NewFiles,
    IReadOnlyCollection<int> AttachmentIdsToDelete);
