using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Interfaces;

public interface IImageAttachmentProcessor
{
    /// <summary>
    /// Validates a still image before a ticket is created or updated.
    /// Animated images are not supported.
    /// </summary>
    Task ValidateAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces the one image payload to store. Returns null for non-image attachments.
    /// Images are never enlarged; images above the configured longest-side limit are
    /// proportionally resized.
    /// </summary>
    Task<ProcessedAttachmentImage?> ProcessAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken = default);
}
