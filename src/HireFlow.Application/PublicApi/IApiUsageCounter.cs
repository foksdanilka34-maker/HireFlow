namespace HireFlow.Application.PublicApi;

public interface IApiUsageCounter
{
    // Атомарно увеличивает счётчик клиента за день, если он меньше квоты; квота исчерпана — false.
    Task<bool> TryIncrementAsync(Guid clientId, DateOnly day, int quota, CancellationToken ct);

    // Возвращает число запросов клиента за день.
    Task<int> GetAsync(Guid clientId, DateOnly day, CancellationToken ct);
}
