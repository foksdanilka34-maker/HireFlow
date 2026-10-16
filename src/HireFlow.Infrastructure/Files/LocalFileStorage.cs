using HireFlow.Application.Resumes;

namespace HireFlow.Infrastructure.Files;

public class LocalFileStorage(string rootPath) : IFileStorage
{
    // Кладёт файл в локальную папку. Заглушка вместо S3 (MinIO) до этапа с файловым хранилищем.
    public async Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct)
    {
        var key = $"resumes/{Guid.CreateVersion7()}.{extension}";
        var path = Path.Combine(rootPath, key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);

        return key;
    }
}
