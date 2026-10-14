using iText.Kernel.Pdf;
using KestrelHelpdesk.Web.Services;

namespace KestrelHelpdesk.Tests.Services;

public sealed class TicketPdfAccessibilityTests
{
    [Fact]
    public void Generate_CreatesTaggedPdfWithTitleAndLanguage()
    {
        var service = new TicketPdfService();
        const string title = "Ticket Board - Current Page";

        var bytes = service.Generate(
        [
            new TicketPdfRow(
                42,
                "Access should be revoked",
                "Open",
                "Low",
                "agent@kestrelhelpdesk.com",
                "2026-10-14 10:30")
        ],
        title);

        using var input = new MemoryStream(bytes);
        using var pdf = new PdfDocument(new PdfReader(input));
        var catalog = pdf.GetCatalog().GetPdfObject();

        Assert.NotNull(catalog.GetAsDictionary(PdfName.StructTreeRoot));
        Assert.Equal("en-US", catalog.GetAsString(PdfName.Lang)?.GetValue());
        Assert.Equal(title, pdf.GetDocumentInfo().GetTitle());
        Assert.True(pdf.GetCatalog().GetPdfObject()
            .GetAsDictionary(PdfName.ViewerPreferences)?
            .GetAsBool(PdfName.DisplayDocTitle)?.GetValue() == true);
        Assert.Equal(1, pdf.GetNumberOfPages());
    }
}
