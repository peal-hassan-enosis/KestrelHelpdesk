using System.Text.RegularExpressions;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Action;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketPdfService : ITicketPdfService
{
    private static readonly Regex LinkableTextPattern = new(
        @"(?<url>https?://[^\s<>""']+|www\.[^\s<>""']+)|(?<email>[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public byte[] Generate(
        IReadOnlyList<TicketPdfRow> rows,
        string title,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var memoryStream = new MemoryStream();
        using var writer = new PdfWriter(memoryStream);
        using var pdf = new PdfDocument(writer);

        // Generate a tagged PDF so assistive technology can interpret the table,
        // paragraphs and any email/URL links as semantic document elements.
        pdf.SetTagged();
        pdf.GetCatalog().SetLang(new PdfString("en-US"));

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

        pdf.GetDocumentInfo()
            .SetTitle(string.IsNullOrWhiteSpace(title) ? "Ticket export" : title)
            .SetAuthor("Kestrel Helpdesk")
            .SetSubject("Ticket export");

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
        var text = value ?? string.Empty;
        var paragraph = new Paragraph().SetFontSize(8);
        var matches = LinkableTextPattern.Matches(text);

        if (matches.Count == 0)
        {
            paragraph.Add(text);
        }
        else
        {
            var cursor = 0;

            foreach (Match match in matches)
            {
                if (match.Index > cursor)
                    paragraph.Add(text[cursor..match.Index]);

                // Don't include punctuation that follows a URL as part of its target.
                var linkText = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']');
                if (linkText.Length == 0)
                {
                    paragraph.Add(match.Value);
                    cursor = match.Index + match.Length;
                    continue;
                }

                var linkTarget = match.Groups["email"].Success
                    ? $"mailto:{linkText}"
                    : linkText.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                        ? $"https://{linkText}"
                        : linkText;

                paragraph.Add(new Link(linkText, PdfAction.CreateURI(linkTarget))
                    .SetFontColor(ColorConstants.BLUE)
                    .SetUnderline());

                cursor = match.Index + linkText.Length;
            }

            if (cursor < text.Length)
                paragraph.Add(text[cursor..]);
        }

        table.AddCell(new Cell().Add(paragraph));
    }
}
