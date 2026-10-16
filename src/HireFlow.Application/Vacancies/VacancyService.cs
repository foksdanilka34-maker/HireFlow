using HireFlow.Application.Common;
using HireFlow.Domain;
using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Application.Vacancies;

public class VacancyService(IAppDbContext db) : IVacancyService
{
    public async Task<List<VacancyResponse>> GetAllAsync(VacancyStatus? status, CancellationToken ct)
    {
        var query = db.Vacancies.AsNoTracking();

        query = status is null
            ? query.Where(v => v.Status != VacancyStatus.Archived)
            : query.Where(v => v.Status == status);

        var vacancies = await query.OrderByDescending(v => v.CreatedAt).ToListAsync(ct);
        return vacancies.Select(VacancyResponse.From).ToList();
    }

    public async Task<VacancyResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var vacancy = await FindAsync(id, ct);
        return VacancyResponse.From(vacancy);
    }

    public async Task<VacancyResponse> CreateAsync(VacancyRequest request, CancellationToken ct)
    {
        var vacancy = new Vacancy(
            request.Title, request.Direction, request.Description, request.Requirements, request.FreezePeriodDays);

        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(ct);

        return VacancyResponse.From(vacancy);
    }

    public async Task<VacancyResponse> UpdateAsync(Guid id, VacancyRequest request, CancellationToken ct)
    {
        var vacancy = await FindAsync(id, ct);
        vacancy.Update(
            request.Title, request.Direction, request.Description, request.Requirements, request.FreezePeriodDays);

        await db.SaveChangesAsync(ct);
        return VacancyResponse.From(vacancy);
    }

    public Task<VacancyResponse> OpenAsync(Guid id, CancellationToken ct) =>
        ChangeAsync(id, v => v.Open(), ct);

    public Task<VacancyResponse> CloseAsync(Guid id, CancellationToken ct) =>
        ChangeAsync(id, v => v.Close(), ct);

    public Task<VacancyResponse> ArchiveAsync(Guid id, CancellationToken ct) =>
        ChangeAsync(id, v => v.Archive(), ct);

    public Task<VacancyResponse> UnarchiveAsync(Guid id, CancellationToken ct) =>
        ChangeAsync(id, v => v.Unarchive(), ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var vacancy = await FindAsync(id, ct);

        if (await db.JobApplications.AnyAsync(a => a.VacancyId == id, ct))
            throw new DomainException("Vacancy has applications and cannot be deleted. Archive it instead.");

        db.Vacancies.Remove(vacancy);
        await db.SaveChangesAsync(ct);
    }

    // Загружает вакансию, применяет к ней действие и сохраняет.
    private async Task<VacancyResponse> ChangeAsync(Guid id, Action<Vacancy> change, CancellationToken ct)
    {
        var vacancy = await FindAsync(id, ct);
        change(vacancy);

        await db.SaveChangesAsync(ct);
        return VacancyResponse.From(vacancy);
    }

    // Ищет вакансию по id или бросает NotFoundException.
    private async Task<Vacancy> FindAsync(Guid id, CancellationToken ct)
    {
        return await db.Vacancies.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException($"Vacancy {id} not found.");
    }
}
