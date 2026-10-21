using HireFlow.Bot;
using HireFlow.Bot.Api;
using HireFlow.Bot.Dialog;
using HireFlow.Bot.Telegram;
using global::Telegram.Bot;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Время на дообработку очередей сообщений при остановке.
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(60));

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
    .Validate(o => o.Workers is >= 1 and <= 64, "Bot:Workers must be between 1 and 64.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IHireFlowApiClient, HireFlowApiClient>((sp, http) =>
{
    var botOptions = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    http.BaseAddress = new Uri(botOptions.ApiBaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(30);
    http.DefaultRequestHeaders.Add("X-Api-Key", botOptions.ApiKey);
});

builder.Services.AddHttpClient("telegram")
    .AddTypedClient<ITelegramBotClient>((http, sp) =>
        new TelegramBotClient(sp.GetRequiredService<IOptions<BotOptions>>().Value.TelegramToken, http));
builder.Services.AddScoped<IChatGateway, TelegramChatGateway>();
builder.Services.AddScoped<DialogHandler>();

builder.Services.AddSingleton(sp =>
{
    var scopes = sp.GetRequiredService<IServiceScopeFactory>();
    return new UpdateDispatcher(
        sp.GetRequiredService<IOptions<BotOptions>>().Value.Workers,
        async (message, ct) =>
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DialogHandler>().HandleAsync(message, ct);
        },
        sp.GetRequiredService<ILogger<UpdateDispatcher>>());
});
builder.Services.AddHostedService<UpdateReceiverService>();
builder.Services.AddHostedService<NotificationDispatcherService>();

var host = builder.Build();
host.Run();
