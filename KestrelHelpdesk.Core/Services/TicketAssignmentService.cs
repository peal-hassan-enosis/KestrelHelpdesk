using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;

namespace KestrelHelpdesk.Core.Services;

public sealed class TicketAssignmentService : ITicketAssignmentService
{
    private readonly ITicketRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserDirectory _userDirectory;

    public TicketAssignmentService(
        ITicketRepository repository,
        ICurrentUserContext currentUser,
        IUserDirectory userDirectory)
    {
        _repository = repository;
        _currentUser = currentUser;
        _userDirectory = userDirectory;
    }

    public async Task AssignTicketAsync(int ticketId, string? agentUserId)
    {
        var user = await _currentUser.GetCurrentUserAsync()
            ?? throw new UnauthorizedAccessException("You must be signed in.");

        var ticket = await _repository.GetTicketByIdAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");

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
}
