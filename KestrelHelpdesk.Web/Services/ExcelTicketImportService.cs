using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KestrelHelpdesk.Web.Services;

public sealed class ExcelTicketImportService : IExcelTicketImportService
{
    private const int MaxRows = 10_000;
    private readonly ITicketService _ticketService;
    private readonly ICurrentUserContext _currentUser;

    public ExcelTicketImportService(
        ITicketService ticketService,
        ICurrentUserContext currentUser)
    {
        _ticketService = ticketService;
        _currentUser = currentUser;
    }

    public async Task<TicketImportValidationResult> ValidateAsync(
        Stream excelStream,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var rows = new List<TicketImportRow>();
        var hasAssignedToColumn = false;

        try
        {
            using var workbook = new XSSFWorkbook(excelStream);
            if (workbook.NumberOfSheets == 0)
                return new TicketImportValidationResult { Errors = ["The Excel file is empty."] };

            var sheet = workbook.GetSheetAt(0);
            if (sheet.PhysicalNumberOfRows <= 1)
                return new TicketImportValidationResult { Errors = ["The Excel file is empty."] };

            var headerRow = sheet.GetRow(sheet.FirstRowNum);
            if (headerRow is null)
                return new TicketImportValidationResult { Errors = ["The Excel file is empty."] };

            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var cellIndex = headerRow.FirstCellNum; cellIndex < headerRow.LastCellNum; cellIndex++)
            {
                var header = GetCellText(headerRow.GetCell(cellIndex));
                if (string.IsNullOrWhiteSpace(header))
                    continue;

                var normalized = NormalizeHeader(header);
                if (headers.ContainsKey(normalized))
                    errors.Add($"Duplicate Excel column '{header}'.");
                else
                    headers[normalized] = cellIndex;
            }

            foreach (var requiredHeader in new[] { "title", "description", "priority", "category" })
            {
                if (!headers.ContainsKey(requiredHeader))
                    errors.Add($"Required Excel column '{requiredHeader}' is missing.");
            }

            hasAssignedToColumn = headers.ContainsKey("assignedto");

            if (headers.ContainsKey("status"))
                errors.Add("The Status column is not allowed. New imported tickets are always created as Open.");

            if (headers.ContainsKey("createdby"))
                errors.Add("The CreatedBy column is not allowed. The authenticated importing user is used as the creator.");

            if (errors.Count > 0)
                return new TicketImportValidationResult
                {
                    Errors = errors,
                    HasAssignedToColumn = hasAssignedToColumn
                };

            var lastRow = Math.Min(sheet.LastRowNum, headerRow.RowNum + MaxRows);

            for (var rowIndex = headerRow.RowNum + 1; rowIndex <= lastRow; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = sheet.GetRow(rowIndex);
                if (row is null || IsBlankRow(row))
                    continue;

                var excelRowNumber = rowIndex + 1;
                var title = GetCellText(row.GetCell(headers["title"]));
                var description = GetCellText(row.GetCell(headers["description"]));
                var priorityText = GetCellText(row.GetCell(headers["priority"]));
                var categoryText = GetCellText(row.GetCell(headers["category"]));
                var assignedToEmail = hasAssignedToColumn ? GetCellText(row.GetCell(headers["assignedto"])) : null;

                if (string.IsNullOrWhiteSpace(title))
                    errors.Add($"Row {excelRowNumber}: Title is required.");
                else if (title.Length is < 5 or > 100)
                    errors.Add($"Row {excelRowNumber}: Title must be between 5 and 100 characters.");

                if (string.IsNullOrWhiteSpace(description))
                    errors.Add($"Row {excelRowNumber}: Description is required.");

                if (!Enum.TryParse<TicketPriority>(priorityText, true, out var priority))
                    errors.Add($"Row {excelRowNumber}: Priority '{priorityText}' is invalid.");

                if (!Enum.TryParse<TicketCategory>(categoryText, true, out var category))
                    errors.Add($"Row {excelRowNumber}: Category '{categoryText}' is invalid.");

                rows.Add(new TicketImportRow
                {
                    ExcelRowNumber = excelRowNumber,
                    Title = title,
                    Description = description,
                    Priority = priority,
                    Category = category,
                    AssignedToEmail = string.IsNullOrWhiteSpace(assignedToEmail) ? null : assignedToEmail.Trim()
                });
            }

            if (sheet.LastRowNum > headerRow.RowNum + MaxRows)
                errors.Add($"The Excel file exceeds the maximum of {MaxRows:N0} data rows.");

            if (rows.Count == 0 && errors.Count == 0)
                errors.Add("The Excel file is empty.");

            if (errors.Count == 0)
            {
                var serviceErrors = await _ticketService.ValidateImportRowsAsync(rows, ignoreAssignments: true, cancellationToken);
                errors.AddRange(serviceErrors);
            }

            var user = await _currentUser.GetCurrentUserAsync();
            var requiresAssignmentConfirmation = hasAssignedToColumn && user is not null && !user.IsInRole(AppRoles.Manager);

            return new TicketImportValidationResult
            {
                Rows = rows,
                Errors = errors,
                HasAssignedToColumn = hasAssignedToColumn,
                RequiresAssignmentConfirmation = requiresAssignmentConfirmation
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
        {
            errors.Add("The selected file could not be read as a valid .xlsx workbook.");
            return new TicketImportValidationResult { Errors = errors, HasAssignedToColumn = hasAssignedToColumn };
        }
    }

    public Task<int> ImportAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default) =>
        _ticketService.ImportTicketsAsync(rows, ignoreAssignments, cancellationToken);

    private static string NormalizeHeader(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string GetCellText(ICell? cell) =>
        cell is null ? string.Empty : cell.ToString()?.Trim() ?? string.Empty;

    private static bool IsBlankRow(IRow row)
    {
        for (var index = row.FirstCellNum; index < row.LastCellNum; index++)
        {
            if (!string.IsNullOrWhiteSpace(GetCellText(row.GetCell(index))))
                return false;
        }

        return true;
    }
}
