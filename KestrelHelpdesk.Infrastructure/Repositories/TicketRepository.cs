using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Infrastructure.Data;
using KestrelHelpdesk.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace KestrelHelpdesk.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public TicketRepository(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<(List<Ticket> Items, int TotalCount)> GetPagedTicketsAsync(int startIndex, int? count, string? statusFilter, string? sortByColumn, bool sortByAscending)
    {
        using var context = await _factory.CreateDbContextAsync();
        var query = context.Tickets.AsNoTracking();

        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<TicketStatus>(statusFilter, true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync();

        if (sortByColumn != null)
        {
            query = sortByColumn switch
            {
                "Title" => sortByAscending ? query.OrderBy(t => t.Title) : query.OrderByDescending(t => t.Title),
                "Status" => sortByAscending ? query.OrderBy(t => t.Status) : query.OrderByDescending(t => t.Status),
                "Priority" => sortByAscending ? query.OrderBy(t => t.Priority) : query.OrderByDescending(t => t.Priority),
                _ => sortByAscending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt)
            };
        }
        else
        {
            query = query.OrderByDescending(t => t.Id);
        }

        var items = await query
            .Skip(startIndex)
            .Take(count ?? 10)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Ticket?> GetTicketByIdAsync(int id)
    {
        using var context = await _factory.CreateDbContextAsync();
        return await context.Tickets
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task CreateTicketAsync(Ticket ticket)
    {
        using var context = await _factory.CreateDbContextAsync();
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
    }

    public async Task UpdateTicketAsync(Ticket ticket)
    {
        using var context = await _factory.CreateDbContextAsync();
        var existing = await context.Tickets.FindAsync(ticket.Id);
        if (existing != null)
        {
            existing.Title = ticket.Title;
            existing.Description = ticket.Description;
            existing.Priority = ticket.Priority;
            existing.Status = ticket.Status;
            await context.SaveChangesAsync();
        }
    }

    public async Task AddCommentAsync(Comment comment)
    {
        using var context = await _factory.CreateDbContextAsync();
        context.Comments.Add(comment);
        await context.SaveChangesAsync();
    }
}