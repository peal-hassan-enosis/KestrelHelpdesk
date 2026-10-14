using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketService
{
    Task<Ticket?> GetTicketByIdAsync(int id);
    Task CreateTicketAsync(Ticket ticket);
    Task UpdateTicketAsync(Ticket ticket);
    Task<IReadOnlyList<TicketAttachment>> SaveTicketWithAttachmentsAsync(
        Ticket ticket,
        bool isCreate,
        IReadOnlyList<TicketAttachment> attachmentsToAdd,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default);
}
