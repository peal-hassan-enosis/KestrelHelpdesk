using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Events;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using Microsoft.AspNetCore.Components.QuickGrid;

namespace KestrelHelpdesk.Core.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserDirectory _userDirectory;
    private readonly ITicketEventService _ticketEvents;

    public TicketService(
        ITicketRepository repository,
        ICurrentUserContext currentUser,
        IUserDirectory userDirectory,
        ITicketEventService ticketEvents)
    {
        _repository = repository;
        _currentUser = currentUser;
        _userDirectory = userDirectory;
        _ticketEvents = ticketEvents;
    }

    public async ValueTask<GridItemsProviderResult<Ticket>> GetPagedTicketsAsync(
        GridItemsProviderRequest<Ticket> request,
        string? statusFilter,
        string? searchTerm,
        TicketScope scope,
        bool notAssignedOnly = false)
    {
        var user = await RequireUserAsync();
        var effectiveScope = GetEffectiveScope(user, scope);
        var (items, totalCount) = await _repository.GetPagedTicketsAsync(
            request.StartIndex,
            request.Count,
            statusFilter,
            searchTerm,
            request.SortByColumn?.Title,
            request.SortByAscending,
            effectiveScope,
            user.UserId,
            notAssignedOnly);

        return GridItemsProviderResult.From(items, totalCount);
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

    public async Task<Ticket?> GetTicketByIdAsync(int id)
    {
        var user = await RequireUserAsync();
        var ticket = await _repository.GetTicketByIdAsync(id);
        return ticket is not null && CanView(user, ticket) ? ticket : null;
    }

    public async Task CreateTicketAsync(Ticket ticket)
    {
        var user = await RequireUserAsync();
        ticket.CreatedBy = user.UserId;
        ticket.Status = TicketStatus.Open;
        ticket.AssignedToUserId = null;
        ticket.CreatedAt = DateTime.UtcNow;
        await _repository.CreateTicketAsync(ticket);
    }

    public async Task UpdateTicketAsync(Ticket ticket)
    {
        var user = await RequireUserAsync();
        var existing = await _repository.GetTicketByIdAsync(ticket.Id) ?? throw new KeyNotFoundException($"Ticket {ticket.Id} was not found.");
        if (!CanEdit(user, existing)) throw new UnauthorizedAccessException("You are not allowed to edit this ticket.");

        var statusChanged = existing.Status != ticket.Status;
        if (user.IsInRole(AppRoles.Manager)) ticket.Status = existing.Status;

        ticket.CreatedBy = existing.CreatedBy;
        ticket.AssignedToUserId = existing.AssignedToUserId;
        ticket.CreatedAt = existing.CreatedAt;
        await _repository.UpdateTicketAsync(ticket);

        if (user.IsInRole(AppRoles.Agent) && statusChanged)
        {
            await _ticketEvents.PublishTicketChangedAsync(
                new TicketChangedEventArgs(ticket.Id, TicketChangeType.StatusChanged));
        }
    }

    public async Task AssignTicketAsync(int ticketId, string? agentUserId)
    {
        var user = await RequireUserAsync();
        var ticket = await _repository.GetTicketByIdAsync(ticketId) ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");
        agentUserId = string.IsNullOrWhiteSpace(agentUserId) ? null : agentUserId;

        if (!user.IsInRole(AppRoles.Manager) && !user.IsInRole(AppRoles.Agent))
            throw new UnauthorizedAccessException("Only agents and managers can change assignment.");
        if (user.IsInRole(AppRoles.Agent) && ticket.CreatedBy != user.UserId)
            throw new UnauthorizedAccessException("Agents may only assign tickets they created.");
        if (user.IsInRole(AppRoles.Agent) && agentUserId is not null && agentUserId != user.UserId)
            throw new UnauthorizedAccessException("Agents can only assign a ticket to themselves.");

        if (agentUserId is not null)
        {
            var agents = await _userDirectory.GetAgentsAsync();
            if (agents.All(a => a.Id != agentUserId))
                throw new UnauthorizedAccessException("The selected user is not an agent.");
        }

        await _repository.AssignTicketAsync(ticketId, agentUserId);
    }

    public async Task AddCommentAsync(int ticketId, Comment comment)
    {
        var user = await RequireUserAsync();
        var ticket = await _repository.GetTicketByIdAsync(ticketId);
        if (ticket is null || !CanView(user, ticket)) throw new UnauthorizedAccessException("You are not allowed to comment on this ticket.");
        comment.TicketId = ticketId;
        comment.Author = user.Email;
        comment.CreatedAt = DateTime.UtcNow;
        await _repository.AddCommentAsync(comment);
    }

    public async Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(TicketScope scope)
    {
        var user = await RequireUserAsync();
        return await _repository.GetTicketStatusCountsAsync(GetEffectiveScope(user, scope), user.UserId);
    }

    public Task<IReadOnlyList<AssignableUser>> GetAgentsAsync() => _userDirectory.GetAgentsAsync();

    private async Task<CurrentUserInfo> RequireUserAsync() => await _currentUser.GetCurrentUserAsync() ?? throw new UnauthorizedAccessException("You must be signed in.");

    private static TicketScope GetEffectiveScope(CurrentUserInfo user, TicketScope requested)
    {
        if (user.IsInRole(AppRoles.Manager)) return TicketScope.ManagerAll;
        if (user.IsInRole(AppRoles.Employee)) return TicketScope.EmployeeOwn;
        return requested == TicketScope.AgentCreated ? TicketScope.AgentCreated : TicketScope.AgentAssigned;
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

    private static bool CanView(CurrentUserInfo user, Ticket ticket) => user.IsInRole(AppRoles.Manager) || ticket.CreatedBy == user.UserId || (user.IsInRole(AppRoles.Agent) && ticket.AssignedToUserId == user.UserId);
    private static bool CanEdit(CurrentUserInfo user, Ticket ticket) => CanView(user, ticket);
}
