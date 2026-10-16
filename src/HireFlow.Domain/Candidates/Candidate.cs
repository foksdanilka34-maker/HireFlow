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

    // Создаёт кандидата. Id выдаст база при сохранении.
    public Candidate(long telegramId, string name, string phone)
    {
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

    // Приводит номер из Telegram к формату E.164: Telegram иногда присылает его без «+».
    private static string NormalizePhone(string phone) =>
        phone.StartsWith('+') ? phone : $"+{phone}";
}
