using System.Text.Json;
using HireFlow.Bot.Api;

namespace HireFlow.Bot.Tests.Fakes;

// Самописный клиент API: данные в памяти, ответы на подбор и отклик настраиваются, все вызовы записываются.
internal sealed class FakeHireFlowApiClient : IHireFlowApiClient
{
    public Dictionary<Guid, VacancyDto> Vacancies { get; } = [];
    public List<VacancyDto> OpenVacancies { get; } = [];
    public Dictionary<long, string> Sessions { get; } = [];
    public List<CandidateApplicationDto> Applications { get; } = [];
    public List<DueNotificationDto> Due { get; } = [];

    // Кандидат уже есть в API (ответ 200) или ещё нет (404).
    public bool CandidateExists { get; set; }

    public Func<int?, ApiResult<MatchesDto>> OnMatches { get; set; } =
        _ => ApiResult<MatchesDto>.Ok(new MatchesDto(null, false, true, []));

    public Func<CreateApplicationCall, ApiResult<ApplicationCreatedDto>> OnCreateApplication { get; set; } =
        _ => ApiResult<ApplicationCreatedDto>.Ok(new ApplicationCreatedDto(Guid.NewGuid()));

    public List<(byte[] File, int? Years)> MatchCalls { get; } = [];
    public List<CreateApplicationCall> ApplicationCalls { get; } = [];
    public int OpenVacanciesRequests { get; private set; }
    public int PutSessionCalls { get; private set; }

    // Если включено, сохранение сессии завершается ошибкой API.
    public bool FailPutSession { get; set; }
    public List<Guid> SentIds { get; } = [];
    public List<(Guid Id, string Error)> FailedIds { get; } = [];

    public Task<ApiResult<VacancyDto>> GetVacancyAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Vacancies.TryGetValue(id, out var v)
            ? ApiResult<VacancyDto>.Ok(v)
            : ApiResult<VacancyDto>.Failure(ApiStatus.NotFound));

    public Task<ApiResult<List<VacancyDto>>> GetOpenVacanciesAsync(CancellationToken ct)
    {
        OpenVacanciesRequests++;
        return Task.FromResult(ApiResult<List<VacancyDto>>.Ok([.. OpenVacancies]));
    }

    public Task<ApiResult<MatchesDto>> FindMatchesAsync(
        long telegramId, byte[] file, string fileName, int? yearsOfExperience, CancellationToken ct)
    {
        MatchCalls.Add((file, yearsOfExperience));
        return Task.FromResult(OnMatches(yearsOfExperience));
    }

    public Task<ApiResult<ApplicationCreatedDto>> CreateApplicationAsync(CreateApplicationCall call, CancellationToken ct)
    {
        ApplicationCalls.Add(call);
        return Task.FromResult(OnCreateApplication(call));
    }

    public Task<ApiResult<CandidateDto>> GetCandidateAsync(long telegramId, CancellationToken ct) =>
        Task.FromResult(CandidateExists
            ? ApiResult<CandidateDto>.Ok(new CandidateDto(Guid.NewGuid(), telegramId, "Иван"))
            : ApiResult<CandidateDto>.Failure(ApiStatus.NotFound));

    public Task<ApiResult<List<CandidateApplicationDto>>> GetCandidateApplicationsAsync(long telegramId, CancellationToken ct) =>
        Task.FromResult(ApiResult<List<CandidateApplicationDto>>.Ok([.. Applications]));

    // Сессии хранятся сериализованными: бот не должен зависеть от общей ссылки на объект, как и с настоящим API.
    public Task<ApiResult<BotSessionState>> GetSessionAsync(long telegramId, CancellationToken ct) =>
        Task.FromResult(Sessions.TryGetValue(telegramId, out var json)
            ? ApiResult<BotSessionState>.Ok(JsonSerializer.Deserialize<BotSessionState>(json)!)
            : ApiResult<BotSessionState>.Failure(ApiStatus.NotFound));

    public Task<ApiResult<BotSessionState>> PutSessionAsync(long telegramId, BotSessionState state, CancellationToken ct)
    {
        PutSessionCalls++;
        if (FailPutSession)
            return Task.FromResult(ApiResult<BotSessionState>.Failure(ApiStatus.Error));

        Sessions[telegramId] = JsonSerializer.Serialize(state);
        return Task.FromResult(ApiResult<BotSessionState>.Ok(state));
    }

    public Task<ApiResult<List<DueNotificationDto>>> GetDueNotificationsAsync(CancellationToken ct) =>
        Task.FromResult(ApiResult<List<DueNotificationDto>>.Ok([.. Due]));

    public Task<ApiStatus> MarkNotificationSentAsync(Guid id, CancellationToken ct)
    {
        SentIds.Add(id);
        return Task.FromResult(ApiStatus.Ok);
    }

    public Task<ApiStatus> MarkNotificationFailedAsync(Guid id, string error, CancellationToken ct)
    {
        FailedIds.Add((id, error));
        return Task.FromResult(ApiStatus.Ok);
    }
}
