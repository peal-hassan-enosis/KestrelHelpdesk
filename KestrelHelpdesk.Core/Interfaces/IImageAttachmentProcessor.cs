using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface IImageAttachmentProcessor
{
    /// <summary>
    /// Validates and produces the single image payload to store. Returns null for
    /// non-image attachments. Images are never enlarged; those exceeding the longest
    /// side limit are proportionally resized.
    /// </summary>
    Task<ProcessedAttachmentImage?> ProcessAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken = default);
}
