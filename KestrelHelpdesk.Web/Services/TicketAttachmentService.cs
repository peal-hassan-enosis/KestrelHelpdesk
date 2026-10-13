using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Infrastructure.Data;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketAttachmentService : ITicketAttachmentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip"
    };

    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IFileStorageService _fileStorage;
    private readonly Core.Interfaces.ITicketService _ticketService;

    public TicketAttachmentService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IFileStorageService fileStorage,
        Core.Interfaces.ITicketService ticketService)
    {
        _dbFactory = dbFactory;
        _fileStorage = fileStorage;
        _ticketService = ticketService;
    }

    public IReadOnlyList<string> ValidateFiles(IReadOnlyList<IBrowserFile> files, int existingAttachmentCount = 0)
    {
        var errors = new List<string>();
        var totalCount = existingAttachmentCount + files.Count;

        if (totalCount > ITicketAttachmentService.MaxAttachmentsPerTicket)
            errors.Add($"A ticket can have a maximum of {ITicketAttachmentService.MaxAttachmentsPerTicket} attachments.");

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var extension = Path.GetExtension(file.Name);

            if (!AllowedExtensions.Contains(extension))
                errors.Add($"'{file.Name}' is not an allowed file type.");

            if (file.Size > ITicketAttachmentService.MaxFileSize)
                errors.Add($"'{file.Name}' exceeds the 10 MB file size limit.");
        }

        return errors;
    }

    public async Task<IReadOnlyList<TicketAttachment>> AddAttachmentsAsync(
        int ticketId,
        IReadOnlyList<IBrowserFile> files,
        int existingAttachmentCount = 0,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(ticketId)
            ?? throw new UnauthorizedAccessException("You are not allowed to modify this ticket.");

        var validationErrors = ValidateFiles(files, existingAttachmentCount);
        if (validationErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", validationErrors));

        if (files.Count == 0)
            return Array.Empty<TicketAttachment>();

        var created = new List<TicketAttachment>(files.Count);
        var savedPaths = new List<string>(files.Count);

        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var extension = Path.GetExtension(file.Name).ToLowerInvariant();

                await using var source = file.OpenReadStream(ITicketAttachmentService.MaxFileSize, cancellationToken);
                var storagePath = await _fileStorage.SaveAsync(source, extension, cancellationToken);
                savedPaths.Add(storagePath);

                created.Add(new TicketAttachment
                {
                    TicketId = ticket.Id,
                    OriginalFileName = file.Name,
                    StoredFileName = storagePath,
                    ContentType = file.ContentType,
                    FileSize = file.Size,
                    StoragePath = storagePath,
                    UploadedByUserId = ticket.CreatedBy,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
            context.TicketAttachments.AddRange(created);
            await context.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch
        {
            foreach (var path in savedPaths)
            {
                try
                {
                    await _fileStorage.DeleteAsync(path, cancellationToken);
                }
                catch
                {
                    // Preserve the original upload/database exception.
                }
            }

            throw;
        }
    }

    public async Task DeleteAttachmentAsync(int ticketId, int attachmentId, CancellationToken cancellationToken = default)
    {
        _ = await _ticketService.GetTicketByIdAsync(ticketId)
            ?? throw new UnauthorizedAccessException("You are not allowed to modify this ticket.");

        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await context.TicketAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketId == ticketId, cancellationToken);

        if (attachment is null)
            throw new KeyNotFoundException("The attachment was not found.");

        context.TicketAttachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);

        await _fileStorage.DeleteAsync(attachment.StoragePath, cancellationToken);
    }

    public async Task<(TicketAttachment Attachment, Stream Content)> OpenAttachmentAsync(
        int ticketId,
        int attachmentId,
        CancellationToken cancellationToken = default)
    {
        _ = await _ticketService.GetTicketByIdAsync(ticketId)
            ?? throw new UnauthorizedAccessException("You are not allowed to view this ticket.");

        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await context.TicketAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.TicketId == ticketId, cancellationToken);

        if (attachment is null)
            throw new KeyNotFoundException("The attachment was not found.");

        var content = await _fileStorage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        return (attachment, content);
    }
}
