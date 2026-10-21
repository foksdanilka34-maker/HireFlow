namespace HireFlow.Api.Security;

public class WebCorsOptions
{
    public const string SectionName = "Web";

    // Адреса кабинета рекрутера, которым разрешены запросы с cookie.
    public string[] Origins { get; set; } = [];
}
