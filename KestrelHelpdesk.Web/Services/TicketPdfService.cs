using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Tagging;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Pdfua;

namespace KestrelHelpdesk.Web.Services;

/// <summary>
/// Generates tagged, PDF/UA-1 ticket exports. PAC/other accessibility checkers should still
/// be run against exported files because content-level accessibility cannot be proven by build alone.
/// </summary>
public sealed class TicketPdfService : ITicketPdfService
{
    private const string DocumentLanguage = "en-US";
    private static readonly DeviceRgb HeaderBackground = new(20, 39, 72);
    private static readonly DeviceRgb HeaderForeground = new(255, 255, 255);
    private static readonly DeviceRgb BodyForeground = new(36, 48, 68);
    private static readonly DeviceRgb AlternateRowBackground = new(246, 249, 253);
    private static readonly DeviceRgb BorderColor = new(222, 229, 239);

    public byte[] Generate(
        IReadOnlyList<TicketPdfRow> rows,
        string title,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        cancellationToken.ThrowIfCancellationRequested();

        var documentTitle = NormalizeDocumentTitle(title);
        var fonts = ResolveFontFiles();
        var config = new PdfUAConfig(PdfUAConformance.PDF_UA_1, documentTitle, DocumentLanguage);

        using var memoryStream = new MemoryStream();
        var writerProperties = new WriterProperties().SetPdfVersion(PdfVersion.PDF_1_7);
        using var writer = new PdfWriter(memoryStream, writerProperties);
        using var pdf = new PdfUADocument(writer, config);

        ConfigureDocumentMetadata(pdf, documentTitle);

        using (var document = new Document(pdf, PageSize.A4.Rotate()))
        {
            var regularFont = PdfFontFactory.CreateFont(
                fonts.Regular,
                PdfEncodings.IDENTITY_H,
                PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
            var boldFont = PdfFontFactory.CreateFont(
                fonts.Bold,
                PdfEncodings.IDENTITY_H,
                PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);

            document.SetMargins(28, 28, 28, 28);

            var heading = new Paragraph(documentTitle)
                .SetFont(boldFont)
                .SetFontSize(18)
                .SetFontColor(new DeviceRgb(20, 39, 72))
                .SetMarginBottom(6);
            heading.GetAccessibilityProperties().SetRole(StandardRoles.H1);
            document.Add(heading);

            document.Add(new Paragraph(
                    $"Generated {DateTimeOffset.Now:MMMM d, yyyy 'at' h:mm tt}. This export contains {rows.Count} ticket(s).")
                .SetFont(regularFont)
                .SetFontSize(10)
                .SetFontColor(BodyForeground)
                .SetMarginBottom(16));

            var table = new Table(new float[] { 0.7f, 3.2f, 1.3f, 1.2f, 1.8f, 1.8f })
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetFont(regularFont)
                .SetFontSize(9.5f);

            AddHeader(table, "ID", boldFont);
            AddHeader(table, "Title", boldFont);
            AddHeader(table, "Status", boldFont);
            AddHeader(table, "Priority", boldFont);
            AddHeader(table, "Assigned To", boldFont);
            AddHeader(table, "Created", boldFont);

            for (var index = 0; index < rows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[index];
                var alternate = index % 2 == 1;

                AddCell(table, row.Id.ToString(), regularFont, alternate, TextAlignment.CENTER);
                AddCell(table, row.Title, regularFont, alternate);
                AddCell(table, row.Status, regularFont, alternate);
                AddCell(table, row.Priority, regularFont, alternate);
                AddCell(table, string.IsNullOrWhiteSpace(row.AssignedTo) ? "Unassigned" : row.AssignedTo, regularFont, alternate);
                AddCell(table, row.Created, regularFont, alternate);
            }

            document.Add(table);
        }

        return memoryStream.ToArray();
    }

    private static void ConfigureDocumentMetadata(PdfDocument pdf, string title)
    {
        pdf.GetCatalog().SetLang(new PdfString(DocumentLanguage));
        pdf.GetCatalog().SetViewerPreferences(new PdfViewerPreferences().SetDisplayDocTitle(true));

        var info = pdf.GetDocumentInfo();
        info.SetTitle(title);
        info.SetAuthor("Kestrel Helpdesk");
        info.SetSubject("Accessible ticket export");
        info.SetCreator("Kestrel Helpdesk");
        info.SetKeywords("Kestrel Helpdesk, support tickets, PDF/UA, accessible report");
    }

    private static void AddHeader(Table table, string value, PdfFont boldFont)
    {
        var header = new Cell()
            .Add(new Paragraph(value)
                .SetFont(boldFont)
                .SetFontSize(10)
                .SetFontColor(HeaderForeground))
            .SetBackgroundColor(HeaderBackground)
            .SetBorderColor(BorderColor)
            .SetPaddingTop(8)
            .SetPaddingBottom(8)
            .SetPaddingLeft(7)
            .SetPaddingRight(7)
            .SetTextAlignment(TextAlignment.LEFT);

        // Preserve semantic header information for screen readers and table navigation.
        header.GetAccessibilityProperties().SetRole(StandardRoles.TH);
        table.AddHeaderCell(header);
    }

    private static void AddCell(
        Table table,
        string? value,
        PdfFont regularFont,
        bool alternate,
        TextAlignment alignment = TextAlignment.LEFT)
    {
        var cell = new Cell()
            .Add(new Paragraph(value ?? string.Empty)
                .SetFont(regularFont)
                .SetFontSize(9.5f)
                .SetFontColor(BodyForeground))
            .SetBorderColor(BorderColor)
            .SetPaddingTop(7)
            .SetPaddingBottom(7)
            .SetPaddingLeft(7)
            .SetPaddingRight(7)
            .SetTextAlignment(alignment);

        if (alternate)
            cell.SetBackgroundColor(AlternateRowBackground);

        table.AddCell(cell);
    }

    private static string NormalizeDocumentTitle(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? "Kestrel Helpdesk Ticket Export"
            : title.Trim();

    private static (string Regular, string Bold) ResolveFontFiles()
    {
        var windowsFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        var candidates = OperatingSystem.IsWindows()
            ? new[]
            {
                (Path.Combine(windowsFonts, "arial.ttf"), Path.Combine(windowsFonts, "arialbd.ttf")),
                (Path.Combine(windowsFonts, "segoeui.ttf"), Path.Combine(windowsFonts, "segoeuib.ttf")),
                (Path.Combine(windowsFonts, "calibri.ttf"), Path.Combine(windowsFonts, "calibrib.ttf"))
            }
            : new[]
            {
                ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
                ("/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf"),
                ("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"),
                ("/System/Library/Fonts/Supplemental/Arial.ttf", "/System/Library/Fonts/Supplemental/Arial Bold.ttf")
            };

        foreach (var (regular, bold) in candidates)
        {
            if (File.Exists(regular) && File.Exists(bold))
                return (regular, bold);
        }

        throw new FileNotFoundException(
            "PDF export needs an embeddable TrueType font. Install Arial/Segoe UI on Windows or fonts-dejavu-core on Linux.");
    }
}
