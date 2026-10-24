namespace HireFlow.Application.PublicApi;

public interface IPublicScreeningService
{
    // Списывает запрос из дневной квоты клиента; квота исчерпана — TooManyRequestsException до полуночи UTC.
    Task ChargeQuotaAsync(Guid clientId, int dailyQuota, CancellationToken ct);

    // Проверяет резюме против требований клиента; неверный запрос или нечитаемый файл — BadRequestException.
    Task<PublicScreeningResponse> ScreenAsync(PublicScreeningRequest request, CancellationToken ct);

    // Возвращает использование квоты клиента за текущий день UTC.
    Task<PublicUsageResponse> GetUsageAsync(Guid clientId, int dailyQuota, CancellationToken ct);
}
