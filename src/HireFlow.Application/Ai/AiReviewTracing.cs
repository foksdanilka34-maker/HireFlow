using System.Diagnostics;

namespace HireFlow.Application.Ai;

public static class AiReviewTracing
{
    public const string SourceName = "HireFlow.Ai";
    public static readonly ActivitySource Source = new(SourceName);
}
