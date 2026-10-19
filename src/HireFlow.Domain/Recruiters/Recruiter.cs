namespace HireFlow.Domain.Recruiters;

public class Recruiter
{
    public const int LoginMaxLength = 50;
    public const int PasswordHashMaxLength = 200;

    public Guid Id { get; private set; }
    public string Login { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    // Нужен EF Core для создания объекта при чтении из БД.
    private Recruiter() { }

    // Создаёт рекрутера с нормализованным логином. Id выдаст база при сохранении.
    public Recruiter(string login, string passwordHash)
    {
        Login = NormalizeLogin(login);
        PasswordHash = passwordHash;
        CreatedAt = DateTime.UtcNow;
    }

    // Заменяет хэш пароля.
    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    // Приводит логин к виду для хранения и поиска: без пробелов по краям, в нижнем регистре, только a-z 0-9 . _ -.
    public static string NormalizeLogin(string login)
    {
        var normalized = (login ?? "").Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            throw new DomainException("Login is required.");
        if (normalized.Length > LoginMaxLength)
            throw new DomainException($"Login must not exceed {LoginMaxLength} characters.");
        if (!normalized.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-'))
            throw new DomainException("Login may contain only latin letters, digits, '.', '_' and '-'.");

        return normalized;
    }
}
