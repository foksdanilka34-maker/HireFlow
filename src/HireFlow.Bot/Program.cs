using HireFlow.Bot;
using HireFlow.Bot.Api;
using HireFlow.Bot.Dialog;
using HireFlow.Bot.Telegram;
using global::Telegram.Bot;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.TelegramToken),
        "Bot:TelegramToken is not set (use user secrets or env Bot__TelegramToken).")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
        "Bot:ApiKey is not set (use user secrets or env Bot__ApiKey).")
    .Validate(o => Uri.TryCreate(o.ApiBaseUrl, UriKind.Absolute, out _),
        "Bot:ApiBaseUrl is not set or is not an absolute URL.")
    .Validate(o => o.NotificationPollInterval > TimeSpan.Zero, "Bot:NotificationPollInterval must be positive.")
    .Validate(o => o.PageSize > 0, "Bot:PageSize must be positive.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IHireFlowApiClient, HireFlowApiClient>((sp, http) =>
{
    var botOptions = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    http.BaseAddress = new Uri(botOptions.ApiBaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(30);
    http.DefaultRequestHeaders.Add("X-Api-Key", botOptions.ApiKey);
});

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
    new TelegramBotClient(sp.GetRequiredService<IOptions<BotOptions>>().Value.TelegramToken));
builder.Services.AddSingleton<IChatGateway, TelegramChatGateway>();
builder.Services.AddSingleton<DialogHandler>();

builder.Services.AddHostedService<UpdateReceiverService>();
builder.Services.AddHostedService<NotificationDispatcherService>();

var host = builder.Build();
host.Run();
