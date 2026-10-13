namespace KestrelHelpdesk.Core.Interfaces;

public interface IFileStorageService
{
    Task<(string StoredFileName, string StoragePath)> SaveAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default);
}
