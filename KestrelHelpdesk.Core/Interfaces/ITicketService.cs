namespace KestrelHelpdesk.Core.Interfaces;

using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Models;
using Microsoft.AspNetCore.Components.QuickGrid;

public interface ITicketService
{
    ValueTask<GridItemsProviderResult<Ticket>> GetPagedTicketsAsync(
        GridItemsProviderRequest<Ticket> request,
        string? statusFilter,
        string? searchTerm,
        TicketScope scope,
        bool notAssignedOnly = false);

    Task<Ticket?> GetTicketByIdAsync(int id);
    Task CreateTicketAsync(Ticket ticket);
    Task UpdateTicketAsync(Ticket ticket);
    Task AssignTicketAsync(int ticketId, string? agentUserId);
    Task AddCommentAsync(int ticketId, Comment comment);
    Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(TicketScope scope);
    Task<IReadOnlyList<AssignableUser>> GetAgentsAsync();
}
