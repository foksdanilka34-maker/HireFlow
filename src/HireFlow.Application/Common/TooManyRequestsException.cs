namespace HireFlow.Application.Common;

// Лимит превышен; RetryAfter — через сколько можно повторить (уходит в заголовок Retry-After).
public class TooManyRequestsException(string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
