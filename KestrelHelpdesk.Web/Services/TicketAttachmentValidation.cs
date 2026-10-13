using Microsoft.AspNetCore.Components.Forms;

namespace KestrelHelpdesk.Web.Services;

public static class TicketAttachmentValidation
{
    public const int MaxAttachmentsPerTicket = 3;
    public const long MaxFileSize = 10 * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip"
    };

    public static IReadOnlyList<string> ValidateFiles(IReadOnlyList<IBrowserFile> files, int existingAttachmentCount = 0)
    {
        var errors = new List<string>();
        if (existingAttachmentCount + files.Count > MaxAttachmentsPerTicket)
            errors.Add($"A ticket can have a maximum of {MaxAttachmentsPerTicket} attachments.");

        foreach (var file in files)
        {
            if (!AllowedExtensions.Contains(Path.GetExtension(file.Name)))
                errors.Add($"'{file.Name}' is not an allowed file type.");
            if (file.Size > MaxFileSize)
                errors.Add($"'{file.Name}' exceeds the 10 MB file size limit.");
        }

        return errors;
    }
}
