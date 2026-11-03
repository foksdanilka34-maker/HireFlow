namespace HireFlow.Domain.Ai;

// Результат носит справочный характер; решение принимает рекрутер.
public record AiReviewResult(string Recommendation, int Score, string Explanation,
    string Model, string PromptVersion, DateTime ReviewedAt);
