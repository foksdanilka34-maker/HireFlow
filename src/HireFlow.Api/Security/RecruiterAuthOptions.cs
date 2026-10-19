namespace HireFlow.Api.Security;

public class RecruiterAuthOptions
{
    public const string SectionName = "RecruiterAuth";

    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);
    public int LoginAttemptsPerMinute { get; set; } = 5;
}
