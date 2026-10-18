namespace HireFlow.Domain.BotSessions;

public class BotSession
{
    public const int StepMaxLength = 50;
    public const int DraftMaxBytes = 8 * 1024;

    public Guid Id { get; private set; }
    public long TelegramId { get; private set; }
    public string Step { get; private set; } = null!;
    public Guid? LinkedVacancyId { get; private set; }
    public string Draft { get; private set; } = null!;
    public DateTime UpdatedAt { get; private set; }

    // Нужен EF Core для создания объекта при чтении из БД.
    private BotSession() { }

    // Создаёт сессию диалога. Id выдаст база при сохранении.
    public BotSession(long telegramId, string step, Guid? linkedVacancyId, string draft)
    {
        TelegramId = telegramId;
        Apply(step, linkedVacancyId, draft);
    }

    // Заменяет шаг, привязку к вакансии и черновик бота.
    public void Update(string step, Guid? linkedVacancyId, string draft) => Apply(step, linkedVacancyId, draft);

    // Применяет новое состояние и обновляет время.
    private void Apply(string step, Guid? linkedVacancyId, string draft)
    {
        Step = step;
        LinkedVacancyId = linkedVacancyId;
        Draft = draft;
        UpdatedAt = DateTime.UtcNow;
    }
}
