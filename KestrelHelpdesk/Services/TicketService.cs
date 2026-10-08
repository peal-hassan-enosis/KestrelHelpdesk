namespace KestrelHelpdesk.Services;

using KestrelHelpdesk.Data;
using KestrelHelpdesk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.QuickGrid;

public class TicketService : ITicketService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public TicketService(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async ValueTask<GridItemsProviderResult<Ticket>> GetPagedTicketsAsync(GridItemsProviderRequest<Ticket> request, string? statusFilter)
    {
        using var context = await _factory.CreateDbContextAsync();

        var query = context.Tickets.AsNoTracking();

        // Server-side filtering
        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<TicketStatus>(statusFilter, true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync();

        // Server-side sorting
        if (request.SortByColumn != null)
        {
            var sortProperty = request.SortByColumn.Title;
            query = sortProperty switch
            {
                "Title" => request.SortByAscending ? query.OrderBy(t => t.Title) : query.OrderByDescending(t => t.Title),
                "Status" => request.SortByAscending ? query.OrderBy(t => t.Status) : query.OrderByDescending(t => t.Status),
                "Priority" => request.SortByAscending ? query.OrderBy(t => t.Priority) : query.OrderByDescending(t => t.Priority),
                _ => request.SortByAscending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt)
            };
        }
        else
        {
            query = query.OrderByDescending(t => t.Id);
        }

        // Server-side paging
        var items = await query
            .Skip(request.StartIndex)
            .Take(request.Count ?? 10)
            .ToListAsync();

        return GridItemsProviderResult.From(items, totalCount);
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
        ticket.CreatedAt = DateTime.UtcNow;
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

    public async Task AddCommentAsync(int ticketId, Comment comment)
    {
        using var context = await _factory.CreateDbContextAsync();
        comment.TicketId = ticketId;
        comment.CreatedAt = DateTime.UtcNow;
        context.Comments.Add(comment);
        await context.SaveChangesAsync();
    }
}