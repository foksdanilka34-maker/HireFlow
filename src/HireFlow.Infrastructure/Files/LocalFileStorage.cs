using HireFlow.Application.Resumes;

namespace HireFlow.Infrastructure.Files;

public class LocalFileStorage(string rootPath) : IFileStorage
{
    private readonly string _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));

    // Кладёт файл в локальную папку. Заглушка вместо S3 (MinIO) до этапа с файловым хранилищем.
    public async Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct)
    {
        var key = $"resumes/{Guid.CreateVersion7()}.{extension}";
        var path = Path.Combine(_root, key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);

        return key;
    }

    // Открывает файл на чтение; нет файла или ключ ведёт за пределы папки хранилища — null.
    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return Task.FromResult<Stream?>(null);

        try
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }
}
