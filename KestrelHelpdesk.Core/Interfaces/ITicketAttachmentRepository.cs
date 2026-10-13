namespace KestrelHelpdesk.Core.Interfaces;

using KestrelHelpdesk.Core.Models;

public interface ITicketAttachmentRepository
{
    Task AddRangeAsync(IReadOnlyList<TicketAttachment> attachments, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IReadOnlyList<TicketAttachment> attachments, CancellationToken cancellationToken = default);
}
