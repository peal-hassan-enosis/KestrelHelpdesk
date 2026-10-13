using System.Security.Claims;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketAttachmentDownloadService : ITicketAttachmentDownloadService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IFileStorageService _storage;

    public TicketAttachmentDownloadService(
        ITicketRepository ticketRepository,
        IFileStorageService storage)
    {
        _ticketRepository = ticketRepository;
        _storage = storage;
    }

    public async Task<(Stream Content, string ContentType, string FileName)> OpenDownloadAsync(
        int ticketId,
        int attachmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException();

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException();

        var ticket = await _ticketRepository.GetTicketByIdAsync(ticketId);
        if (ticket is null)
            throw new KeyNotFoundException($"Ticket {ticketId} was not found.");

        var currentUser = new CurrentUserInfo(
            userId,
            user.FindFirstValue(ClaimTypes.Email) ?? user.Identity.Name ?? string.Empty,
            user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        if (!TicketAccessPolicy.CanView(currentUser, ticket))
            throw new UnauthorizedAccessException();

        var attachment = (ticket.Attachments ?? []).FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
            throw new FileNotFoundException();

        var stream = await _storage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        return (stream, attachment.ContentType, attachment.OriginalFileName);
    }
}
