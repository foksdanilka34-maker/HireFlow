namespace HireFlow.Web.Auth;

public static class ReturnUrl
{
    // Возвращает адрес возврата, если это безопасный локальный путь, иначе /vacancies.
    public static string Safe(string? url) =>
        url is not null
        && url.StartsWith('/')
        && !url.StartsWith("//")
        && !url.StartsWith("/\\")
        && !url.Any(char.IsControl)
        && !url.StartsWith("/login")
            ? url
            : "/vacancies";
}
