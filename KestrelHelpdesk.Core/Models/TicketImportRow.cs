namespace KestrelHelpdesk.Core.Models;

public sealed class TicketImportRow
{
    public int ExcelRowNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TicketPriority Priority { get; init; }
    public TicketCategory Category { get; init; }
    public string? AssignedToEmail { get; init; }
}
