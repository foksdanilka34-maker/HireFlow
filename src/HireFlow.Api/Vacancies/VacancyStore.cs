using System.Collections.Concurrent;

namespace HireFlow.Api.Vacancies;

public class VacancyStore
{
    private readonly ConcurrentDictionary<int, Vacancy> _vacancies = new();
    private int _lastId;

    public List<Vacancy> GetAll() => _vacancies.Values.OrderBy(v => v.Id).ToList();
    public Vacancy? Get(int id) => _vacancies.GetValueOrDefault(id);

    public Vacancy Add(string title, string ?description, VacancyRequirements requirements)
    {
        var id = Interlocked.Increment(ref _lastId);
        var vacancy = new Vacancy(id, title, description, requirements,
            IsOpen: true, CreatedAt: DateTime.UtcNow);
        _vacancies[id] = vacancy;
        return vacancy;
    }

    public Vacancy? Update(int id, string title, string ?description, VacancyRequirements requirements, bool isOpen)
    {
        if (!_vacancies.TryGetValue(id, out var existing))
            return null;
        
        var updated = existing with
        {
            Title = title,
            Description = description,
            Requirements = requirements,
            IsOpen = isOpen
        };
        _vacancies[id] = updated;
        return updated;
    }

    public bool Delete(int id) => _vacancies.TryRemove(id, out _);
}