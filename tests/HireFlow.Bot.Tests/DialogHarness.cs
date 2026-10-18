using System.Text.Json;
using HireFlow.Bot.Api;
using HireFlow.Bot.Dialog;
using HireFlow.Bot.Tests.Fakes;

namespace HireFlow.Bot.Tests;

// Собранный диалог с фейками: помощники для сообщений кандидата, вакансий и разбора сессии.
internal sealed class DialogHarness
{
    public const long UserId = 555;

    public FakeHireFlowApiClient Api { get; } = new();
    public FakeChatGateway Chat { get; } = new();
    public DialogHandler Handler { get; }

    public DialogHarness() => Handler = new DialogHandler(Api, Chat);

    // Последний ответ бота кандидату.
    public string Last => Chat.Sent[^1].Text;

    // Все ответы бота одним текстом.
    public string AllReplies => string.Join("\n", Chat.Sent.Select(s => s.Text));

    // Сохранённая сессия кандидата.
    public BotSessionState Session => JsonSerializer.Deserialize<BotSessionState>(Api.Sessions[UserId])!;

    // Кандидат пишет текст.
    public Task SendAsync(string text) => Handler.HandleAsync(
        new IncomingMessage(UserId, UserId, text, null, "Иван", "Петров"), CancellationToken.None);

    // Кандидат присылает документ.
    public Task SendDocumentAsync(string fileId = "file-1", string fileName = "cv.pdf", long fileSize = 1000) =>
        Handler.HandleAsync(
            new IncomingMessage(UserId, UserId, null, new IncomingDocument(fileId, fileName, fileSize), "Иван", "Петров"),
            CancellationToken.None);

    // Кандидат присылает фото или стикер: ни текста, ни документа.
    public Task SendNonTextAsync() => Handler.HandleAsync(
        new IncomingMessage(UserId, UserId, null, null, "Иван", "Петров"), CancellationToken.None);

    // Добавляет вакансию в API.
    public VacancyDto AddVacancy(string title, string status = "open", string direction = "QA")
    {
        var vacancy = new VacancyDto(Guid.NewGuid(), title, direction, null, status);
        Api.Vacancies[vacancy.Id] = vacancy;
        return vacancy;
    }

    // Создаёт вакансии «A», «B», … для подбора.
    public List<VacancyDto> AddVacancies(int count) =>
        Enumerable.Range(0, count).Select(i => AddVacancy(((char)('A' + i)).ToString())).ToList();

    // Подбор возвращает вакансии; опыт, телефон в резюме и необходимость спросить опыт задаются.
    public void SetMatches(IEnumerable<VacancyDto> vacancies, int? years = 5, bool experienceRequired = false, bool phoneFound = true) =>
        Api.OnMatches = sent => ApiResult<MatchesDto>.Ok(
            new MatchesDto(sent ?? years, experienceRequired && sent is null, phoneFound, [.. vacancies]));

    // Сохраняет готовую сессию, чтобы проверить шаг без прохождения всего диалога.
    public void SeedSession(string step, BotDraft? draft = null, Guid? linkedVacancyId = null) =>
        Api.Sessions[UserId] = JsonSerializer.Serialize(new BotSessionState
        {
            Step = step,
            LinkedVacancyId = linkedVacancyId,
            Draft = draft ?? new BotDraft(),
        });
}
