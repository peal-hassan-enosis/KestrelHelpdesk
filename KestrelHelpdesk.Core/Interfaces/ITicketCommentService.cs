using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketCommentService
{
    Task AddCommentAsync(int ticketId, Comment comment);
}
