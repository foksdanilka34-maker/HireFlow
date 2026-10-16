using HireFlow.Domain.Notifications;
using HireFlow.Domain.Screening;

namespace HireFlow.Domain.JobApplications;

public class JobApplication
{
    public const int FileKeyMaxLength = 200;
    public const int FileNameMaxLength = 255;
    public const int FileHashLength = 64;

    private readonly List<ApplicationStatusChange> _history = [];
    private readonly List<Notification> _notifications = [];

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
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyList<ApplicationStatusChange> History => _history;
    public IReadOnlyList<Notification> Notifications => _notifications;

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
        string resumeText)
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
    }

    // Сохраняет результат скрининга: прошёл или пограничный — к рекрутеру, не прошёл — автоотказ с уведомлением.
    public void ApplyScreening(ScreeningResult result, TimeSpan rejectionDelay)
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
        else
        {
            ChangeStatus(ApplicationStatus.RecruiterReview, StatusChangedBy.System, reason);
        }
    }

    // Приглашает на созвон: из очереди рекрутера или из автоотказа, если рекрутер не согласен с системой.
    public void Invite(string? comment)
    {
        if (Status is not (ApplicationStatus.RecruiterReview or ApplicationStatus.AutoRejected))
            throw new DomainException("Only an application under review or auto-rejected can be invited.");

        CancelPendingNotifications();
        ChangeStatus(ApplicationStatus.Invited, StatusChangedBy.Recruiter, comment);
        Notify(NotificationType.Invited, TimeSpan.Zero);
    }

    // Отказывает по решению рекрутера; уведомление уйдёт кандидату с задержкой.
    public void Reject(string? comment, TimeSpan rejectionDelay)
    {
        if (Status != ApplicationStatus.RecruiterReview)
            throw new DomainException("Only an application under review can be rejected.");

        ChangeStatus(ApplicationStatus.Rejected, StatusChangedBy.Recruiter, comment);
        Notify(NotificationType.Rejected, rejectionDelay);
    }

    // Меняет статус и записывает переход в историю.
    private void ChangeStatus(ApplicationStatus newStatus, StatusChangedBy changedBy, string? reason)
    {
        _history.Add(new ApplicationStatusChange(Status, newStatus, changedBy, reason));

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
