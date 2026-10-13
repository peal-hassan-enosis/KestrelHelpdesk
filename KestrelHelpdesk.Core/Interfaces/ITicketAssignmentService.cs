namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketAssignmentService
{
    Task AssignTicketAsync(int ticketId, string? agentUserId);
}
