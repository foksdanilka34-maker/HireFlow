namespace HireFlow.Domain.ApiClients;

public class ApiClient
{
    public const int NameMaxLength = 50;
    public const int KeyPrefixLength = 8;
    public const int KeyHashLength = 64;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string KeyPrefix { get; private set; } = null!;
    public string KeyHash { get; private set; } = null!;
    public int DailyQuota { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    // Нужен EF Core для создания объекта при чтении из БД.
    private ApiClient() { }

    // Создаёт клиента публичного API с нормализованным именем и ключом. Id выдаст база при сохранении.
    public ApiClient(string name, string keyPrefix, string keyHash, int dailyQuota)
    {
        Name = NormalizeName(name);
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
        DailyQuota = dailyQuota;
        CreatedAt = DateTime.UtcNow;
    }

    // Заменяет ключ на новый и снимает отзыв: старый ключ перестаёт работать.
    public void ReplaceKey(string keyPrefix, string keyHash)
    {
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
        RevokedAt = null;
    }

    // Отзывает ключ клиента.
    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
    }

    // Приводит имя к виду для хранения и поиска: без пробелов по краям, в нижнем регистре, только a-z 0-9 . _ -.
    public static string NormalizeName(string name)
    {
        var normalized = (name ?? "").Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            throw new DomainException("Client name is required.");
        if (normalized.Length > NameMaxLength)
            throw new DomainException($"Client name must not exceed {NameMaxLength} characters.");
        if (!normalized.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-'))
            throw new DomainException("Client name may contain only latin letters, digits, '.', '_' and '-'.");

        return normalized;
    }
}
