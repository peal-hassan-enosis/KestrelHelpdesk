using Microsoft.AspNetCore.Components.Forms;
using KestrelHelpdesk.Core.Models;

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

    Task ApplyChangesAsync(
        int ticketId,
        IReadOnlyList<IBrowserFile> files,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default);

    Task<(Stream Content, string ContentType, string FileName)> OpenDownloadAsync(
        int ticketId,
        int attachmentId,
        CancellationToken cancellationToken = default);
}
