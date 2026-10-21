namespace HireFlow.Web.Api;

// Результат вызова API без значения: HTTP-код и готовый текст ошибки для показа.
public record ApiResult(int Status, string? Error = null)
{
    public bool IsOk => Error is null;
}

// Результат вызова API со значением.
public record ApiResult<T>(int Status, T? Value = default, string? Error = null)
{
    public bool IsOk => Error is null;
}
