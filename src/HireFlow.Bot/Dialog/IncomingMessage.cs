namespace HireFlow.Bot.Dialog;

public record IncomingDocument(string FileId, string? FileName, long? FileSize);

public record IncomingMessage(
    long UserId,
    long ChatId,
    string? Text,
    IncomingDocument? Document,
    string? FirstName,
    string? LastName);
