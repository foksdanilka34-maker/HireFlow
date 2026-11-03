using System.ComponentModel.DataAnnotations;
using HireFlow.Application.Ai;
using HireFlow.Application.Common;
using HireFlow.Domain.Ai;
using HireFlow.Domain.JobApplications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HireFlow.Application.JobApplications;

public record PendingQuestionResponse(Guid ApplicationId, string VacancyTitle, int Position, int Total, string Question, DateTime Deadline);
public record AnswerQuestionRequest([property: Required, MaxLength(4000)] string Answer, int Position);
public record AnswerQuestionResponse(bool Completed, PendingQuestionResponse? Next);

public class QuestionnaireService(IAppDbContext db, IOptions<AiReviewOptions> aiOptions)
{
    // Возвращает старейший незавершённый опрос кандидата; параллельные опросы идут по очереди.
    public async Task<PendingQuestionResponse?> GetPendingAsync(long telegramId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = from a in db.JobApplications.Include(a => a.Questions)
                    join c in db.Candidates on a.CandidateId equals c.Id
                    join v in db.Vacancies on a.VacancyId equals v.Id
                    where c.TelegramId == telegramId && a.Status == ApplicationStatus.AwaitingQuestions && a.QuestionsDeadline > now
                    orderby a.CreatedAt, a.Id
                    select new { Application = a, Title = v.Title };
        var item = await query.FirstOrDefaultAsync(ct);
        return item is null ? null : ToPending(item.Application, item.Title);
    }

    // Сверяет номер вопроса и принадлежность отклика кандидату до записи ответа.
    public async Task<AnswerQuestionResponse> AnswerAsync(long telegramId, Guid applicationId, AnswerQuestionRequest request, CancellationToken ct)
    {
        var pending = await GetPendingAsync(telegramId, ct);
        if (pending is null || pending.ApplicationId != applicationId || pending.Position != request.Position)
            throw new ConflictException("Question has changed or expired.");
        var application = await db.JobApplications.Include(a => a.Questions).Include(a => a.History).Include(a => a.Notifications)
            .SingleAsync(a => a.Id == applicationId, ct);
        var requiresReview = application.AnswerNextQuestion(request.Answer, aiOptions.Value.Enabled);
        if (requiresReview) db.AiReviewJobs.Add(new AiReviewJob(application, null, AiReviewJobKind.Answers));
        await db.SaveChangesAsync(ct);
        var next = application.Status == ApplicationStatus.AwaitingQuestions ? ToPending(application, pending.VacancyTitle) : null;
        return new AnswerQuestionResponse(next is null, next);
    }

    // Переводит просроченные опросы в финальный статус без автоматического отказа.
    public async Task ExpireOverdueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = await db.JobApplications.Include(a => a.History).Include(a => a.Notifications)
            .Where(a => a.Status == ApplicationStatus.AwaitingQuestions && a.QuestionsDeadline <= now).Take(50).ToListAsync(ct);
        foreach (var application in expired) application.ExpireQuestions();
        if (expired.Count > 0) await db.SaveChangesAsync(ct);
    }

    private static PendingQuestionResponse ToPending(JobApplication a, string title)
    {
        var q = a.Questions.OrderBy(q => q.Position).First(q => q.Answer is null);
        return new PendingQuestionResponse(a.Id, title, q.Position, a.Questions.Count, q.Text, a.QuestionsDeadline!.Value);
    }
}
