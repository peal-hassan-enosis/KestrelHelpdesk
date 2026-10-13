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

    Task<Ticket?> GetTicketByIdAsync(int id);
    Task CreateTicketAsync(Ticket ticket);
    Task UpdateTicketAsync(Ticket ticket);
    Task AssignTicketAsync(int ticketId, string? agentUserId);
    Task AddCommentAsync(int ticketId, Comment comment);
    Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(TicketScope scope);
    Task<IReadOnlyList<AssignableUser>> GetAgentsAsync();
    Task<IReadOnlyList<string>> ValidateImportRowsAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default);
    Task<int> ImportTicketsAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default);
}
