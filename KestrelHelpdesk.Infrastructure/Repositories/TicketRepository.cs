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
        int startIndex, int? count, string? statusFilter, string? sortByColumn,
        bool sortByAscending, TicketScope scope, string userId, bool notAssignedOnly)
    {
        using var context = await _factory.CreateDbContextAsync();
        var query = context.Tickets.AsNoTracking();

        query = scope switch
        {
            TicketScope.EmployeeOwn or TicketScope.AgentCreated => query.Where(t => t.CreatedBy == userId),
            TicketScope.AgentAssigned => query.Where(t => t.AssignedToUserId == userId),
            TicketScope.ManagerAll => query,
            _ => query.Where(t => false)
        };

        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<TicketStatus>(statusFilter, true, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        if (notAssignedOnly)
            query = query.Where(t => t.Status == TicketStatus.Open && t.AssignedToUserId == null);

        var totalCount = await query.CountAsync();

        query = sortByColumn switch
        {
            "Title" => sortByAscending ? query.OrderBy(t => t.Title) : query.OrderByDescending(t => t.Title),
            "Status" => sortByAscending ? query.OrderBy(t => t.Status) : query.OrderByDescending(t => t.Status),
            "Priority" => sortByAscending ? query.OrderBy(t => t.Priority) : query.OrderByDescending(t => t.Priority),
            _ => sortByAscending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt)
        };

        var items = await query.Skip(startIndex).Take(count ?? 10).ToListAsync();
        return (items, totalCount);
    }

    public async Task<Ticket?> GetTicketByIdAsync(int id)
    {
        using var context = await _factory.CreateDbContextAsync();
        return await context.Tickets.Include(t => t.Comments).FirstOrDefaultAsync(t => t.Id == id);
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
        if (existing is null) return;
        existing.Title = ticket.Title;
        existing.Description = ticket.Description;
        existing.Priority = ticket.Priority;
        existing.Category = ticket.Category;
        existing.Status = ticket.Status;
        await context.SaveChangesAsync();
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
        var query = context.Tickets.AsNoTracking();
        query = scope switch
        {
            TicketScope.EmployeeOwn or TicketScope.AgentCreated => query.Where(t => t.CreatedBy == userId),
            TicketScope.AgentAssigned => query.Where(t => t.AssignedToUserId == userId),
            TicketScope.ManagerAll => query,
            _ => query.Where(t => false)
        };
        return await query.GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }
}
