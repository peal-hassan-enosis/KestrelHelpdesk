namespace KestrelHelpdesk.Core.Models;

public sealed class TicketImportValidationResult
{
    public IReadOnlyList<TicketImportRow> Rows { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public bool HasAssignedToColumn { get; init; }
    public bool RequiresAssignmentConfirmation { get; init; }

    public bool IsValid => Errors.Count == 0 && Rows.Count > 0;
}
