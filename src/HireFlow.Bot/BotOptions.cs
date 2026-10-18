namespace HireFlow.Bot;

public class BotOptions
{
    public const string SectionName = "Bot";

    public string TelegramToken { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public TimeSpan NotificationPollInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int PageSize { get; set; } = 3;
}
