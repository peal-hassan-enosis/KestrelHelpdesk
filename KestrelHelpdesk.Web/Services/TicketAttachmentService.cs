using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketAttachmentService : ITicketAttachmentService
{
    public long MaxFileSize => 10 * 1024 * 1024;
    public int MaxFilesPerTicket => 3;

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
        var existingAttachments = Array.Empty<TicketAttachment>();

        // A new ticket has no database ID yet. Validate its new files without
        // attempting to load ticket ID 0; access validation happens when the
        // ticket is created and ApplyChangesAsync persists the attachments.
        if (ticketId > 0)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(ticketId);
            if (ticket is null)
                throw new UnauthorizedAccessException();

            existingAttachments = (ticket.Attachments ?? []).ToArray();

            var existingIds = existingAttachments.Select(a => a.Id).ToHashSet();
            if (attachmentIdsToDelete.Any(id => !existingIds.Contains(id)))
                throw new ValidationException("One or more attachments do not belong to this ticket.");
        }
        else if (attachmentIdsToDelete.Count > 0)
        {
            throw new ValidationException("Attachments cannot be deleted before the ticket is created.");
        }

        var finalCount = existingAttachments.Length - attachmentIdsToDelete.Count + files.Count;
        if (finalCount > MaxFilesPerTicket)
            throw new ValidationException($"A ticket can have a maximum of {MaxFilesPerTicket} attachments.");

        var errors = new List<string>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extension = Path.GetExtension(file.Name);
            if (!AllowedContentTypes.ContainsKey(extension))
            {
                errors.Add($"{file.Name}: this file type is not allowed.");
                continue;
            }

            if (file.Size > MaxFileSize)
                errors.Add($"{file.Name}: file size must not exceed {MaxFileSize / (1024 * 1024)} MB.");
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

        var ticket = await _ticketService.GetTicketByIdAsync(ticketId);
        if (ticket is null)
            throw new UnauthorizedAccessException();

        var existingAttachments = ticket.Attachments ?? [];
        var attachmentsToDelete = existingAttachments
            .Where(a => attachmentIdsToDelete.Contains(a.Id))
            .ToList();

        var savedFiles = new List<(string StoredFileName, string StoragePath)>();
        try
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var userId = currentUser?.UserId;
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException();

            var newAttachments = new List<TicketAttachment>();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension = Path.GetExtension(file.Name);
                var contentType = AllowedContentTypes[extension];
                var sanitizedName = Path.GetFileName(file.Name);

                await using var stream = file.OpenReadStream(MaxFileSize, cancellationToken);
                var storedFile = await _storage.SaveAsync(stream, extension, cancellationToken);
                savedFiles.Add(storedFile);

                newAttachments.Add(new TicketAttachment
                {
                    TicketId = ticketId,
                    OriginalFileName = sanitizedName,
                    StoredFileName = storedFile.StoredFileName,
                    ContentType = contentType,
                    FileSize = file.Size,
                    StoragePath = storedFile.StoragePath,
                    UploadedByUserId = userId,
                    UploadedAt = DateTime.UtcNow
                });
            }

            if (newAttachments.Count > 0)
                await _repository.AddRangeAsync(newAttachments, cancellationToken);

            if (attachmentsToDelete.Count > 0)
                await _repository.DeleteRangeAsync(attachmentsToDelete, cancellationToken);

            foreach (var attachment in attachmentsToDelete)
                await _storage.DeleteAsync(attachment.StoragePath, cancellationToken);
        }
        catch
        {
            foreach (var savedFile in savedFiles)
                await _storage.DeleteAsync(savedFile.StoragePath, CancellationToken.None);

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

        var attachment = (ticket.Attachments ?? []).FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
            throw new FileNotFoundException();

        var stream = await _storage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        return (stream, attachment.ContentType, attachment.OriginalFileName);
    }
}
