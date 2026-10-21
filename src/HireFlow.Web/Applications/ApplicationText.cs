using HireFlow.Web.Api;

namespace HireFlow.Web.Applications;

// Русские подписи для откликов и доступные рекрутеру решения.
public static class ApplicationText
{
    // Русское название статуса отклика.
    public static string Status(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Received => "Новый",
        ApplicationStatus.RecruiterReview => "На проверке",
        ApplicationStatus.AutoRejected => "Автоотказ",
        ApplicationStatus.Invited => "Приглашён",
        ApplicationStatus.Rejected => "Отказ рекрутера",
        _ => status.ToString(),
    };

    // Русское название вердикта скрининга.
    public static string Verdict(ScreeningVerdict verdict) => verdict switch
    {
        ScreeningVerdict.Passed => "Прошёл",
        ScreeningVerdict.Borderline => "Пограничный",
        ScreeningVerdict.Failed => "Не прошёл",
        _ => verdict.ToString(),
    };

    // Кто изменил статус.
    public static string ChangedBy(StatusChangedBy changedBy) => changedBy switch
    {
        StatusChangedBy.System => "Система",
        StatusChangedBy.Ai => "ИИ",
        StatusChangedBy.Recruiter => "Рекрутер",
        _ => changedBy.ToString(),
    };

    // Подпись кнопки решения.
    public static string DecisionName(RecruiterDecision decision) => decision switch
    {
        RecruiterDecision.Invite => "Пригласить на созвон",
        RecruiterDecision.Reject => "Отказать",
        _ => decision.ToString(),
    };

    // Решения, которые API примет в этом статусе.
    public static IReadOnlyList<RecruiterDecision> Decisions(ApplicationStatus status) => status switch
    {
        ApplicationStatus.RecruiterReview => [RecruiterDecision.Invite, RecruiterDecision.Reject],
        ApplicationStatus.AutoRejected => [RecruiterDecision.Invite],
        _ => [],
    };

    // Приглашение после уже отправленного кандидату автоотказа требует подтверждения.
    public static bool NeedsInviteConfirmation(ApplicationDto application) =>
        application.Status == ApplicationStatus.AutoRejected && application.RejectionSent;
}
