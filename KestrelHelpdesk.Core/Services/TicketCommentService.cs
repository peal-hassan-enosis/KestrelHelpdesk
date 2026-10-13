using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Services;

public sealed class TicketCommentService : ITicketCommentService
{
    private readonly ITicketRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public TicketCommentService(ITicketRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task AddCommentAsync(int ticketId, Comment comment)
    {
        var user = await _currentUser.GetCurrentUserAsync()
            ?? throw new UnauthorizedAccessException("You must be signed in.");

        var ticket = await _repository.GetTicketByIdAsync(ticketId);
        if (ticket is null || !TicketAccessPolicy.CanView(user, ticket))
            throw new UnauthorizedAccessException("You are not allowed to comment on this ticket.");

        comment.TicketId = ticketId;
        comment.Author = user.Email;
        comment.CreatedAt = DateTime.UtcNow;
        await _repository.AddCommentAsync(comment);
    }
}
