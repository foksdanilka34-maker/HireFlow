namespace HireFlow.Application.Ai;

public class AiReviewOptions
{
    public const string SectionName = "AiReview";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:18081";
    public string Model { get; set; } = "gemma-4-e4b";
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 10;
    public int PollIntervalSeconds { get; set; } = 3;
}
