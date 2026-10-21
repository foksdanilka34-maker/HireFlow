using System.Security.Cryptography;
using HireFlow.Application.Common;
using HireFlow.Application.Resumes;
using HireFlow.Domain.Resumes;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HireFlow.Application.Matching;

public class VacancyMatchingService(
    IAppDbContext db,
    IResumeReader resumeReader,
    IOptions<ResumeUploadOptions> uploadOptions,
    HireFlowMetrics metrics) : IVacancyMatchingService
{
    public async Task<FindMatchesResponse> FindMatchesAsync(FindMatchesRequest request, CancellationToken ct)
    {
        var resume = resumeReader.Read(request.FileContent);
        await RegisterUploadAsync(request, ct);
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

    // Учитывает файл в лимите: новый файл сверх DailyLimit разных файлов за окно — 429, иначе запись или обновление времени.
    private async Task RegisterUploadAsync(FindMatchesRequest request, CancellationToken ct)
    {
        var options = uploadOptions.Value;
        var now = DateTime.UtcNow;
        var windowStart = now - options.Window;
        var hash = Convert.ToHexStringLower(SHA256.HashData(request.FileContent));
        var uploads = await db.ResumeUploads.Where(u => u.TelegramId == request.TelegramId).ToListAsync(ct);

        var existing = uploads.FirstOrDefault(u => u.FileHash == hash);
        var isNewInWindow = existing is null || existing.UploadedAt <= windowStart;
        if (isNewInWindow && uploads.Count(u => u.UploadedAt > windowStart) >= options.DailyLimit)
        {
            metrics.ResumeUploadLimited();
            throw new TooManyRequestsException("Daily resume upload limit reached.");
        }

        db.ResumeUploads.RemoveRange(uploads.Where(u => u.UploadedAt <= windowStart && u != existing));
        if (existing is null)
            db.ResumeUploads.Add(new ResumeUpload(request.TelegramId, hash, now));
        else if (existing.UploadedAt <= windowStart)
            existing.Touch(now);

        await db.SaveChangesAsync(ct);
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
