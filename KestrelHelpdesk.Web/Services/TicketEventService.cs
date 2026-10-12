using KestrelHelpdesk.Core.Events;
using KestrelHelpdesk.Core.Interfaces;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketEventService : ITicketEventService
{
    public event Func<TicketChangedEventArgs, Task>? TicketChanged;

    public async Task PublishTicketChangedAsync(TicketChangedEventArgs eventArgs)
    {
        var handlers = TicketChanged?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            if (handler is not Func<TicketChangedEventArgs, Task> callback) continue;
            await callback(eventArgs);
        }
    }
}
