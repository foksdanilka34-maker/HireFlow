namespace HireFlow.Bot.Telegram;

public interface IChatGateway
{
    // Отправляет обычное текстовое сообщение, без разметки и клавиатур.
    Task SendTextAsync(long chatId, string text, CancellationToken ct);

    // Скачивает файл из Telegram по file_id целиком в память.
    Task<byte[]> DownloadFileAsync(string fileId, CancellationToken ct);
}
