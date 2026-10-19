using HireFlow.Domain.JobApplications;

namespace HireFlow.Application.JobApplications;

public interface IJobApplicationService
{
    // Создаёт отклик: проверяет кандидата, вакансию и файл, сохраняет резюме и сразу проводит скрининг.
    Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request, CancellationToken ct);

    // Возвращает отклик по id вместе с историей статусов.
    Task<ApplicationResponse> GetAsync(Guid id, CancellationToken ct);

    // Возвращает отклики с фильтрами по вакансии и статусу, новые первыми.
    Task<List<ApplicationListItem>> ListAsync(Guid? vacancyId, ApplicationStatus? status, CancellationToken ct);

    // Применяет решение рекрутера: пригласить на созвон или отказать.
    Task<ApplicationResponse> DecideAsync(Guid id, Guid recruiterId, DecisionRequest request, CancellationToken ct);

    // Открывает файл резюме отклика; нет отклика или файла — NotFoundException.
    Task<ResumeFile> GetResumeAsync(Guid id, CancellationToken ct);
}
