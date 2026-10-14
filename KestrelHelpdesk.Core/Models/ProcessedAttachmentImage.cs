namespace KestrelHelpdesk.Core.Models;

/// <summary>
/// Describes the single image payload that should be persisted for an attachment.
/// Metadata reflects the bytes and format in Content, not the browser upload.
/// </summary>
public sealed record ProcessedAttachmentImage(
    byte[] Content,
    string Extension,
    string ContentType,
    int Width,
    int Height,
    bool WasResized);
