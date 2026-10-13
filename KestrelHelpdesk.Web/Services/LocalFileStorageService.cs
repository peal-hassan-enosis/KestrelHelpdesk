using Microsoft.Extensions.Configuration;

namespace KestrelHelpdesk.Web.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration["FileStorage:AttachmentsPath"];
        _rootPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "FileStorage", "Attachments")
            : Path.GetFullPath(configuredPath, environment.ContentRootPath);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream source, string extension, CancellationToken cancellationToken = default)
    {
        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = GetFullPath(storedFileName);

        await using var destination = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

        await source.CopyToAsync(destination, cancellationToken);
        return storedFileName;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(GetFullPath(storagePath), FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = GetFullPath(storagePath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string GetFullPath(string storagePath)
    {
        var fileName = Path.GetFileName(storagePath);
        if (!string.Equals(fileName, storagePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid attachment storage path.");

        return Path.Combine(_rootPath, fileName);
    }
}
