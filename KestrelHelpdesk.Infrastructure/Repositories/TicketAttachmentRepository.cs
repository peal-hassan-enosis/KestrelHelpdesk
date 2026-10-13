using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KestrelHelpdesk.Infrastructure.Repositories;

public sealed class TicketAttachmentRepository : ITicketAttachmentRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public TicketAttachmentRepository(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public async Task<IReadOnlyList<TicketAttachment>> GetByTicketIdAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        using var context = await _factory.CreateDbContextAsync(cancellationToken);
        return await context.TicketAttachments
            .AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyList<TicketAttachment> attachments, CancellationToken cancellationToken = default)
    {
        if (attachments.Count == 0) return;
        using var context = await _factory.CreateDbContextAsync(cancellationToken);
        context.TicketAttachments.AddRange(attachments);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRangeAsync(IReadOnlyList<TicketAttachment> attachments, CancellationToken cancellationToken = default)
    {
        if (attachments.Count == 0) return;
        using var context = await _factory.CreateDbContextAsync(cancellationToken);
        context.TicketAttachments.RemoveRange(attachments.Select(a => new TicketAttachment { Id = a.Id }));
        await context.SaveChangesAsync(cancellationToken);
    }
}
