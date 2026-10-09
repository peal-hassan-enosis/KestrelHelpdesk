using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using Microsoft.AspNetCore.Components.QuickGrid;

namespace KestrelHelpdesk.Core.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;

    public TicketService(ITicketRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<GridItemsProviderResult<Ticket>> GetPagedTicketsAsync(GridItemsProviderRequest<Ticket> request, string? statusFilter)
    {
        // Extract parameters to keep QuickGrid logic isolated from the database
        var (items, totalCount) = await _repository.GetPagedTicketsAsync(
            request.StartIndex,
            request.Count,
            statusFilter,
            request.SortByColumn?.Title,
            request.SortByAscending
        );

        return GridItemsProviderResult.From(items, totalCount);
    }

    public async Task<Ticket?> GetTicketByIdAsync(int id)
    {
        return await _repository.GetTicketByIdAsync(id);
    }

    public async Task CreateTicketAsync(Ticket ticket)
    {
        // Business logic stays in the service
        ticket.CreatedAt = DateTime.UtcNow;

        await _repository.CreateTicketAsync(ticket);
    }

    public async Task UpdateTicketAsync(Ticket ticket)
    {
        await _repository.UpdateTicketAsync(ticket);
    }

    public async Task AddCommentAsync(int ticketId, Comment comment)
    {
        // Business logic stays in the service
        comment.TicketId = ticketId;
        comment.CreatedAt = DateTime.UtcNow;

        await _repository.AddCommentAsync(comment);
    }

    public async Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync()
    {
        return await _repository.GetTicketStatusCountsAsync();
    }
}