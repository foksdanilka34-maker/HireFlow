namespace HireFlow.Api.Security;

public class BotApiKeyOptions
{
    public const string SectionName = "BotApi";
    public const string HeaderName = "X-Api-Key";

    public string ApiKey { get; set; } = "";
}
