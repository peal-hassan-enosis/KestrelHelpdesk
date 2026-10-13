namespace KestrelHelpdesk.Web.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream source, string extension, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
