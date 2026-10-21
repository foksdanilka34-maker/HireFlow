using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace HireFlow.Bot.Api;

public class HireFlowApiClient(HttpClient http, ILogger<HireFlowApiClient> logger) : IHireFlowApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public Task<ApiResult<VacancyDto>> GetVacancyAsync(Guid id, CancellationToken ct) =>
        SendAsync<VacancyDto>("get_vacancy", () => new HttpRequestMessage(HttpMethod.Get, $"vacancies/{id}"), ct);

    public Task<ApiResult<List<VacancyDto>>> GetOpenVacanciesAsync(CancellationToken ct) =>
        SendAsync<List<VacancyDto>>("get_open_vacancies", () => new HttpRequestMessage(HttpMethod.Get, "vacancies?status=open"), ct);

    public Task<ApiResult<MatchesDto>> FindMatchesAsync(
        long telegramId, byte[] file, string fileName, int? yearsOfExperience, CancellationToken ct) =>
        SendAsync<MatchesDto>("find_matches", () =>
        {
            var form = new MultipartFormDataContent();
            if (yearsOfExperience is not null)
                form.Add(new StringContent(yearsOfExperience.Value.ToString()), "yearsOfExperience");
            form.Add(FileContent(file), "file", SafeFileName(fileName));
            return new HttpRequestMessage(HttpMethod.Post, $"candidates/{telegramId}/matches") { Content = form };
        }, ct);

    public Task<ApiResult<ApplicationCreatedDto>> CreateApplicationAsync(CreateApplicationCall call, CancellationToken ct) =>
        SendAsync<ApplicationCreatedDto>("create_application", () =>
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(call.TelegramId.ToString()), "telegramId" },
                { new StringContent(call.VacancyId.ToString()), "vacancyId" },
                { new StringContent(call.YearsOfExperience.ToString()), "yearsOfExperience" },
                { new StringContent(call.Name), "name" },
            };
            if (!string.IsNullOrWhiteSpace(call.Phone))
                form.Add(new StringContent(call.Phone), "phone");
            form.Add(FileContent(call.File), "file", SafeFileName(call.FileName));
            return new HttpRequestMessage(HttpMethod.Post, "applications") { Content = form };
        }, ct);

    public Task<ApiResult<CandidateDto>> GetCandidateAsync(long telegramId, CancellationToken ct) =>
        SendAsync<CandidateDto>("get_candidate", () => new HttpRequestMessage(HttpMethod.Get, $"candidates/{telegramId}"), ct);

    public Task<ApiResult<List<CandidateApplicationDto>>> GetCandidateApplicationsAsync(long telegramId, CancellationToken ct) =>
        SendAsync<List<CandidateApplicationDto>>("get_candidate_applications",
            () => new HttpRequestMessage(HttpMethod.Get, $"candidates/{telegramId}/applications"), ct);

    public async Task<ApiResult<BotSessionState>> GetSessionAsync(long telegramId, CancellationToken ct)
    {
        var result = await SendAsync<SessionWire>("get_session",
            () => new HttpRequestMessage(HttpMethod.Get, $"bot-sessions/{telegramId}"), ct);
        return FromWire(result);
    }

    public async Task<ApiResult<BotSessionState>> PutSessionAsync(long telegramId, BotSessionState state, CancellationToken ct)
    {
        var result = await SendAsync<SessionWire>("put_session", () => new HttpRequestMessage(HttpMethod.Put, $"bot-sessions/{telegramId}")
        {
            Content = JsonContent.Create(new { step = state.Step, linkedVacancyId = state.LinkedVacancyId, draft = state.Draft }, options: Json),
        }, ct);
        return FromWire(result);
    }

    public Task<ApiResult<List<DueNotificationDto>>> GetDueNotificationsAsync(CancellationToken ct) =>
        SendAsync<List<DueNotificationDto>>("get_due_notifications",
            () => new HttpRequestMessage(HttpMethod.Get, "internal/notifications/due"), ct);

    public Task<ApiStatus> MarkNotificationSentAsync(Guid id, CancellationToken ct) =>
        SendNoContentAsync("notification_sent",
            () => new HttpRequestMessage(HttpMethod.Post, $"internal/notifications/{id}/sent"), ct);

    public Task<ApiStatus> MarkNotificationFailedAsync(Guid id, string error, CancellationToken ct) =>
        SendNoContentAsync("notification_failed", () => new HttpRequestMessage(HttpMethod.Post, $"internal/notifications/{id}/failed")
        {
            Content = JsonContent.Create(new { error }, options: Json),
        }, ct);

    private record SessionWire(string Step, Guid? LinkedVacancyId, BotDraft? Draft);

    // Переводит ответ API с сессией в состояние диалога.
    private static ApiResult<BotSessionState> FromWire(ApiResult<SessionWire> result) =>
        result is { IsOk: true, Value: { } wire }
            ? ApiResult<BotSessionState>.Ok(new BotSessionState
            {
                Step = wire.Step,
                LinkedVacancyId = wire.LinkedVacancyId,
                Draft = wire.Draft ?? new BotDraft(),
            })
            : ApiResult<BotSessionState>.Failure(result.Status);

    // Оборачивает байты файла в часть multipart-формы.
    private static ByteArrayContent FileContent(byte[] file) => new(file);

    // Имя файла для формы; пустое заменяем нейтральным.
    private static string SafeFileName(string fileName) => string.IsNullOrWhiteSpace(fileName) ? "resume" : fileName;

    // Выполняет запрос и разбирает JSON-ответ; 400/404/409 возвращает как статус, остальное — как Error.
    private async Task<ApiResult<T>> SendAsync<T>(string operation, Func<HttpRequestMessage> build, CancellationToken ct)
    {
        try
        {
            using var request = build();
            using var response = await http.SendAsync(request, ct);
            var status = MapStatus(operation, response.StatusCode);
            if (status != ApiStatus.Ok)
                return ApiResult<T>.Failure(status);

            var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
            return value is null ? ApiResult<T>.Failure(ApiStatus.Error) : ApiResult<T>.Ok(value);
        }
        catch (Exception ex) when (IsTransient(ex, ct))
        {
            logger.LogWarning("API call {Operation} failed: {ErrorType}", operation, ex.GetType().Name);
            return ApiResult<T>.Failure(ApiStatus.Error);
        }
    }

    // Выполняет запрос без тела ответа и возвращает только статус.
    private async Task<ApiStatus> SendNoContentAsync(string operation, Func<HttpRequestMessage> build, CancellationToken ct)
    {
        try
        {
            using var request = build();
            using var response = await http.SendAsync(request, ct);
            return MapStatus(operation, response.StatusCode);
        }
        catch (Exception ex) when (IsTransient(ex, ct))
        {
            logger.LogWarning("API call {Operation} failed: {ErrorType}", operation, ex.GetType().Name);
            return ApiStatus.Error;
        }
    }

    // Сетевая ошибка, таймаут или битый JSON — не отмена по запросу хоста.
    private static bool IsTransient(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or NotSupportedException
        || (ex is OperationCanceledException && !ct.IsCancellationRequested);

    // Переводит HTTP-код в статус результата и пишет в лог только код.
    private ApiStatus MapStatus(string operation, HttpStatusCode code)
    {
        var status = code switch
        {
            _ when (int)code is >= 200 and < 300 => ApiStatus.Ok,
            HttpStatusCode.BadRequest => ApiStatus.BadRequest,
            HttpStatusCode.NotFound => ApiStatus.NotFound,
            HttpStatusCode.Conflict => ApiStatus.Conflict,
            HttpStatusCode.TooManyRequests => ApiStatus.TooManyRequests,
            _ => ApiStatus.Error,
        };

        if (status == ApiStatus.Error)
            logger.LogWarning("API call {Operation} returned {StatusCode}", operation, (int)code);

        return status;
    }
}
