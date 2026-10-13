using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Events;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using Microsoft.AspNetCore.Components.QuickGrid;
using System.ComponentModel.DataAnnotations;

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
        var validationErrors = ValidateTicket(ticket);
        if (validationErrors.Count > 0)
            throw new ValidationException(string.Join(" ", validationErrors));

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

        var validationErrors = ValidateTicket(ticket);
        if (validationErrors.Count > 0)
            throw new ValidationException(string.Join(" ", validationErrors));

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

    public async Task<IReadOnlyList<string>> ValidateImportRowsAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync();
        var errors = new List<string>();
        var agents = await _userDirectory.GetAgentsAsync();
        var agentByEmail = agents.ToDictionary(a => a.Email, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ticket = new Ticket
            {
                Title = row.Title,
                Description = row.Description,
                Priority = row.Priority,
                Category = row.Category,
                Status = TicketStatus.Open,
                CreatedBy = user.UserId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var validationError in ValidateTicket(ticket))
                errors.Add($"Row {row.ExcelRowNumber}: {validationError}");
        }

        if (!user.IsInRole(AppRoles.Manager) && !ignoreAssignments && rows.Any(r => !string.IsNullOrWhiteSpace(r.AssignedToEmail)))
            errors.Add("The Excel file contains ticket assignments, but your role is not authorized to assign tickets.");

        if (user.IsInRole(AppRoles.Manager))
        {
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(row.AssignedToEmail))
                    continue;

                if (!agentByEmail.ContainsKey(row.AssignedToEmail.Trim()))
                    errors.Add($"Row {row.ExcelRowNumber}: Assigned To '{row.AssignedToEmail}' is not a valid agent email.");
            }
        }

        return errors;
    }

    public async Task<int> ImportTicketsAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
            throw new InvalidOperationException("There are no tickets to import.");

        var user = await RequireUserAsync();
        var errors = await ValidateImportRowsAsync(rows, ignoreAssignments, cancellationToken);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        var agents = await _userDirectory.GetAgentsAsync();
        var agentByEmail = agents.ToDictionary(a => a.Email, StringComparer.OrdinalIgnoreCase);
        var tickets = new List<Ticket>(rows.Count);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? assignedToUserId = null;
            if (!ignoreAssignments && !string.IsNullOrWhiteSpace(row.AssignedToEmail))
                assignedToUserId = agentByEmail[row.AssignedToEmail.Trim()].Id;

            tickets.Add(new Ticket
            {
                Title = row.Title,
                Description = row.Description,
                Priority = row.Priority,
                Category = row.Category,
                Status = TicketStatus.Open,
                CreatedBy = user.UserId,
                AssignedToUserId = assignedToUserId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _repository.CreateTicketsAsync(tickets, cancellationToken);
        return tickets.Count;
    }

    private static IReadOnlyList<string> ValidateTicket(Ticket ticket)
    {
        var validationContext = new ValidationContext(ticket);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(ticket, validationContext, validationResults, validateAllProperties: true);
        return validationResults
            .Select(result => result.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Cast<string>()
            .ToList();
    }

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
