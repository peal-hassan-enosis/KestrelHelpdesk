using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketRepository
{
    Task<(List<Ticket> Items, int TotalCount)> GetPagedTicketsAsync(
        int startIndex, int? count, string? statusFilter, string? searchTerm,
        string? sortByColumn, bool sortByAscending, TicketScope scope, string userId, bool notAssignedOnly);

    Task<IReadOnlyList<Ticket>> GetTicketsForExportAsync(
        string? statusFilter, string? searchTerm,
        string? sortByColumn, bool sortByAscending, TicketScope scope, string userId, bool notAssignedOnly);

    Task<Ticket?> GetTicketByIdAsync(int id);
    Task CreateTicketAsync(Ticket ticket);
    Task CreateTicketsAsync(IReadOnlyList<Ticket> tickets, CancellationToken cancellationToken = default);
    Task UpdateTicketAsync(Ticket ticket);
    Task AssignTicketAsync(int ticketId, string? agentUserId);
    Task AddCommentAsync(Comment comment);
    Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(TicketScope scope, string userId);
}
