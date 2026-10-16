using HireFlow.Domain.Vacancies;

namespace HireFlow.Application.Vacancies;

public interface IVacancyService
{
    // Возвращает вакансии с нужным статусом, а без фильтра — все, кроме архивных.
    Task<List<VacancyResponse>> GetAllAsync(VacancyStatus? status, CancellationToken ct);

    // Возвращает вакансию по id.
    Task<VacancyResponse> GetAsync(Guid id, CancellationToken ct);

    // Создаёт вакансию.
    Task<VacancyResponse> CreateAsync(VacancyRequest request, CancellationToken ct);

    // Меняет содержимое вакансии.
    Task<VacancyResponse> UpdateAsync(Guid id, VacancyRequest request, CancellationToken ct);

    // Открывает приём откликов.
    Task<VacancyResponse> OpenAsync(Guid id, CancellationToken ct);

    // Закрывает приём откликов.
    Task<VacancyResponse> CloseAsync(Guid id, CancellationToken ct);

    // Переносит вакансию в архив.
    Task<VacancyResponse> ArchiveAsync(Guid id, CancellationToken ct);

    // Возвращает вакансию из архива.
    Task<VacancyResponse> UnarchiveAsync(Guid id, CancellationToken ct);

    // Удаляет вакансию.
    Task DeleteAsync(Guid id, CancellationToken ct);
}
