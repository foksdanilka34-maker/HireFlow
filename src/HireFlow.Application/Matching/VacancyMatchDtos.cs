using HireFlow.Domain.Vacancies;

namespace HireFlow.Application.Matching;

public record FindMatchesRequest(long TelegramId, int? YearsOfExperience, byte[] FileContent);

public record FindMatchesResponse(
    int? YearsOfExperience,
    bool ExperienceRequired,
    bool PhoneFound,
    List<VacancyMatchResponse> Vacancies);

public record VacancyMatchResponse(Guid Id, string Title, string Direction, string? Description)
{
    // Собирает ответ API из вакансии: только то, что бот покажет кандидату.
    public static VacancyMatchResponse From(Vacancy v) => new(v.Id, v.Title, v.Direction, v.Description);
}
