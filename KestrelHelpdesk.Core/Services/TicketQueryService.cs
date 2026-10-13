using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Services;

public sealed class TicketQueryService : ITicketQueryService
{
    private readonly ITicketRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public TicketQueryService(ITicketRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetTicketGridDataAsync(
        int page,
        int pageSize,
        string? statusFilter,
        string? searchTerm,
        TicketScope scope,
        bool notAssignedOnly = false,
        string? sortBy = null,
        bool sortDescending = false)
    {
        var user = await RequireUserAsync();
        var effectiveScope = GetEffectiveScope(user, scope);
        var startIndex = Math.Max(0, page) * Math.Max(1, pageSize);

        var (items, totalCount) = await _repository.GetPagedTicketsAsync(
            startIndex,
            pageSize,
            statusFilter,
            searchTerm,
            NormalizeSortColumn(sortBy),
            !sortDescending,
            effectiveScope,
            user.UserId,
            notAssignedOnly);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Ticket>> GetTicketsForExportAsync(
        string? statusFilter,
        string? searchTerm,
        TicketScope scope,
        bool notAssignedOnly = false,
        string? sortBy = null,
        bool sortDescending = false)
    {
        var user = await RequireUserAsync();
        var effectiveScope = GetEffectiveScope(user, scope);

        return await _repository.GetTicketsForExportAsync(
            statusFilter,
            searchTerm,
            NormalizeSortColumn(sortBy),
            !sortDescending,
            effectiveScope,
            user.UserId,
            notAssignedOnly);
    }

    public async Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(TicketScope scope)
    {
        var user = await RequireUserAsync();
        return await _repository.GetTicketStatusCountsAsync(GetEffectiveScope(user, scope), user.UserId);
    }

    private async Task<CurrentUserInfo> RequireUserAsync() =>
        await _currentUser.GetCurrentUserAsync()
        ?? throw new UnauthorizedAccessException("You must be signed in.");

    private static TicketScope GetEffectiveScope(CurrentUserInfo user, TicketScope requested)
    {
        if (user.IsInRole(AppRoles.Manager))
            return TicketScope.ManagerAll;

        if (user.IsInRole(AppRoles.Employee))
            return TicketScope.EmployeeOwn;

        return requested == TicketScope.AgentCreated
            ? TicketScope.AgentCreated
            : TicketScope.AgentAssigned;
    }

    private static string? NormalizeSortColumn(string? sortBy) => sortBy switch
    {
        "Id" => "Id",
        "Title" => "Title",
        "Status" => "Status",
        "Priority" => "Priority",
        "CreatedAt" => "CreatedAt",
        _ => null
    };
}
