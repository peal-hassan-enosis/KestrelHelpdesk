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
    private readonly IImageAttachmentProcessor _imageProcessor;

    // The validated image output is reused by ApplyChangesAsync so that validation and
    // storage operate on the exact same processed bytes without resizing the image twice.
    private readonly Dictionary<IBrowserFile, ProcessedAttachmentImage> _processedImages =
        new(ReferenceEqualityComparer.Instance);

    public TicketAttachmentService(
        ITicketService ticketService,
        ITicketAttachmentRepository repository,
        IFileStorageService storage,
        ICurrentUserContext currentUser,
        IImageAttachmentProcessor imageProcessor)
    {
        _ticketService = ticketService;
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
        _imageProcessor = imageProcessor;
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

    public async Task ApplyChangesAsync(
        int ticketId,
        IReadOnlyList<IBrowserFile> files,
        IReadOnlyCollection<int> attachmentIdsToDelete,
        CancellationToken cancellationToken = default)
    {
        var savedFiles = new List<(string StoredFileName, string StoragePath)>();

        try
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

            var currentUser = await _currentUser.GetCurrentUserAsync();
            var userId = currentUser?.UserId;
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException();

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

                    // When processing changes an extension (for example, a resized static GIF
                    // becomes PNG), keep the downloadable filename consistent with stored bytes.
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
        finally
        {
            foreach (var file in files)
                _processedImages.Remove(file);
        }
    }
}
