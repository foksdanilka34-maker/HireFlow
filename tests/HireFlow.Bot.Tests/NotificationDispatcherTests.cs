using HireFlow.Bot.Api;
using HireFlow.Bot.Telegram;
using HireFlow.Bot.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HireFlow.Bot.Tests;

// Одна итерация рассылки: результат отправки уходит в API как sent или failed.
public class NotificationDispatcherTests
{
    private readonly FakeHireFlowApiClient _api = new();
    private readonly FakeChatGateway _chat = new();

    // Сервис с настоящей фабрикой scope: клиенты API и Telegram берутся из DI, как в работе бота.
    private async Task RunOnceAsync()
    {
        await using var provider = new ServiceCollection()
            .AddScoped<IHireFlowApiClient>(_ => _api)
            .AddScoped<IChatGateway>(_ => _chat)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var service = new NotificationDispatcherService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BotOptions()),
            NullLogger<NotificationDispatcherService>.Instance);

        await service.RunOnceAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(NotificationKind.Invited, "Вас приглашают на созвон по вакансии «QA». Рекрутер свяжется с вами.")]
    [InlineData(NotificationKind.Rejected, "Спасибо за отклик на «QA». К сожалению, сейчас мы не готовы продолжить.")]
    public async Task RunOnce_SentNotificationIsMarkedSentWithCandidateText(NotificationKind kind, string expectedText)
    {
        var notification = new DueNotificationDto(Guid.NewGuid(), 777, kind, "QA");
        _api.Due.Add(notification);

        await RunOnceAsync();

        Assert.Equal((777L, expectedText), Assert.Single(_chat.Sent));
        Assert.Equal([notification.Id], _api.SentIds);
        Assert.Empty(_api.FailedIds);
    }

    [Fact]
    public async Task RunOnce_TelegramErrorIsMarkedFailedWithErrorAndNotSent()
    {
        var notification = new DueNotificationDto(Guid.NewGuid(), 777, NotificationKind.Invited, "QA");
        _api.Due.Add(notification);
        _chat.SendFailure = new InvalidOperationException("Forbidden: bot was blocked by the user");

        await RunOnceAsync();

        Assert.Empty(_api.SentIds);
        var failed = Assert.Single(_api.FailedIds);
        Assert.Equal(notification.Id, failed.Id);
        Assert.Equal("Forbidden: bot was blocked by the user", failed.Error);
    }
}
