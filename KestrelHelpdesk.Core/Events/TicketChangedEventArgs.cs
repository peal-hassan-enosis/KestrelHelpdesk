namespace KestrelHelpdesk.Core.Events;

public sealed record TicketChangedEventArgs(int TicketId, TicketChangeType ChangeType);

public enum TicketChangeType
{
    StatusChanged
}
