using Microsoft.Extensions.Options;

namespace KestrelHelpdesk.Web.Services;

public sealed class FileStorageOptions
{
    public string AttachmentsPath { get; set; } = "FileStorage/Attachments";
}

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IWebHostEnvironment environment, IOptions<FileStorageOptions> options)
    {
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.AttachmentsPath));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream source, string extension, CancellationToken cancellationToken = default)
    {
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
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
        var fullPath = GetFullPath(storagePath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storagePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private string GetFullPath(string storagePath)
    {
        var fileName = Path.GetFileName(storagePath);
        if (!string.Equals(fileName, storagePath, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage path.");

        return Path.Combine(_rootPath, fileName);
    }
}
