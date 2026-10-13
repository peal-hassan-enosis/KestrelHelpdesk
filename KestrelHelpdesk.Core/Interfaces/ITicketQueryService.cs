using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketQueryService
{
    Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetTicketGridDataAsync(
        int page,
        int pageSize,
        string? statusFilter,
        string? searchTerm,
        TicketScope scope,
        bool notAssignedOnly = false,
        string? sortBy = null,
        bool sortDescending = false);

    Task<IReadOnlyList<Ticket>> GetTicketsForExportAsync(
        string? statusFilter,
        string? searchTerm,
        TicketScope scope,
        bool notAssignedOnly = false,
        string? sortBy = null,
        bool sortDescending = false);

    Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(TicketScope scope);
}
