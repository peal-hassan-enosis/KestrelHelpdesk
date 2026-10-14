using System.ComponentModel.DataAnnotations;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KestrelHelpdesk.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public TicketRepository(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public async Task<(List<Ticket> Items, int TotalCount)> GetPagedTicketsAsync(
        int startIndex, int? count, string? statusFilter, string? searchTerm,
        string? sortByColumn, bool sortByAscending, TicketScope scope, string userId, bool notAssignedOnly)
    {
        using var context = await _factory.CreateDbContextAsync();
        var query = BuildTicketQuery(context, statusFilter, searchTerm, sortByColumn, sortByAscending, scope, userId, notAssignedOnly);
        var totalCount = await query.CountAsync();
        var items = await query.Skip(startIndex).Take(count ?? 50).ToListAsync();
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Ticket>> GetTicketsForExportAsync(
        string? statusFilter, string? searchTerm,
        string? sortByColumn, bool sortByAscending, TicketScope scope, string userId, bool notAssignedOnly)
    {
        using var context = await _factory.CreateDbContextAsync();
        var query = BuildTicketQuery(context, statusFilter, searchTerm, sortByColumn, sortByAscending, scope, userId, notAssignedOnly);
        return await query.ToListAsync();
    }

    private static IQueryable<Ticket> BuildTicketQuery(
        ApplicationDbContext context,
        string? statusFilter,
        string? searchTerm,
        string? sortByColumn,
        bool sortByAscending,
        TicketScope scope,
        string userId,
        bool notAssignedOnly)
    {
        var query = context.Tickets.AsNoTracking();

        query = scope switch
        {
            TicketScope.EmployeeOwn or TicketScope.AgentCreated => query.Where(t => t.CreatedBy == userId),
            TicketScope.AgentAssigned => query.Where(t => t.AssignedToUserId == userId),
            TicketScope.ManagerAll => query,
            _ => query.Where(t => false)
        };

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<TicketStatus>(statusFilter, true, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(t => EF.Functions.Like(t.Title, pattern) || EF.Functions.Like(t.Description, pattern));
        }

        if (notAssignedOnly)
            query = query.Where(t => t.Status == TicketStatus.Open && t.AssignedToUserId == null);

        return sortByColumn switch
        {
            "Title" => sortByAscending ? query.OrderBy(t => t.Title) : query.OrderByDescending(t => t.Title),
            "Status" => sortByAscending ? query.OrderBy(t => t.Status) : query.OrderByDescending(t => t.Status),
            "Priority" => sortByAscending ? query.OrderBy(t => t.Priority) : query.OrderByDescending(t => t.Priority),
            "Id" => sortByAscending ? query.OrderBy(t => t.Id) : query.OrderByDescending(t => t.Id),
            _ => sortByAscending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt)
        };
    }

    public async Task<Ticket?> GetTicketByIdAsync(int id)
    {
        using var context = await _factory.CreateDbContextAsync();
        return await context.Tickets
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task CreateTicketAsync(Ticket ticket)
    {
        using var context = await _factory.CreateDbContextAsync();
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
    }

    public async Task CreateTicketsAsync(IReadOnlyList<Ticket> tickets, CancellationToken cancellationToken = default)
    {
        using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateTicketAsync(Ticket ticket)
    {
        using var context = await _factory.CreateDbContextAsync();
        var existing = await context.Tickets.FindAsync(ticket.Id);
        if (existing is null) return;
        existing.Title = ticket.Title;
        existing.Description = ticket.Description;
        existing.Priority = ticket.Priority;
        existing.Category = ticket.Category;
        existing.Status = ticket.Status;
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<TicketAttachment>> SaveTicketWithAttachmentsAsync(
        Ticket ticket,
        bool isCreate,
        IReadOnlyList<TicketAttachment> attachmentsToAdd,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var removedAttachments = new List<TicketAttachment>();

        try
        {
            if (isCreate)
            {
                context.Tickets.Add(ticket);
                // Generate the ticket ID in this transaction before assigning it to attachment rows.
                await context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var existing = await context.Tickets
                    .FirstOrDefaultAsync(item => item.Id == ticket.Id, cancellationToken)
                    ?? throw new KeyNotFoundException($"Ticket {ticket.Id} was not found.");

                existing.Title = ticket.Title;
                existing.Description = ticket.Description;
                existing.Priority = ticket.Priority;
                existing.Category = ticket.Category;
                existing.Status = ticket.Status;
            }

            var deleteIds = attachmentIdsToDelete.Distinct().ToArray();
            if (isCreate && deleteIds.Length > 0)
                throw new ValidationException("Attachments cannot be deleted while creating a ticket.");

            if (deleteIds.Length > 0)
            {
                removedAttachments = await context.TicketAttachments
                    .Where(attachment => attachment.TicketId == ticket.Id && deleteIds.Contains(attachment.Id))
                    .ToListAsync(cancellationToken);

                if (removedAttachments.Count != deleteIds.Length)
                    throw new ValidationException("One or more attachments do not belong to this ticket.");

                context.TicketAttachments.RemoveRange(removedAttachments);
            }

            foreach (var attachment in attachmentsToAdd)
            {
                attachment.TicketId = ticket.Id;
            }

            if (attachmentsToAdd.Count > 0)
                context.TicketAttachments.AddRange(attachmentsToAdd);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return removedAttachments;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task AssignTicketAsync(int ticketId, string? agentUserId)
    {
        using var context = await _factory.CreateDbContextAsync();
        var ticket = await context.Tickets.FindAsync(ticketId);
        if (ticket is null) return;
        ticket.AssignedToUserId = agentUserId;
        await context.SaveChangesAsync();
    }

    public async Task AddCommentAsync(Comment comment)
    {
        using var context = await _factory.CreateDbContextAsync();
        context.Comments.Add(comment);
        await context.SaveChangesAsync();
    }

    public async Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(TicketScope scope, string userId)
    {
        using var context = await _factory.CreateDbContextAsync();

        // Reuse the grid's access-filtered base query so the dashboard and ticket board
        // always calculate visibility using the same rules.
        var query = BuildTicketQuery(
            context,
            statusFilter: null,
            searchTerm: null,
            sortByColumn: null,
            sortByAscending: true,
            scope: scope,
            userId: userId,
            notAssignedOnly: false);

        return await query.GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }
}
