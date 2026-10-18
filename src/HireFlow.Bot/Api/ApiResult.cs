namespace HireFlow.Bot.Api;

public enum ApiStatus
{
    Ok,
    BadRequest,
    NotFound,
    Conflict,
    Error,
}

public record ApiResult<T>(ApiStatus Status, T? Value = default)
{
    public bool IsOk => Status == ApiStatus.Ok;

    // Успешный ответ со значением.
    public static ApiResult<T> Ok(T value) => new(ApiStatus.Ok, value);

    // Ответ без значения: ошибка клиента, «не найдено», конфликт или сбой.
    public static ApiResult<T> Failure(ApiStatus status) => new(status);
}
