using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HireFlow.Web.Auth;

namespace HireFlow.Web.Api;

public class HireFlowApiClient(HttpClient http, AuthState auth)
{
    public const string UnavailableError = "API недоступен";
    public const string InvalidCredentialsError = "Неверный логин или пароль";
    public const string TooManyAttemptsError = "Слишком много попыток, подождите минуту";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    // Входит по логину и паролю; при успехе запоминает рекрутера в AuthState.
    public async Task<ApiResult<RecruiterDto>> LoginAsync(string login, string password, CancellationToken ct = default)
    {
        var result = await SendAsync<RecruiterDto>(
            () => new HttpRequestMessage(HttpMethod.Post, "auth/login")
            {
                Content = JsonContent.Create(new LoginRequestDto(login, password), options: Json),
            },
            signOutOn401: false, ct);

        if (result.IsOk && result.Value is not null)
            auth.SignIn(result.Value);
        return result.Status switch
        {
            401 => result with { Error = InvalidCredentialsError },
            429 => result with { Error = TooManyAttemptsError },
            _ => result,
        };
    }

    // Выходит из сессии; локально сессия сбрасывается при любом ответе.
    public async Task<ApiResult> LogoutAsync(CancellationToken ct = default)
    {
        var result = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, "auth/logout"), ct);
        auth.SignOut();
        return result;
    }

    // Узнаёт текущего рекрутера по cookie; при успехе запоминает его в AuthState.
    public async Task<ApiResult<RecruiterDto>> GetMeAsync(CancellationToken ct = default)
    {
        var result = await SendAsync<RecruiterDto>(() => new HttpRequestMessage(HttpMethod.Get, "auth/me"), true, ct);
        if (result.IsOk && result.Value is not null)
            auth.SignIn(result.Value);
        return result;
    }

    // Возвращает вакансии, при заданном статусе — только с этим статусом.
    public Task<ApiResult<List<VacancyDto>>> GetVacanciesAsync(VacancyStatus? status = null, CancellationToken ct = default)
    {
        var url = status is null ? "vacancies" : $"vacancies?status={JsonNamingPolicy.SnakeCaseLower.ConvertName(status.Value.ToString())}";
        return SendAsync<List<VacancyDto>>(() => new HttpRequestMessage(HttpMethod.Get, url), true, ct);
    }

    // Возвращает вакансию по id.
    public Task<ApiResult<VacancyDto>> GetVacancyAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<VacancyDto>(() => new HttpRequestMessage(HttpMethod.Get, $"vacancies/{id}"), true, ct);

    // Создаёт вакансию.
    public Task<ApiResult<VacancyDto>> CreateVacancyAsync(VacancyRequestDto request, CancellationToken ct = default) =>
        SendAsync<VacancyDto>(() => new HttpRequestMessage(HttpMethod.Post, "vacancies")
        {
            Content = JsonContent.Create(request, options: Json),
        }, true, ct);

    // Сохраняет изменения вакансии.
    public Task<ApiResult<VacancyDto>> UpdateVacancyAsync(Guid id, VacancyRequestDto request, CancellationToken ct = default) =>
        SendAsync<VacancyDto>(() => new HttpRequestMessage(HttpMethod.Put, $"vacancies/{id}")
        {
            Content = JsonContent.Create(request, options: Json),
        }, true, ct);

    // Открывает вакансию.
    public Task<ApiResult<VacancyDto>> OpenAsync(Guid id, CancellationToken ct = default) => ChangeStatusAsync(id, "open", ct);

    // Закрывает вакансию.
    public Task<ApiResult<VacancyDto>> CloseAsync(Guid id, CancellationToken ct = default) => ChangeStatusAsync(id, "close", ct);

    // Переносит вакансию в архив.
    public Task<ApiResult<VacancyDto>> ArchiveAsync(Guid id, CancellationToken ct = default) => ChangeStatusAsync(id, "archive", ct);

    // Возвращает вакансию из архива.
    public Task<ApiResult<VacancyDto>> UnarchiveAsync(Guid id, CancellationToken ct = default) => ChangeStatusAsync(id, "unarchive", ct);

    // Удаляет вакансию без откликов.
    public Task<ApiResult> DeleteVacancyAsync(Guid id, CancellationToken ct = default) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, $"vacancies/{id}"), ct);

    // Вызывает действие смены статуса вакансии.
    private Task<ApiResult<VacancyDto>> ChangeStatusAsync(Guid id, string action, CancellationToken ct) =>
        SendAsync<VacancyDto>(() => new HttpRequestMessage(HttpMethod.Post, $"vacancies/{id}/{action}"), true, ct);

    // Отправляет запрос без тела ответа.
    private async Task<ApiResult> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        var result = await SendAsync<JsonElement>(createRequest, true, ct, readBody: false);
        return new ApiResult(result.Status, result.Error);
    }

    // Отправляет запрос и читает ответ; ошибки превращает в текст, 401 сбрасывает сессию.
    private async Task<ApiResult<T>> SendAsync<T>(
        Func<HttpRequestMessage> createRequest, bool signOutOn401, CancellationToken ct, bool readBody = true)
    {
        try
        {
            using var request = createRequest();
            using var response = await http.SendAsync(request, ct);
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                if (!readBody)
                    return new ApiResult<T>(status);
                var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                return new ApiResult<T>(status, value);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && signOutOn401)
                auth.SignOut();
            return new ApiResult<T>(status, Error: await ReadErrorAsync(response, ct));
        }
        catch (Exception e) when (e is HttpRequestException or JsonException
            || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Сеть недоступна, таймаут или ответ не от API (тело не JSON).
            return new ApiResult<T>(0, Error: UnavailableError);
        }
    }

    // Собирает текст ошибки из ProblemDetails, без тела — общий текст по коду.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(Json, ct);
            if (problem?.Errors is { Count: > 0 } errors)
                return string.Join("; ", errors.SelectMany(e => e.Value.Select(m => $"{e.Key}: {m}")));
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                return problem.Detail;
        }
        catch (JsonException)
        {
            // Тело не ProblemDetails — ниже вернётся общий текст.
        }

        return DefaultError((int)response.StatusCode);
    }

    // Общий текст ошибки по HTTP-коду.
    public static string DefaultError(int status) => status switch
    {
        400 => "Некорректный запрос",
        401 => "Требуется вход",
        403 => "Нет доступа",
        404 => "Не найдено",
        409 => "Действие недоступно в текущем состоянии",
        429 => "Слишком много запросов, подождите",
        _ => $"Ошибка сервера ({status})",
    };

    private record ProblemDto(string? Detail, Dictionary<string, string[]>? Errors);
}
