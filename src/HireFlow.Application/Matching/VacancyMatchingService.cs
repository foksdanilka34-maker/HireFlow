using HireFlow.Application.Common;
using HireFlow.Application.Resumes;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Application.Matching;

public class VacancyMatchingService(IAppDbContext db, IResumeReader resumeReader) : IVacancyMatchingService
{
    public async Task<FindMatchesResponse> FindMatchesAsync(FindMatchesRequest request, CancellationToken ct)
    {
        var resume = resumeReader.Read(request.FileContent);
        var years = request.YearsOfExperience ?? ResumeFacts.FindYearsOfExperience(resume.Text);
        var phoneFound = ResumeFacts.FindPhone(resume.Text) is not null;
        if (years is null)
            return new FindMatchesResponse(null, ExperienceRequired: true, phoneFound, []);

        var appliedVacancyIds = await GetAppliedVacancyIdsAsync(request.TelegramId, ct);

        var vacancies = await db.Vacancies
            .AsNoTracking()
            .Where(v => v.Status == VacancyStatus.Open && !appliedVacancyIds.Contains(v.Id))
            .ToListAsync(ct);

        var matches = vacancies
            .Select(v => new { Vacancy = v, Result = ResumeScorer.Evaluate(resume.Text, years.Value, v.Requirements) })
            .Where(x => x.Result.Verdict == ScreeningVerdict.Passed)
            .OrderByDescending(x => x.Result.Score)
            .Select(x => VacancyMatchResponse.From(x.Vacancy))
            .ToList();

        return new FindMatchesResponse(years, ExperienceRequired: false, phoneFound, matches);
    }

    // Возвращает id вакансий, на которые кандидат уже откликался, — их в подборе не показываем.
    private async Task<List<Guid>> GetAppliedVacancyIdsAsync(long telegramId, CancellationToken ct)
    {
        return await db.JobApplications
            .Where(a => db.Candidates.Any(c => c.Id == a.CandidateId && c.TelegramId == telegramId))
            .Select(a => a.VacancyId)
            .Distinct()
            .ToListAsync(ct);
    }
}
