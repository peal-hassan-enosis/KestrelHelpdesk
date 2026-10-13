namespace KestrelHelpdesk.Web.Services;

public interface ITicketPdfService
{
    byte[] Generate(
        IReadOnlyList<TicketPdfRow> rows,
        string title,
        CancellationToken cancellationToken = default);
}

public sealed record TicketPdfRow(
    int Id,
    string Title,
    string Status,
    string Priority,
    string AssignedTo,
    string Created);
