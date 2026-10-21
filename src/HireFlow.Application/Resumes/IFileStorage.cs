namespace HireFlow.Application.Resumes;

public interface IFileStorage
{
    // Сохраняет файл и возвращает его ключ в хранилище.
    Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct);

    // Открывает файл по ключу на чтение; нет файла — null.
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
}
