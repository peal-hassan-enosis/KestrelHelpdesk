using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface IExcelTicketImportService
{
    Task<TicketImportValidationResult> ValidateAsync(Stream excelStream, CancellationToken cancellationToken = default);
    Task<int> ImportAsync(IReadOnlyList<TicketImportRow> rows, bool ignoreAssignments, CancellationToken cancellationToken = default);
}
