using HireFlow.Application.Ai;
using HireFlow.Application.Common;
using System.Diagnostics;
using System.Text.Json;
using HireFlow.Domain.Vacancies;
using HireFlow.Domain.Ai;
using HireFlow.Application.JobApplications;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireFlow.Infrastructure.Ai;

public class AiReviewProcessor(AppDbContext db, IAiReviewer reviewer, IOptions<AiReviewOptions> options, IOptions<QuestionOptions> questionOptions, ILogger<AiReviewProcessor> logger, HireFlowMetrics metrics)
{
    // Блокировка строки до завершения запроса исключает повторную обработку несколькими экземплярами API.
    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var jobs = await db.AiReviewJobs.FromSqlRaw("SELECT * FROM ai_review_jobs WHERE status = 'pending' AND available_at <= now() ORDER BY available_at, id LIMIT 1 FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);
        var job = jobs.SingleOrDefault();
        if (job is null) return false;
        using var activity = AiReviewTracing.Source.StartActivity("ai.review");
        activity?.SetTag("ai.review.kind", job.Kind.ToString().ToLowerInvariant());
        var application = await db.JobApplications.Include(a => a.History).Include(a => a.Questions).Include(a => a.Notifications).SingleAsync(a => a.Id == job.ApplicationId, ct);
        if (!options.Value.Enabled)
        {
            job.FailAttempt("Disabled", 1, TimeSpan.Zero);
            if (job.Kind == AiReviewJobKind.Resume) application.CompleteAiReview(null);
            else application.CompleteAnswersReview(null);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        var started = Stopwatch.GetTimestamp();
        try
        {
            var candidate = await db.Candidates.AsNoTracking().SingleAsync(c => c.Id == application.CandidateId, ct);
            if (job.Kind == AiReviewJobKind.Resume)
            {
                var requirements = job.RequirementsSnapshot is null ? null : JsonSerializer.Deserialize<VacancyRequirements>(job.RequirementsSnapshot);
                if (requirements is null) throw new InvalidDataException("AI review requirements snapshot is missing.");
                var result = await reviewer.ReviewAsync(application, requirements, candidate.Name, candidate.Phone, ct);
                application.CompleteAiReview(result, TimeSpan.FromDays(Math.Clamp(questionOptions.Value.ResponseDays, 1, 365)));
            }
            else
            {
                var result = await reviewer.ReviewAnswersAsync(application, candidate.Name, candidate.Phone, ct);
                application.CompleteAnswersReview(result);
            }
            job.Complete();
            metrics.AiReviewAttempt("success", Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var settings = options.Value;
            var delay = TimeSpan.FromSeconds(Math.Clamp(settings.RetryDelaySeconds, 1, 3600) * Math.Pow(2, Math.Min(job.Attempts, 6)));
            job.FailAttempt(ex.GetType().Name, Math.Clamp(settings.MaxAttempts, 1, 10), delay);
            metrics.AiReviewAttempt("failure", Stopwatch.GetElapsedTime(started));
            logger.LogWarning("AI review attempt failed: {ErrorType}; attempt {Attempt}", ex.GetType().Name, job.Attempts);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            if (job.Status == AiReviewJobStatus.Failed)
            {
                if (job.Kind == AiReviewJobKind.Resume) application.CompleteAiReview(null);
                else application.CompleteAnswersReview(null);
            }
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
