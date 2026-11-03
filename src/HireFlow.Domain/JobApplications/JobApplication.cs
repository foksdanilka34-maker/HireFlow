using HireFlow.Domain.Notifications;
using HireFlow.Domain.Ai;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Domain.JobApplications;

public class JobApplication
{
    public const int FileKeyMaxLength = 200;
    public const int FileNameMaxLength = 255;
    public const int FileHashLength = 64;

    private readonly List<ApplicationStatusChange> _history = [];
    private readonly List<Notification> _notifications = [];
    private readonly List<ApplicationQuestion> _questions = [];

    public Guid Id { get; private set; }
    public Guid VacancyId { get; private set; }
    public Guid CandidateId { get; private set; }
    public int YearsOfExperience { get; private set; }
    public string ResumeFileKey { get; private set; } = null!;
    public string ResumeFileName { get; private set; } = null!;
    public string ResumeFileHash { get; private set; } = null!;
    public string ResumeText { get; private set; } = null!;
    public ApplicationStatus Status { get; private set; }
    public ScreeningResult Screening { get; private set; } = null!;
    public AiReviewResult? AiReview { get; private set; }
    public bool AiReviewUnavailable { get; private set; }
    public AiReviewResult? AnswersReview { get; private set; }
    public bool AnswersReviewUnavailable { get; private set; }
    public DateTime? QuestionsDeadline { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyList<ApplicationStatusChange> History => _history;
    public IReadOnlyList<Notification> Notifications => _notifications;
    public IReadOnlyList<ApplicationQuestion> Questions => _questions;

    // Нужен EF Core для создания объекта при чтении из БД.
    private JobApplication() { }

    // Создаёт отклик в статусе «принят» и первую запись истории.
    public JobApplication(
        Guid vacancyId,
        Guid candidateId,
        int yearsOfExperience,
        string resumeFileKey,
        string resumeFileName,
        string resumeFileHash,
        string resumeText,
        IReadOnlyList<VacancyQuestion>? questions = null)
    {
        VacancyId = vacancyId;
        CandidateId = candidateId;
        YearsOfExperience = yearsOfExperience;
        ResumeFileKey = resumeFileKey;
        ResumeFileName = resumeFileName.Length > FileNameMaxLength ? resumeFileName[..FileNameMaxLength] : resumeFileName;
        ResumeFileHash = resumeFileHash;
        ResumeText = resumeText;
        Status = ApplicationStatus.Received;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;

        _history.Add(new ApplicationStatusChange(null, ApplicationStatus.Received, StatusChangedBy.System, reason: null));
        if (questions is not null)
            for (var i = 0; i < questions.Count; i++) _questions.Add(new ApplicationQuestion(questions[i], i));
    }

    // Сохраняет результат скрининга: прошёл или пограничный — к рекрутеру, не прошёл — автоотказ с уведомлением.
    public void ApplyScreening(ScreeningResult result, TimeSpan rejectionDelay, bool aiEnabled = false, TimeSpan? questionWindow = null)
    {
        if (Status != ApplicationStatus.Received)
            throw new DomainException("Screening can only be applied to a received application.");

        Screening = result;
        var reason = $"Screening {result.Verdict.ToString().ToLowerInvariant()}, score {result.Score}.";

        if (result.Verdict == ScreeningVerdict.Failed)
        {
            ChangeStatus(ApplicationStatus.AutoRejected, StatusChangedBy.System, reason);
            Notify(NotificationType.Rejected, rejectionDelay);
        }
        else if (aiEnabled)
        {
            ChangeStatus(ApplicationStatus.AiReview, StatusChangedBy.System, reason);
        }
        else if (result.Verdict == ScreeningVerdict.Passed && _questions.Count > 0)
        {
            StartQuestions(questionWindow ?? TimeSpan.FromDays(7), StatusChangedBy.System);
        }
        else
        {
            ChangeStatus(ApplicationStatus.RecruiterReview, StatusChangedBy.System, reason);
        }
    }

    // Завершает фоновую проверку и передаёт отклик рекрутеру без автоматического отказа.
    public void CompleteAiReview(AiReviewResult? result, TimeSpan? questionWindow = null)
    {
        if (Status != ApplicationStatus.AiReview)
            throw new DomainException("Application is not awaiting AI review.");
        AiReview = result;
        AiReviewUnavailable = result is null;
        if (result?.Recommendation == "pass" && _questions.Count > 0)
            StartQuestions(questionWindow ?? TimeSpan.FromDays(7), StatusChangedBy.Ai);
        else
            ChangeStatus(ApplicationStatus.RecruiterReview, result is null ? StatusChangedBy.System : StatusChangedBy.Ai,
                result is null ? "AI review unavailable." : "AI review completed.");
    }

    // Начинает опрос и создаёт уведомление с первым вопросом.
    private void StartQuestions(TimeSpan window, StatusChangedBy by)
    {
        QuestionsDeadline = DateTime.UtcNow.Add(window);
        ChangeStatus(ApplicationStatus.AwaitingQuestions, by, "Awaiting candidate answers.");
        Notify(NotificationType.QuestionsReady, TimeSpan.Zero);
    }

    // Записывает очередной ответ, а после последнего передаёт ответы на проверку.
    public bool AnswerNextQuestion(string answer, bool aiEnabled)
    {
        if (Status != ApplicationStatus.AwaitingQuestions) throw new DomainException("Application is not awaiting answers.");
        if (QuestionsDeadline <= DateTime.UtcNow) throw new DomainException("Answer deadline has expired.");
        var next = _questions.OrderBy(q => q.Position).FirstOrDefault(q => q.Answer is null)
            ?? throw new DomainException("No unanswered question.");
        next.SetAnswer(answer);
        UpdatedAt = DateTime.UtcNow;
        if (_questions.Any(q => q.Answer is null)) return false;
        CancelPendingNotifications();
        if (aiEnabled) ChangeStatus(ApplicationStatus.AiAnswersReview, StatusChangedBy.System, "Answers submitted for AI review.");
        else
        {
            AnswersReviewUnavailable = true;
            ChangeStatus(ApplicationStatus.RecruiterReview, StatusChangedBy.System, "Answers submitted; AI review unavailable.");
        }
        return aiEnabled;
    }

    public void CompleteAnswersReview(AiReviewResult? result)
    {
        if (Status != ApplicationStatus.AiAnswersReview) throw new DomainException("Application is not awaiting answer review.");
        AnswersReview = result;
        AnswersReviewUnavailable = result is null;
        ChangeStatus(ApplicationStatus.RecruiterReview, result is null ? StatusChangedBy.System : StatusChangedBy.Ai,
            result is null ? "AI answer review unavailable." : "AI answer review completed.");
    }

    public void ExpireQuestions()
    {
        if (Status != ApplicationStatus.AwaitingQuestions || QuestionsDeadline > DateTime.UtcNow)
            throw new DomainException("Questions are not expired.");
        CancelPendingNotifications();
        ChangeStatus(ApplicationStatus.Expired, StatusChangedBy.System, "Candidate answer deadline expired.");
    }

    // Приглашает на созвон: из очереди рекрутера или из автоотказа, если рекрутер не согласен с системой.
    public void Invite(Guid recruiterId, string? comment)
    {
        if (Status is not (ApplicationStatus.RecruiterReview or ApplicationStatus.AutoRejected))
            throw new DomainException("Only an application under review or auto-rejected can be invited.");

        CancelPendingNotifications();
        ChangeStatus(ApplicationStatus.Invited, StatusChangedBy.Recruiter, comment, recruiterId);
        Notify(NotificationType.Invited, TimeSpan.Zero);
    }

    // Отказывает по решению рекрутера; уведомление уйдёт кандидату с задержкой.
    public void Reject(Guid recruiterId, string? comment, TimeSpan rejectionDelay)
    {
        if (Status != ApplicationStatus.RecruiterReview)
            throw new DomainException("Only an application under review can be rejected.");

        ChangeStatus(ApplicationStatus.Rejected, StatusChangedBy.Recruiter, comment, recruiterId);
        Notify(NotificationType.Rejected, rejectionDelay);
    }

    // Меняет статус и записывает переход в историю (с рекрутером, если решение его).
    private void ChangeStatus(ApplicationStatus newStatus, StatusChangedBy changedBy, string? reason, Guid? recruiterId = null)
    {
        _history.Add(new ApplicationStatusChange(Status, newStatus, changedBy, reason, recruiterId));

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    // Ставит уведомление кандидату в очередь: отправить не раньше чем через delay.
    private void Notify(NotificationType type, TimeSpan delay)
    {
        _notifications.Add(new Notification(type, DateTime.UtcNow + delay));
    }

    // Отменяет уведомления, которые ещё не отправлены.
    private void CancelPendingNotifications()
    {
        foreach (var notification in _notifications)
            notification.Cancel();
    }
}
