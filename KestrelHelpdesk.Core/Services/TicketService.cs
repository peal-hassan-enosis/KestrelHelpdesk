using System.ComponentModel.DataAnnotations;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Events;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Services;

public sealed class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITicketEventService _ticketEvents;

    public TicketService(
        ITicketRepository repository,
        ICurrentUserContext currentUser,
        ITicketEventService ticketEvents)
    {
        _repository = repository;
        _currentUser = currentUser;
        _ticketEvents = ticketEvents;
    }

    public async Task<Ticket?> GetTicketByIdAsync(int id)
    {
        var user = await RequireUserAsync();
        var ticket = await _repository.GetTicketByIdAsync(id);
        return ticket is not null && TicketAccessPolicy.CanView(user, ticket) ? ticket : null;
    }

    public async Task CreateTicketAsync(Ticket ticket)
    {
        var user = await RequireUserAsync();
        ValidateTicket(ticket);

        ticket.CreatedBy = user.UserId;
        ticket.Status = TicketStatus.Open;
        ticket.AssignedToUserId = null;
        ticket.CreatedAt = DateTime.UtcNow;

        await _repository.CreateTicketAsync(ticket);
    }

    public async Task UpdateTicketAsync(Ticket ticket)
    {
        var user = await RequireUserAsync();
        var existing = await _repository.GetTicketByIdAsync(ticket.Id)
            ?? throw new KeyNotFoundException($"Ticket {ticket.Id} was not found.");

        if (!TicketAccessPolicy.CanEdit(user, existing))
            throw new UnauthorizedAccessException("You are not allowed to edit this ticket.");

        var statusChanged = existing.Status != ticket.Status;
        if (user.IsInRole(AppRoles.Manager))
            ticket.Status = existing.Status;

        ValidateTicket(ticket);

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

    private async Task<CurrentUserInfo> RequireUserAsync() =>
        await _currentUser.GetCurrentUserAsync()
        ?? throw new UnauthorizedAccessException("You must be signed in.");

    private static void ValidateTicket(Ticket ticket)
    {
        var validationContext = new ValidationContext(ticket);
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(ticket, validationContext, validationResults, validateAllProperties: true);
        var errors = validationResults
            .Select(result => result.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Cast<string>()
            .ToList();

        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));
    }
}
