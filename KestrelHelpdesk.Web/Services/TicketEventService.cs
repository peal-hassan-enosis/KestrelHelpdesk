using KestrelHelpdesk.Core.Events;
using KestrelHelpdesk.Core.Interfaces;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketEventService : ITicketEventService
{
    private readonly ILogger<TicketEventService> _logger;

    public TicketEventService(ILogger<TicketEventService> logger)
    {
        _logger = logger;
    }
    public event Func<TicketChangedEventArgs, Task>? TicketChanged;

    public async Task PublishTicketChangedAsync(TicketChangedEventArgs eventArgs)
    {
        var handlers = TicketChanged?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            if (handler is not Func<TicketChangedEventArgs, Task> callback) continue;
            try
            {
                await callback(eventArgs);
            }
            catch (Exception exception)
            {
                // Event handlers refresh connected UI circuits. A disconnected or failing
                // subscriber must not turn a successfully committed database save into an
                // apparent failure for the user.
                _logger.LogWarning(exception, "Ticket change notification failed for ticket {TicketId}.", eventArgs.TicketId);
            }
        }
    }
}
