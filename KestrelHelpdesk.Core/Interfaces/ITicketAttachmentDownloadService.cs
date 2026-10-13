using System.Security.Claims;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketAttachmentDownloadService
{
    Task<(Stream Content, string ContentType, string FileName)> OpenDownloadAsync(
        int ticketId,
        int attachmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
