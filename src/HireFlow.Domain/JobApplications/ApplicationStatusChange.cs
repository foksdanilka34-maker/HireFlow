namespace HireFlow.Domain.JobApplications;

public class ApplicationStatusChange
{
    public const int ReasonMaxLength = 1000;

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public ApplicationStatus? FromStatus { get; private set; }
    public ApplicationStatus ToStatus { get; private set; }
    public StatusChangedBy ChangedBy { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ChangedAt { get; private set; }

    // Нужен EF Core для создания объекта при чтении из БД.
    private ApplicationStatusChange() { }

    // Создаёт запись истории. Вызывается только из JobApplication.
    internal ApplicationStatusChange(ApplicationStatus? from, ApplicationStatus to, StatusChangedBy changedBy, string? reason)
    {
        FromStatus = from;
        ToStatus = to;
        ChangedBy = changedBy;
        Reason = reason;
        ChangedAt = DateTime.UtcNow;
    }
}
