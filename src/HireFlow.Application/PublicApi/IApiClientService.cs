namespace HireFlow.Application.PublicApi;

public record IssuedApiKey(string Name, string Key);

public interface IApiClientService
{
    // Создаёт клиента или выдаёт существующему новый ключ (старый перестаёт работать, отзыв снимается).
    Task<IssuedApiKey> IssueKeyAsync(string name, CancellationToken ct);

    // Отзывает ключ клиента; клиента нет — NotFoundException.
    Task RevokeAsync(string name, CancellationToken ct);
}
