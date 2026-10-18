using global::Telegram.Bot;

namespace HireFlow.Bot.Telegram;

public class TelegramChatGateway(ITelegramBotClient client) : IChatGateway
{
    public async Task SendTextAsync(long chatId, string text, CancellationToken ct) =>
        await client.SendMessage(chatId, text, cancellationToken: ct);

    public async Task<byte[]> DownloadFileAsync(string fileId, CancellationToken ct)
    {
        var file = await client.GetFile(fileId, ct);
        if (string.IsNullOrEmpty(file.FilePath))
            throw new InvalidOperationException("Telegram returned a file without a path.");

        using var memory = new MemoryStream();
        await client.DownloadFile(file.FilePath, memory, ct);
        return memory.ToArray();
    }
}
