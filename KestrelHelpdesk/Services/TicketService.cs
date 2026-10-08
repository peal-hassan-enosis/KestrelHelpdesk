namespace KestrelHelpdesk.Services;

using KestrelHelpdesk.Models;

public class TicketService : ITicketService
{
    private readonly List<Ticket> _tickets;

    public TicketService()
    {
        // Hardcoded initial data
        _tickets = Enumerable.Range(1, 20).Select(i => new Ticket
        {
            Id = i,
            Title = $"Support Issue #{i}",
            Description = "Example description text for testing UI rendering.",
            Status = (TicketStatus)(i % 3),
            Priority = (TicketPriority)(i % 4),
            Category = (TicketCategory)(i % 4),
            Comments = new List<Comment>()
        }).ToList();
    }

    public Task<List<Ticket>> GetTicketsAsync(string? statusFilter = null)
    {
        var query = _tickets.AsEnumerable();

        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<TicketStatus>(statusFilter, true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        return Task.FromResult(query.ToList());
    }

    public Task<Ticket?> GetTicketByIdAsync(int id)
    {
        var ticket = _tickets.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(ticket);
    }

    public Task CreateTicketAsync(Ticket ticket)
    {
        ticket.Id = _tickets.Max(t => t.Id) + 1;
        _tickets.Add(ticket);
        return Task.CompletedTask;
    }

    public Task UpdateTicketAsync(Ticket ticket)
    {
        var existing = _tickets.FirstOrDefault(t => t.Id == ticket.Id);
        if (existing != null)
        {
            existing.Title = ticket.Title;
            existing.Description = ticket.Description;
            existing.Priority = ticket.Priority;
            existing.Status = ticket.Status;
            existing.Category = ticket.Category;
        }
        return Task.CompletedTask;
    }

    public Task AddCommentAsync(int ticketId, Comment comment)
    {
        var ticket = _tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket != null)
        {
            comment.Id = ticket.Comments.Count + 1;
            comment.CreatedAt = DateTime.UtcNow;
            ticket.Comments.Add(comment);
        }
        return Task.CompletedTask;
    }
}