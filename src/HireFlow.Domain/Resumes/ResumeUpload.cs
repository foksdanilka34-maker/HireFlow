namespace HireFlow.Domain.Resumes;

public class ResumeUpload
{
    public const int FileHashLength = 64;

    public Guid Id { get; private set; }
    public long TelegramId { get; private set; }
    public string FileHash { get; private set; } = null!;
    public DateTime UploadedAt { get; private set; }

    // Нужен EF Core для создания объекта при чтении из БД.
    private ResumeUpload() { }

    // Запоминает загрузку файла резюме (только хэш, без содержимого). Id выдаст база при сохранении.
    public ResumeUpload(long telegramId, string fileHash, DateTime uploadedAt)
    {
        if (fileHash.Length != FileHashLength)
            throw new DomainException("File hash must be a SHA-256 hex string.");

        TelegramId = telegramId;
        FileHash = fileHash;
        UploadedAt = uploadedAt;
    }

    // Отмечает повторную загрузку того же файла.
    public void Touch(DateTime uploadedAt) => UploadedAt = uploadedAt;
}
