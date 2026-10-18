namespace HireFlow.Domain.Candidates;

public class Candidate
{
    public const int NameMaxLength = 200;
    public const int PhoneMaxLength = 16;

    public Guid Id { get; private set; }
    public long TelegramId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Нужен EF Core для создания объекта при чтении из БД.
    private Candidate() { }

    // Создаёт кандидата. Id (UUIDv7) выдаётся сразу, чтобы отклик можно было сохранить вместе с кандидатом.
    public Candidate(long telegramId, string name, string phone)
    {
        Id = Guid.CreateVersion7();
        TelegramId = telegramId;
        Name = name.Trim();
        Phone = NormalizePhone(phone);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    // Обновляет имя и телефон.
    public void Update(string name, string phone)
    {
        Name = name.Trim();
        Phone = NormalizePhone(phone);
        UpdatedAt = DateTime.UtcNow;
    }

    // Приводит номер к формату E.164, невалидный номер — DomainException.
    private static string NormalizePhone(string phone) =>
        PhoneNumber.TryNormalize(phone, out var e164)
            ? e164
            : throw new DomainException("Phone number is not valid.");
}
