using System.Globalization;
using System.Text.RegularExpressions;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Action;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Pdf.Tagging;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Pdfua;
using IOPath = System.IO.Path;

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
    private static readonly DeviceRgb LinkForeground = new(37, 88, 176);

    // PAC checks URL and email-looking text even when there is no annotation. These values
    // are emitted as real, tagged links so both the visible text and destination are accessible.
    private static readonly Regex LinkableTextPattern = new(
        @"(?<url>(?:https?://|www\.)[^\s<>]+)|(?<email>[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            var generatedAt = DateTimeOffset.Now.ToString(
                "MMMM d, yyyy 'at' h:mm tt",
                CultureInfo.GetCultureInfo(DocumentLanguage));
            document.Add(new Paragraph(
                    $"Generated {generatedAt}. This export contains {rows.Count} ticket(s).")
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

            // Preserve navigable document structure for PDF accessibility tools and readers.
            pdf.InitializeOutlines();
            var outlineRoot = pdf.GetOutlines(false);
            var reportBookmark = outlineRoot.AddOutline(documentTitle);
            reportBookmark.AddDestination(PdfExplicitDestination.CreateFit(pdf.GetFirstPage()));
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
            .SetBorder(new SolidBorder(BorderColor, 0.5f))
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
        var paragraph = CreateAccessibleParagraph(value ?? string.Empty, regularFont, 9.5f);

        var cell = new Cell()
            .Add(paragraph)
            .SetBorder(new SolidBorder(BorderColor, 0.5f))
            .SetPaddingTop(7)
            .SetPaddingBottom(7)
            .SetPaddingLeft(7)
            .SetPaddingRight(7)
            .SetTextAlignment(alignment);

        if (alternate)
            cell.SetBackgroundColor(AlternateRowBackground);

        table.AddCell(cell);
    }

    private static Paragraph CreateAccessibleParagraph(string value, PdfFont font, float fontSize)
    {
        var paragraph = new Paragraph()
            .SetFont(font)
            .SetFontSize(fontSize)
            .SetFontColor(BodyForeground);

        var position = 0;
        foreach (Match match in LinkableTextPattern.Matches(value))
        {
            var matchedText = match.Value;
            var linkText = TrimLinkPunctuation(matchedText, out var trailingText);

            if (linkText.Length == 0)
                continue;

            if (match.Index > position)
                paragraph.Add(value[position..match.Index]);

            var isEmail = match.Groups["email"].Success;
            var target = isEmail
                ? $"mailto:{linkText}"
                : linkText.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                    ? $"https://{linkText}"
                    : linkText;

            var link = new Link(linkText, PdfAction.CreateURI(target))
                .SetFont(font)
                .SetFontSize(fontSize)
                .SetFontColor(LinkForeground);

            // Explicitly assign the Link structure role; iText also associates the Link
            // annotation with this element, making the link discoverable by screen readers.
            link.GetAccessibilityProperties().SetRole(StandardRoles.LINK);
            paragraph.Add(link);

            if (trailingText.Length > 0)
                paragraph.Add(trailingText);

            position = match.Index + matchedText.Length;
        }

        if (position < value.Length)
            paragraph.Add(value[position..]);

        return paragraph;
    }

    private static string TrimLinkPunctuation(string candidate, out string trailingText)
    {
        var trimmed = candidate.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\'');
        trailingText = candidate[trimmed.Length..];
        return trimmed;
    }

    private static string NormalizeDocumentTitle(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? "Kestrel Helpdesk Ticket Export"
            : title.Trim();

    private static (string Regular, string Bold) ResolveFontFiles()
    {
        var windowsFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        (string Regular, string Bold)[] candidates;

        if (OperatingSystem.IsWindows())
        {
            candidates = new (string Regular, string Bold)[]
            {
                (IOPath.Combine(windowsFonts, "arial.ttf"), IOPath.Combine(windowsFonts, "arialbd.ttf")),
                (IOPath.Combine(windowsFonts, "segoeui.ttf"), IOPath.Combine(windowsFonts, "segoeuib.ttf")),
                (IOPath.Combine(windowsFonts, "calibri.ttf"), IOPath.Combine(windowsFonts, "calibrib.ttf"))
            };
        }
        else
        {
            candidates = new (string Regular, string Bold)[]
            {
                ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
                ("/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf"),
                ("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"),
                ("/System/Library/Fonts/Supplemental/Arial.ttf", "/System/Library/Fonts/Supplemental/Arial Bold.ttf")
            };
        }

        foreach (var (regular, bold) in candidates)
        {
            if (File.Exists(regular) && File.Exists(bold))
                return (regular, bold);
        }

        throw new FileNotFoundException(
            "PDF export needs an embeddable TrueType font. Install Arial/Segoe UI on Windows or fonts-dejavu-core on Linux.");
    }
}
