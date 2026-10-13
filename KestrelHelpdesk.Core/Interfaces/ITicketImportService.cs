using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface ITicketImportService
{
    Task<IReadOnlyList<string>> ValidateImportRowsAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default);

    Task<int> ImportTicketsAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default);
}
