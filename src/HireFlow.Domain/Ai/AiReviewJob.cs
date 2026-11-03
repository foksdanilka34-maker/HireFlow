using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Vacancies;
using System.Text.Json;

namespace HireFlow.Domain.Ai;

public enum AiReviewJobStatus { Pending, Completed, Failed }
public enum AiReviewJobKind { Resume, Answers }

// Задача проверки создаётся в той же транзакции, что и отклик.
public class AiReviewJob
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public JobApplication Application { get; private set; } = null!;
    public AiReviewJobStatus Status { get; private set; }
    public AiReviewJobKind Kind { get; private set; }
    public int Attempts { get; private set; }
    public DateTime AvailableAt { get; private set; }
    public string? LastError { get; private set; }
    public string? RequirementsSnapshot { get; private set; }

    private AiReviewJob() { }

    public AiReviewJob(JobApplication application, VacancyRequirements? requirements, AiReviewJobKind kind = AiReviewJobKind.Resume)
    {
        Id = Guid.CreateVersion7();
        Application = application;
        Kind = kind;
        Status = AiReviewJobStatus.Pending;
        AvailableAt = DateTime.UtcNow;
        RequirementsSnapshot = requirements is null ? null : JsonSerializer.Serialize(requirements);
    }

    public void Complete() { Status = AiReviewJobStatus.Completed; Attempts++; LastError = null; }

    public void FailAttempt(string error, int maxAttempts, TimeSpan retryDelay)
    {
        Attempts++;
        LastError = error.Length > 300 ? error[..300] : error;
        if (Attempts >= maxAttempts) Status = AiReviewJobStatus.Failed;
        else AvailableAt = DateTime.UtcNow.Add(retryDelay);
    }
}
