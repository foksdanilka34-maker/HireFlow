using System.ComponentModel.DataAnnotations;
using HireFlow.Application.Common;
using HireFlow.Application.Resumes;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;
using Microsoft.Extensions.Options;

namespace HireFlow.Application.PublicApi;

public class PublicScreeningService(
    IApiUsageCounter usageCounter,
    IResumeReader resumeReader,
    IOptions<PublicApiOptions> options) : IPublicScreeningService
{
    public const int MaxSkills = 50;
    public const int MaxSynonyms = 20;
    public const int MaxSynonymLength = 100;
    public const int MaxTextLength = 100_000;
    public const int MaxYearsOfExperience = 50;

    public async Task ChargeQuotaAsync(Guid clientId, int dailyQuota, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!await usageCounter.TryIncrementAsync(clientId, DateOnly.FromDateTime(now), dailyQuota, ct))
            throw new TooManyRequestsException("Daily quota exceeded.", NextMidnight(now) - now);
    }

    public async Task<PublicScreeningResponse> ScreenAsync(PublicScreeningRequest request, CancellationToken ct)
    {
        var requirements = ValidateRequirements(request.Requirements);
        if ((request.File is null) == (request.Text is null))
            throw new BadRequestException("Send either a file or a text.");
        if (request.YearsOfExperience is < 0 or > MaxYearsOfExperience)
            throw new BadRequestException($"yearsOfExperience must be from 0 to {MaxYearsOfExperience}.");

        var text = request.File is null ? request.Text! : await ReadFileAsync(request.File, ct);
        if (string.IsNullOrWhiteSpace(text))
            throw new BadRequestException("The resume text is empty.");
        if (text.Length > MaxTextLength)
            throw new BadRequestException($"The resume text must not exceed {MaxTextLength} characters.");

        var years = request.YearsOfExperience ?? ResumeFacts.FindYearsOfExperience(text);
        var source = request.YearsOfExperience is not null ? YearsOfExperienceSource.Request
            : years is not null ? YearsOfExperienceSource.Resume
            : YearsOfExperienceSource.None;

        var result = ResumeScorer.Evaluate(text, years ?? 0, requirements);
        return new PublicScreeningResponse(
            result.Score,
            result.Verdict,
            Matched(requirements.RequiredSkills, result.MissingRequiredSkills),
            result.MissingRequiredSkills,
            Matched(requirements.NiceToHaveSkills, result.MissingNiceToHaveSkills),
            result.MissingNiceToHaveSkills,
            years,
            source,
            result.ExperienceIsEnough);
    }

    public async Task<PublicUsageResponse> GetUsageAsync(Guid clientId, int dailyQuota, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var count = await usageCounter.GetAsync(clientId, DateOnly.FromDateTime(now), ct);
        return new PublicUsageResponse(count, dailyQuota, NextMidnight(now));
    }

    // Достаёт текст из файла с ограничением по времени. Зависшее извлечение после таймаута дорабатывает
    // в фоне — Task.Run не прервать; это принимаем, клиенту сразу уходит 400.
    private async Task<string> ReadFileAsync(byte[] file, CancellationToken ct)
    {
        try
        {
            var content = await Task.Run(() => resumeReader.Read(file), ct)
                .WaitAsync(options.Value.ExtractionTimeout, ct);
            return content.Text;
        }
        catch (TimeoutException)
        {
            throw new BadRequestException("Could not read the file in time. Please send another PDF or DOCX.");
        }
    }

    // Проверяет требования по атрибутам VacancyRequirements и каждого навыка (вложенные списки валидатор
    // сам не обходит) и по ограничениям размера публичного API.
    private static VacancyRequirements ValidateRequirements(VacancyRequirements? requirements)
    {
        if (requirements is null)
            throw new BadRequestException("requirements are required.");

        ValidateObject(requirements);
        if (requirements.RequiredSkills.Count + requirements.NiceToHaveSkills.Count > MaxSkills)
            throw new BadRequestException($"requirements must not contain more than {MaxSkills} skills.");

        foreach (var skill in requirements.RequiredSkills.Concat(requirements.NiceToHaveSkills))
        {
            if (skill is null)
                throw new BadRequestException("Invalid requirements: a skill is empty.");
            ValidateObject(skill);
            if (skill.Synonyms.Count > MaxSynonyms)
                throw new BadRequestException($"A skill must not have more than {MaxSynonyms} synonyms.");
            if (skill.Synonyms.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > MaxSynonymLength))
                throw new BadRequestException($"A synonym must be non-empty and not exceed {MaxSynonymLength} characters.");
        }

        return requirements;
    }

    // Проверяет атрибуты DataAnnotations объекта и IValidatableObject; первая ошибка — BadRequestException.
    private static void ValidateObject(object value)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true))
            throw new BadRequestException($"Invalid requirements: {results[0].ErrorMessage}");
    }

    // Найденные навыки: навыки требований без ненайденных, в порядке требований.
    private static List<string> Matched(List<Skill> skills, List<string> missing) =>
        skills.Select(s => s.Name).Where(name => !missing.Contains(name)).ToList();

    // Следующая полночь UTC — когда сбрасывается дневная квота.
    private static DateTime NextMidnight(DateTime now) => now.Date.AddDays(1);
}
