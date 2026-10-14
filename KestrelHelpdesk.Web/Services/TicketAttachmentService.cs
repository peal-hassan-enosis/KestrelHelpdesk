using System.ComponentModel.DataAnnotations;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace KestrelHelpdesk.Web.Services;

public sealed class TicketAttachmentService : ITicketAttachmentService
{
    public long MaxFileSize => TicketAttachmentPolicy.MaxFileSize;
    public int MaxFilesPerTicket => TicketAttachmentPolicy.MaxFilesPerTicket;

    private readonly ITicketService _ticketService;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserContext _currentUser;
    private readonly IImageAttachmentProcessor _imageProcessor;
    private readonly ILogger<TicketAttachmentService> _logger;

    // The validated image output is reused by ApplyChangesAsync so that validation and
    // storage operate on the exact same processed bytes without resizing the image twice.
    private readonly Dictionary<IBrowserFile, ProcessedAttachmentImage> _processedImages =
        new(ReferenceEqualityComparer.Instance);

    public TicketAttachmentService(
        ITicketService ticketService,
        IFileStorageService storage,
        ICurrentUserContext currentUser,
        IImageAttachmentProcessor imageProcessor,
        ILogger<TicketAttachmentService> logger)
    {
        _ticketService = ticketService;
        _storage = storage;
        _currentUser = currentUser;
        _imageProcessor = imageProcessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ValidateFilesAsync(
        int ticketId,
        IReadOnlyList<IBrowserFile> files,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default)
    {
        // Release processed bytes left by a previous selection, while retaining cached
        // results for IBrowserFile instances that are still selected.
        var selectedFiles = new HashSet<IBrowserFile>(files, ReferenceEqualityComparer.Instance);
        foreach (var cachedFile in _processedImages.Keys.Where(file => !selectedFiles.Contains(file)).ToArray())
            _processedImages.Remove(cachedFile);

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
            {
                errors.Add($"{file.Name}: file size must not exceed {MaxFileSize / (1024 * 1024)} MB.");
                continue;
            }

            if (!TicketAttachmentPolicy.IsImageExtension(extension) || _processedImages.ContainsKey(file))
                continue;

            try
            {
                await using var source = file.OpenReadStream(MaxFileSize, cancellationToken);
                var processedImage = await _imageProcessor.ProcessAsync(source, extension, cancellationToken);

                if (processedImage is null)
                {
                    errors.Add($"{file.Name}: the image could not be processed.");
                    continue;
                }

                if (processedImage.Content.LongLength > MaxFileSize)
                {
                    errors.Add($"{file.Name}: the processed image exceeds the {MaxFileSize / (1024 * 1024)} MB limit. Try uploading a more compressed image.");
                    continue;
                }

                _processedImages[file] = processedImage;
            }
            catch (InvalidDataException ex)
            {
                errors.Add($"{file.Name}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var file in files)
                _processedImages.Remove(file);
        }

        return errors;
    }

    public async Task SaveTicketChangesAsync(
        Ticket ticketToSave,
        bool isCreateMode,
        IReadOnlyList<IBrowserFile> files,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default)
    {
        var ticketId = isCreateMode ? 0 : ticketToSave.Id;
        var savedFiles = new List<(string StoredFileName, string StoragePath)>();

        try
        {
            var validationErrors = await ValidateFilesAsync(
                ticketId,
                files,
                attachmentIdsToDelete,
                cancellationToken);

            if (validationErrors.Count > 0)
                throw new ValidationException(string.Join(" ", validationErrors));

            Ticket? existingTicket = null;
            if (!isCreateMode)
            {
                existingTicket = await _ticketService.GetTicketByIdAsync(ticketId);
                if (existingTicket is null)
                    throw new UnauthorizedAccessException("You are not allowed to update this ticket.");
            }

            var currentAttachments = existingTicket?.Attachments ?? [];
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var userId = currentUser?.UserId;
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("You must be signed in to upload attachments.");

            var newAttachments = new List<TicketAttachment>();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var uploadedExtension = Path.GetExtension(file.Name);
                if (!TicketAttachmentPolicy.TryGetContentType(uploadedExtension, out var originalContentType))
                    throw new ValidationException($"{file.Name}: this file type is not allowed.");

                var displayName = Path.GetFileName(file.Name);
                string extension;
                string contentType;
                long fileSize;
                (string StoredFileName, string StoragePath) storedFile;

                if (TicketAttachmentPolicy.IsImageExtension(uploadedExtension))
                {
                    if (!_processedImages.TryGetValue(file, out var processedImage))
                    {
                        await using var source = file.OpenReadStream(MaxFileSize, cancellationToken);
                        processedImage = await _imageProcessor.ProcessAsync(source, uploadedExtension, cancellationToken)
                            ?? throw new ValidationException($"{file.Name}: the image could not be processed.");
                    }

                    extension = processedImage.Extension;
                    contentType = processedImage.ContentType;
                    fileSize = processedImage.Content.LongLength;

                    if (!extension.Equals(uploadedExtension, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(contentType, originalContentType, StringComparison.OrdinalIgnoreCase))
                    {
                        displayName = Path.ChangeExtension(displayName, extension);
                    }

                    await using var processedStream = new MemoryStream(processedImage.Content, writable: false);
                    storedFile = await _storage.SaveAsync(processedStream, extension, cancellationToken);
                }
                else
                {
                    extension = uploadedExtension;
                    contentType = originalContentType;
                    fileSize = file.Size;

                    await using var source = file.OpenReadStream(MaxFileSize, cancellationToken);
                    storedFile = await _storage.SaveAsync(source, extension, cancellationToken);
                }

                savedFiles.Add(storedFile);

                newAttachments.Add(new TicketAttachment
                {
                    // TicketRepository assigns the real key inside the same DB transaction
                    // for new tickets; existing tickets already have their key.
                    TicketId = ticketId,
                    OriginalFileName = displayName,
                    StoredFileName = storedFile.StoredFileName,
                    ContentType = contentType,
                    FileSize = fileSize,
                    StoragePath = storedFile.StoragePath,
                    UploadedByUserId = userId,
                    UploadedAt = DateTime.UtcNow
                });
            }

            // Ticket data and attachment metadata are committed together. If the database
            // transaction fails, the catch block removes the staged physical uploads.
            var deletedAttachments = await _ticketService.SaveTicketWithAttachmentsAsync(
                ticketToSave,
                isCreateMode,
                newAttachments,
                attachmentIdsToDelete,
                cancellationToken);

            // Delete old file bytes only after the transaction succeeds. A cleanup failure
            // must not make a committed ticket update look as if it rolled back.
            foreach (var attachment in deletedAttachments)
            {
                try
                {
                    await _storage.DeleteAsync(attachment.StoragePath, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(
                        cleanupException,
                        "Could not remove stored file for deleted ticket attachment {AttachmentId}.",
                        attachment.Id);
                }
            }
        }
        catch
        {
            foreach (var savedFile in savedFiles)
            {
                try
                {
                    await _storage.DeleteAsync(savedFile.StoragePath, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogError(
                        cleanupException,
                        "Could not remove staged upload {StoredFileName} after a failed ticket save.",
                        savedFile.StoredFileName);
                }
            }

            throw;
        }
        finally
        {
            foreach (var file in files)
                _processedImages.Remove(file);
        }
    }

}
