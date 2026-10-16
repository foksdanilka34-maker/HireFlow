namespace HireFlow.Domain.Vacancies;

public class Vacancy
{
    public const int TitleMaxLength = 200;
    public const int DirectionMaxLength = 100;
    public const int DefaultFreezePeriodDays = 90;

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string Direction { get; private set; } = null!;
    public string? Description { get; private set; }
    public VacancyStatus Status { get; private set; }
    public VacancyRequirements Requirements { get; private set; } = null!;
    public int FreezePeriodDays { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Нужен EF Core для создания объекта при чтении из БД.
    private Vacancy() { }

    // Создаёт открытую вакансию. Id выдаст база при сохранении.
    public Vacancy(string title, string direction, string? description, VacancyRequirements requirements, int freezePeriodDays)
    {
        Title = title.Trim();
        Direction = direction.Trim();
        Description = description;
        Requirements = requirements;
        FreezePeriodDays = freezePeriodDays;
        Status = VacancyStatus.Open;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    // Меняет содержимое вакансии. Вакансию в архиве менять нельзя.
    public void Update(string title, string direction, string? description, VacancyRequirements requirements, int freezePeriodDays)
    {
        EnsureNotArchived();
        Title = title.Trim();
        Direction = direction.Trim();
        Description = description;
        Requirements = requirements;
        FreezePeriodDays = freezePeriodDays;
        UpdatedAt = DateTime.UtcNow;
    }

    // Открывает приём откликов.
    public void Open() => ChangeStatus(VacancyStatus.Open);

    // Останавливает приём откликов.
    public void Close() => ChangeStatus(VacancyStatus.Closed);

    // Переносит вакансию в архив.
    public void Archive()
    {
        Status = VacancyStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    // Возвращает вакансию из архива в статус «закрыта».
    public void Unarchive()
    {
        if (Status != VacancyStatus.Archived)
            throw new DomainException("Vacancy is not archived.");

        Status = VacancyStatus.Closed;
        UpdatedAt = DateTime.UtcNow;
    }

    // Бросает ошибку, если вакансия не принимает отклики.
    public void EnsureAcceptsApplications()
    {
        if (Status != VacancyStatus.Open)
            throw new DomainException("Vacancy is not accepting applications.");
    }

    // Меняет статус, если вакансия не в архиве.
    private void ChangeStatus(VacancyStatus newStatus)
    {
        EnsureNotArchived();
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    // Бросает ошибку, если вакансия в архиве.
    private void EnsureNotArchived()
    {
        if (Status == VacancyStatus.Archived)
            throw new DomainException("Vacancy is archived. Unarchive it first.");
    }
}
