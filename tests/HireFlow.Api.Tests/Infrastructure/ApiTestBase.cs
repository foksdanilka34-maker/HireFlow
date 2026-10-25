using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HireFlow.Application.Candidates;
using HireFlow.Application.JobApplications;
using HireFlow.Application.Notifications;
using HireFlow.Application.Vacancies;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using HireFlow.Domain.Vacancies;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests.Infrastructure;

// Общие помощники интеграционных тестов: HTTP-вызовы, данные с уникальными именами, доступ к БД и хранилищу.
public abstract class ApiTestBase(ApiAppFixture app) : IClassFixture<ApiAppFixture>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    protected ApiAppFixture App => app;
    protected HttpClient Client => app.Client;

    // Возвращает строку с уникальным суффиксом: безопасна как название навыка (только буквы и цифры).
    protected static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..12];

    // Возвращает случайный уникальный Telegram ID.
    protected static long NewTelegramId() => Random.Shared.NextInt64(1_000_000, 1L << 50);

    // Читает тело ответа как JSON нужного типа.
    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    // Создаёт вакансию через API и проверяет, что она создана.
    protected async Task<VacancyResponse> CreateVacancyAsync(
        string[] required,
        string[]? niceToHave = null,
        int minYears = 0,
        int passingScore = 70,
        int borderlineWidth = 10,
        string? title = null)
    {
        var request = VacancyRequestFor(required, niceToHave, minYears, passingScore, borderlineWidth, title);
        var response = await Client.PostAsJsonAsync("/vacancies", request, Json);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<VacancyResponse>(response);
    }

    // Собирает тело запроса на создание вакансии.
    protected static VacancyRequest VacancyRequestFor(
        string[] required,
        string[]? niceToHave = null,
        int minYears = 0,
        int passingScore = 70,
        int borderlineWidth = 10,
        string? title = null) => new()
    {
        Title = title ?? Unique("Vacancy "),
        Direction = "QA",
        Description = "Тестовая вакансия",
        Requirements = new VacancyRequirements
        {
            RequiredSkills = required.Select(n => new Skill { Name = n }).ToList(),
            NiceToHaveSkills = (niceToHave ?? []).Select(n => new Skill { Name = n }).ToList(),
            MinYearsOfExperience = minYears,
            PassingScore = passingScore,
            BorderlineWidth = borderlineWidth,
        },
        FreezePeriodDays = 90,
    };

    // Создаёт кандидата с новым Telegram ID и возвращает его.
    protected async Task<CandidateResponse> CreateCandidateAsync(long? telegramId = null)
    {
        var id = telegramId ?? NewTelegramId();
        var response = await Client.PutAsJsonAsync(
            $"/candidates/{id}", new CandidateRequest { Name = "Тест Кандидат", Phone = "79991234567" }, Json);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<CandidateResponse>(response);
    }

    // Отправляет отклик multipart-формой и возвращает сырой ответ.
    protected Task<HttpResponseMessage> ApplyAsync(
        long telegramId, Guid vacancyId, byte[] file, int years = 5, string fileName = "resume.pdf") =>
        Client.PostAsync("/applications", ResumeForm(file, fileName, years, ("telegramId", telegramId.ToString()), ("vacancyId", vacancyId.ToString())));

    // Отправляет отклик и ожидает 201.
    protected async Task<ApplicationResponse> ApplyOkAsync(
        long telegramId, Guid vacancyId, byte[] file, int years = 5, string fileName = "resume.pdf")
    {
        var response = await ApplyAsync(telegramId, vacancyId, file, years, fileName);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<ApplicationResponse>(response);
    }

    // Запрашивает подбор вакансий по резюме и возвращает сырой ответ.
    protected Task<HttpResponseMessage> MatchesAsync(long telegramId, byte[] file, int years = 5, string fileName = "resume.pdf") =>
        Client.PostAsync($"/candidates/{telegramId}/matches", ResumeForm(file, fileName, years));

    // Собирает multipart-форму с файлом резюме и дополнительными полями.
    protected static MultipartFormDataContent ResumeForm(
        byte[] file, string fileName, int years, params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields)
            form.Add(new StringContent(value), name);
        form.Add(new StringContent(years.ToString()), "yearsOfExperience");
        form.Add(new ByteArrayContent(file), "file", fileName);
        return form;
    }

    // Отправляет решение рекрутера.
    protected Task<HttpResponseMessage> DecideAsync(Guid applicationId, string? decision, string? comment = null)
    {
        var body = decision is null ? new { comment } : (object)new { decision, comment };
        return Client.PostAsJsonAsync($"/applications/{applicationId}/decision", body, Json);
    }

    // Возвращает список уведомлений, которым пора уйти.
    protected async Task<List<DueNotificationResponse>> GetDueAsync()
    {
        var response = await Client.GetAsync("/internal/notifications/due");
        response.EnsureSuccessStatusCode();
        return await ReadAsync<List<DueNotificationResponse>>(response);
    }

    // Читает уведомления отклика прямо из БД, старые первыми.
    protected async Task<List<Notification>> GetNotificationsAsync(Guid applicationId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Notifications.AsNoTracking()
            .Where(n => n.ApplicationId == applicationId)
            .OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .ToListAsync();
    }

    // Сдвигает send_after уведомления в прошлое (по умолчанию на 5 минут), чтобы оно стало «наступившим».
    protected async Task MakeDueAsync(Guid notificationId, TimeSpan? ago = null)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var past = DateTime.UtcNow - (ago ?? TimeSpan.FromMinutes(5));
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE notifications SET send_after = {past} WHERE id = {notificationId}");
    }

    // Считает файлы резюме в хранилище этого класса тестов.
    protected int StoredFileCount()
    {
        var folder = Path.Combine(App.StoragePath, "resumes");
        return Directory.Exists(folder) ? Directory.GetFiles(folder).Length : 0;
    }

    // Считает отклики в БД.
    protected async Task<int> CountApplicationsAsync(Guid? vacancyId = null)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.JobApplications.CountAsync(a => vacancyId == null || a.VacancyId == vacancyId);
    }

    // Создаёт отклик, который прошёл скрининг и ждёт решения рекрутера.
    protected async Task<ApplicationResponse> CreateReviewApplicationAsync(string? vacancyTitle = null, long? telegramId = null)
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill], title: vacancyTitle);
        var candidate = await CreateCandidateAsync(telegramId);
        var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith(skill));
        Assert.Equal(ApplicationStatus.RecruiterReview, application.Status);
        return application;
    }

    // Создаёт отклик, отклонённый скринингом: автоотказ с ожидающим уведомлением.
    protected async Task<ApplicationResponse> CreateAutoRejectedApplicationAsync(string? vacancyTitle = null, long? telegramId = null)
    {
        // Два обязательных навыка: при нехватке одного отклик пограничный, а не автоотказ.
        var vacancy = await CreateVacancyAsync([Unique("s"), Unique("s")], title: vacancyTitle);
        var candidate = await CreateCandidateAsync(telegramId);
        var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, ResumeWith("nothing", "relevant"));
        Assert.Equal(ApplicationStatus.AutoRejected, application.Status);
        return application;
    }

    // Резюме, которое проходит вакансию с указанными навыками.
    protected static byte[] ResumeWith(params string[] skills) => ResumeFactory.Pdf(["Experienced engineer", .. skills]);
}
