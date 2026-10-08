namespace KestrelHelpdesk.Services;

using KestrelHelpdesk.Models;

public interface ITicketService
{
    Task<List<Ticket>> GetTicketsAsync(string? statusFilter = null);
    Task<Ticket?> GetTicketByIdAsync(int id);
    Task CreateTicketAsync(Ticket ticket);
    Task UpdateTicketAsync(Ticket ticket);
    Task AddCommentAsync(int ticketId, Comment comment);
}