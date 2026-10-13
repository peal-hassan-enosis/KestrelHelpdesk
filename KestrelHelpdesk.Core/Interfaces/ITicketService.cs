using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketService
{
    Task<Ticket?> GetTicketByIdAsync(int id);
    Task CreateTicketAsync(Ticket ticket);
    Task UpdateTicketAsync(Ticket ticket);
}
