using KestrelHelpdesk.Core.Authorization;

namespace KestrelHelpdesk.Tests.Services;

public sealed class TicketAttachmentPolicyTests
{
    [Fact]
    public void AllowsConfiguredExtension()
    {
        Assert.True(TicketAttachmentPolicy.TryGetContentType(".pdf", out var contentType));
        Assert.Equal("application/pdf", contentType);
    }

    [Fact]
    public void RejectsUnsupportedExtension()
    {
        Assert.False(TicketAttachmentPolicy.TryGetContentType(".exe", out _));
    }

    [Fact]
    public void ExposesExpectedLimits()
    {
        Assert.Equal(3, TicketAttachmentPolicy.MaxFilesPerTicket);
        Assert.Equal(10 * 1024 * 1024, TicketAttachmentPolicy.MaxFileSize);
    }
}
