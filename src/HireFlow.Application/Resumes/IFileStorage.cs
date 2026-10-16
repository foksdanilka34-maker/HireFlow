namespace HireFlow.Application.Resumes;

public interface IFileStorage
{
    // Сохраняет файл и возвращает его ключ в хранилище.
    Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct);
}
