using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Domain.Tests;

internal static class TestData
{
    // Собирает навык с синонимами.
    public static Skill Skill(string name, params string[] synonyms) =>
        new() { Name = name, Synonyms = [.. synonyms] };

    // Собирает требования вакансии из названий навыков.
    public static VacancyRequirements Requirements(
        string[] required,
        string[]? niceToHave = null,
        int minYears = 0,
        int passingScore = 70,
        int borderlineWidth = 10) => new()
    {
        RequiredSkills = required.Select(n => Skill(n)).ToList(),
        NiceToHaveSkills = (niceToHave ?? []).Select(n => Skill(n)).ToList(),
        MinYearsOfExperience = minYears,
        PassingScore = passingScore,
        BorderlineWidth = borderlineWidth,
    };

    // Создаёт отклик в статусе «принят».
    public static JobApplication NewApplication(string fileName = "resume.pdf") =>
        new(Guid.NewGuid(), Guid.NewGuid(), 3, "resumes/key.pdf", fileName, new string('a', 64), "resume text");

    // Создаёт результат скрининга с нужным вердиктом.
    public static ScreeningResult ScreeningOf(ScreeningVerdict verdict, int score = 50) =>
        new(score, verdict, [], [], true);

    // Создаёт отклик, прошедший скрининг: статус «рассмотрение рекрутером».
    public static JobApplication ReviewApplication()
    {
        var application = NewApplication();
        application.ApplyScreening(ScreeningOf(ScreeningVerdict.Passed, 90), TimeSpan.FromHours(4));
        return application;
    }

    // Создаёт отклик в автоотказе с ожидающим уведомлением об отказе.
    public static JobApplication AutoRejectedApplication(TimeSpan? delay = null)
    {
        var application = NewApplication();
        application.ApplyScreening(ScreeningOf(ScreeningVerdict.Failed, 10), delay ?? TimeSpan.FromHours(4));
        return application;
    }
}
