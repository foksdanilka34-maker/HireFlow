using HireFlow.Application.Common;
using HireFlow.Application.PublicApi;
using HireFlow.Domain;
using HireFlow.Domain.ApiClients;

namespace HireFlow.Api.PublicApi;

public static class ApiClientCommand
{
    // Выдаёт клиенту публичного API новый ключ или отзывает его из консоли; возвращает код выхода.
    public static async Task<int> RunAsync(IServiceProvider services, string name, bool revoke)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IApiClientService>();
        try
        {
            if (revoke)
            {
                await service.RevokeAsync(name, CancellationToken.None);
                Console.WriteLine($"Ключ клиента {ApiClient.NormalizeName(name)} отозван");
                return 0;
            }

            var issued = await service.IssueKeyAsync(name, CancellationToken.None);
            Console.WriteLine($"Ключ клиента {issued.Name}:");
            Console.WriteLine(issued.Key);
            Console.WriteLine("Сохраните ключ сейчас: больше его не показать. Прежний ключ клиента больше не работает.");
            return 0;
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (ConflictException)
        {
            Console.Error.WriteLine("Клиент с таким именем только что создан другой командой. Повторите команду.");
            return 1;
        }
    }
}
