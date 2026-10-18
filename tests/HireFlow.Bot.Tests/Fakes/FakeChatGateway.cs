using HireFlow.Bot.Telegram;

namespace HireFlow.Bot.Tests.Fakes;

// Самописный шлюз Telegram: запоминает отправленные сообщения и скачивания, файлы отдаёт из словаря.
internal sealed class FakeChatGateway : IChatGateway
{
    public List<(long ChatId, string Text)> Sent { get; } = [];
    public List<string> Downloads { get; } = [];
    public Dictionary<string, byte[]> Files { get; } = [];

    // Если задано, отправка бросает это исключение (например, «бот заблокирован»).
    public Exception? SendFailure { get; set; }

    public Task SendTextAsync(long chatId, string text, CancellationToken ct)
    {
        if (SendFailure is not null)
            throw SendFailure;

        Sent.Add((chatId, text));
        return Task.CompletedTask;
    }

    // Если задано, скачивание бросает это исключение (сбой Telegram).
    public Exception? DownloadFailure { get; set; }

    public Task<byte[]> DownloadFileAsync(string fileId, CancellationToken ct)
    {
        Downloads.Add(fileId);
        if (DownloadFailure is not null)
            throw DownloadFailure;

        return Task.FromResult(Files.TryGetValue(fileId, out var bytes) ? bytes : [1, 2, 3]);
    }
}
