using Microsoft.AspNetCore.Components.Forms;

namespace KestrelHelpdesk.Web.Services;

public interface ITicketAttachmentService
{
    long MaxFileSize { get; }
    int MaxFilesPerTicket { get; }

    Task<IReadOnlyList<string>> ValidateFilesAsync(
        int ticketId,
        IReadOnlyList<IBrowserFile> files,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default);

    Task SaveTicketChangesAsync(
        KestrelHelpdesk.Core.Models.Ticket ticket,
        bool isCreateMode,
        IReadOnlyList<IBrowserFile> files,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default);
}
