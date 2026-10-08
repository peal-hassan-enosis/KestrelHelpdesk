namespace KestrelHelpdesk.Services;

using KestrelHelpdesk.Models;
using Microsoft.AspNetCore.Components.QuickGrid;

public interface ITicketService
{
    // Add <Ticket> to the request parameter
    ValueTask<GridItemsProviderResult<Ticket>> GetPagedTicketsAsync(GridItemsProviderRequest<Ticket> request, string? statusFilter);

    Task<Ticket?> GetTicketByIdAsync(int id);
    Task CreateTicketAsync(Ticket ticket);
    Task UpdateTicketAsync(Ticket ticket);
    Task AddCommentAsync(int ticketId, Comment comment);
}