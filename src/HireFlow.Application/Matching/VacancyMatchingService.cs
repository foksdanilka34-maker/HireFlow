using HireFlow.Application.Common;
using HireFlow.Application.Resumes;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Application.Matching;

public class VacancyMatchingService(IAppDbContext db, IResumeReader resumeReader) : IVacancyMatchingService
{
    public async Task<List<VacancyMatchResponse>> FindMatchesAsync(FindMatchesRequest request, CancellationToken ct)
    {
        var resume = resumeReader.Read(request.FileContent);
        var appliedVacancyIds = await GetAppliedVacancyIdsAsync(request.TelegramId, ct);

        var vacancies = await db.Vacancies
            .AsNoTracking()
            .Where(v => v.Status == VacancyStatus.Open && !appliedVacancyIds.Contains(v.Id))
            .ToListAsync(ct);

        return vacancies
            .Select(v => new { Vacancy = v, Result = ResumeScorer.Evaluate(resume.Text, request.YearsOfExperience, v.Requirements) })
            .Where(x => x.Result.Verdict == ScreeningVerdict.Passed)
            .OrderByDescending(x => x.Result.Score)
            .Select(x => VacancyMatchResponse.From(x.Vacancy))
            .ToList();
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
