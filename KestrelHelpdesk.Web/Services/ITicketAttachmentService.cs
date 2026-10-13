using KestrelHelpdesk.Core.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace KestrelHelpdesk.Web.Services;

public interface ITicketAttachmentService
{
    const int MaxAttachmentsPerTicket = 3;
    const long MaxFileSize = 10 * 1024 * 1024;

    IReadOnlyList<string> ValidateFiles(IReadOnlyList<IBrowserFile> files, int existingAttachmentCount = 0);

    Task<IReadOnlyList<TicketAttachment>> AddAttachmentsAsync(
        int ticketId,
        IReadOnlyList<IBrowserFile> files,
        int existingAttachmentCount = 0,
        CancellationToken cancellationToken = default);

    Task DeleteAttachmentAsync(int ticketId, int attachmentId, CancellationToken cancellationToken = default);

    Task<(TicketAttachment Attachment, Stream Content)> OpenAttachmentAsync(
        int ticketId,
        int attachmentId,
        CancellationToken cancellationToken = default);
}
