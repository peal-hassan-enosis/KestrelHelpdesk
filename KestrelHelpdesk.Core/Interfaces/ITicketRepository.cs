using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketRepository
{
    Task<(List<Ticket> Items, int TotalCount)> GetPagedTicketsAsync(int startIndex, int? count, string? statusFilter, string? sortByColumn, bool sortByAscending);
    Task<Ticket?> GetTicketByIdAsync(int id);
    Task CreateTicketAsync(Ticket ticket);
    Task UpdateTicketAsync(Ticket ticket);
    Task AddCommentAsync(Comment comment);
}