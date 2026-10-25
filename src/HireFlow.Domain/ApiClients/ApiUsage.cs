namespace HireFlow.Domain.ApiClients;

// Число запросов клиента публичного API за день (UTC); строки пишет и увеличивает один SQL-запрос.
public class ApiUsage
{
    public Guid ApiClientId { get; private set; }
    public DateOnly Day { get; private set; }
    public int Count { get; private set; }

    // Нужен EF Core для создания объекта при чтении из БД.
    private ApiUsage() { }
}
