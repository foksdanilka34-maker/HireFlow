namespace HireFlow.Application.Resumes;

public class ResumeUploadOptions
{
    public const string SectionName = "ResumeUploads";

    public int DailyLimit { get; init; } = 3;
    public TimeSpan Window { get; init; } = TimeSpan.FromHours(24);
}
