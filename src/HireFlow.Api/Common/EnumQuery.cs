namespace HireFlow.Api.Common;

public static class EnumQuery
{
    // Разбирает необязательный фильтр из строки запроса: 'recruiter_review' → RecruiterReview, пусто → без фильтра.
    public static bool TryParse<TEnum>(string? value, out TEnum? result) where TEnum : struct, Enum
    {
        result = null;
        if (string.IsNullOrEmpty(value))
            return true;

        if (!Enum.TryParse<TEnum>(value.Replace("_", ""), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return false;

        result = parsed;
        return true;
    }
}
