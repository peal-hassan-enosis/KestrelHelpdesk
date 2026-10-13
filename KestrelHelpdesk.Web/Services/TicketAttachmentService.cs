using System.ComponentModel.DataAnnotations;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketAttachmentService : ITicketAttachmentService
{
    public long MaxFileSize => TicketAttachmentPolicy.MaxFileSize;
    public int MaxFilesPerTicket => TicketAttachmentPolicy.MaxFilesPerTicket;

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
            if (!TicketAttachmentPolicy.TryGetContentType(extension, out _))
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
                if (!TicketAttachmentPolicy.TryGetContentType(extension, out var contentType))
                    throw new ValidationException($"{file.Name}: this file type is not allowed.");

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
}
