using KestrelHelpdesk.Core.Events;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketEventService
{
    event Func<TicketChangedEventArgs, Task>? TicketChanged;

    Task PublishTicketChangedAsync(TicketChangedEventArgs eventArgs);
}
