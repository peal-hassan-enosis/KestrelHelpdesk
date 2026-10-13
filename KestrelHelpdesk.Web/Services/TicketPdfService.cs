using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketPdfService : ITicketPdfService
{
    public byte[] Generate(
        IReadOnlyList<TicketPdfRow> rows,
        string title,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var memoryStream = new MemoryStream();
        using var writer = new PdfWriter(memoryStream);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf, PageSize.A4.Rotate());

        var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        document.SetMargins(20, 20, 20, 20);
        document.Add(new Paragraph(title)
            .SetFont(boldFont)
            .SetFontSize(15)
            .SetMarginBottom(4));
        document.Add(new Paragraph($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}    Tickets: {rows.Count}")
            .SetFontSize(8)
            .SetMarginBottom(10));

        var table = new Table(new float[] { 0.7f, 3.2f, 1.3f, 1.2f, 1.8f, 1.8f })
            .SetWidth(UnitValue.CreatePercentValue(100));

        AddHeader(table, "ID", boldFont);
        AddHeader(table, "Title", boldFont);
        AddHeader(table, "Status", boldFont);
        AddHeader(table, "Priority", boldFont);
        AddHeader(table, "Assigned To", boldFont);
        AddHeader(table, "Created", boldFont);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddCell(table, row.Id.ToString());
            AddCell(table, row.Title);
            AddCell(table, row.Status);
            AddCell(table, row.Priority);
            AddCell(table, row.AssignedTo);
            AddCell(table, row.Created);
        }

        document.Add(table);
        document.Close();

        return memoryStream.ToArray();
    }

    private static void AddHeader(Table table, string value, PdfFont boldFont)
    {
        table.AddHeaderCell(new Cell()
            .Add(new Paragraph(value).SetFont(boldFont).SetFontSize(8))
            .SetTextAlignment(TextAlignment.CENTER));
    }

    private static void AddCell(Table table, string? value)
    {
        table.AddCell(new Cell()
            .Add(new Paragraph(value ?? string.Empty).SetFontSize(8)));
    }
}
