using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketAttachmentService : ITicketAttachmentService
{
    public const long MaxFileSize = 10 * 1024 * 1024;
    public const int MaxFilesPerTicket = 3;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            [".zip"] = "application/zip"
        };

    private readonly ITicketService _ticketService;
    private readonly ITicketAttachmentRepository _repository;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserContext _currentUser;

    public TicketAttachmentService(
        ITicketService ticketService,
        ITicketAttachmentRepository repository,
        IFileStorageService storage,
        ICurrentUserContext currentUser)
    {
        _ticketService = ticketService;
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<string>> ValidateFilesAsync(
        int ticketId,
        IReadOnlyList<IBrowserFile> files,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default)
    {
        Ticket? ticket = null;
        if (ticketId > 0)
        {
            ticket = await _ticketService.GetTicketByIdAsync(ticketId);
            if (ticket is null)
                throw new ValidationException("The ticket does not exist or you do not have access to it.");
        }

        var existingAttachments = ticket?.Attachments ?? [];
        var existingIds = existingAttachments.Select(a => a.Id).ToHashSet();
        var invalidDeleteIds = attachmentIdsToDelete.Where(id => !existingIds.Contains(id)).ToList();
        if (invalidDeleteIds.Count > 0)
            throw new ValidationException("One or more attachments selected for deletion are invalid.");

        var finalCount = existingAttachments.Count - attachmentIdsToDelete.Count + files.Count;
        if (finalCount > MaxFilesPerTicket)
            throw new ValidationException($"A ticket can have a maximum of {MaxFilesPerTicket} attachments.");

        var errors = new List<string>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var originalName = Path.GetFileName(file.Name);
            var extension = Path.GetExtension(originalName);

            if (!AllowedContentTypes.ContainsKey(extension))
                errors.Add($"{originalName}: file type '{extension}' is not allowed.");

            if (file.Size > MaxFileSize)
                errors.Add($"{originalName}: file size cannot exceed 10 MB.");
        }

        return errors;
    }

    public async Task ApplyChangesAsync(
        int ticketId,
        IReadOnlyList<IBrowserFile> files,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidateFilesAsync(ticketId, files, attachmentIdsToDelete, cancellationToken);
        if (validationErrors.Count > 0)
            throw new ValidationException(string.Join(" ", validationErrors));

        var ticket = await _ticketService.GetTicketByIdAsync(ticketId)
            ?? throw new ValidationException("The ticket does not exist or you do not have access to it.");

        var currentUser = await _currentUser.GetCurrentUserAsync()
            ?? throw new ValidationException("You must be signed in to manage attachments.");

        var attachmentsToDelete = ticket.Attachments
            .Where(a => attachmentIdsToDelete.Contains(a.Id))
            .ToList();

        var savedPaths = new List<string>();
        var newAttachments = new List<TicketAttachment>();

        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var originalName = Path.GetFileName(file.Name);
                var extension = Path.GetExtension(originalName);

                await using var source = file.OpenReadStream(MaxFileSize, cancellationToken);
                var storedFileName = await _storage.SaveAsync(source, extension, cancellationToken);
                savedPaths.Add(storedFileName);

                newAttachments.Add(new TicketAttachment
                {
                    TicketId = ticketId,
                    OriginalFileName = originalName.Length > 255 ? originalName[..255] : originalName,
                    StoredFileName = storedFileName,
                    ContentType = AllowedContentTypes[extension],
                    FileSize = file.Size,
                    StoragePath = storedFileName,
                    UploadedByUserId = currentUser.UserId,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await _repository.AddRangeAsync(newAttachments, cancellationToken);
            await _repository.DeleteRangeAsync(attachmentsToDelete, cancellationToken);

            foreach (var attachment in attachmentsToDelete)
                await _storage.DeleteAsync(attachment.StoragePath, cancellationToken);
        }
        catch
        {
            foreach (var path in savedPaths)
            {
                try { await _storage.DeleteAsync(path, CancellationToken.None); }
                catch { }
            }

            throw;
        }
    }

    public async Task<(Stream Content, string ContentType, string FileName)> OpenDownloadAsync(
        int ticketId,
        int attachmentId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketService.GetTicketByIdAsync(ticketId);
        if (ticket is null)
            throw new UnauthorizedAccessException();

        var attachment = ticket.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
            throw new KeyNotFoundException();

        var stream = await _storage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        return (stream, attachment.ContentType, attachment.OriginalFileName);
    }
}
