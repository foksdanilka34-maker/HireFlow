using HireFlow.Web.Api;

namespace HireFlow.Web.Applications;

// Вакансия для выпадающего фильтра.
public record VacancyOption(Guid Id, string Title);

// Сортировка и фильтры списков откликов.
public static class ApplicationLists
{
    // Очередь: высокий балл сверху, при равном — кто дольше ждёт.
    public static List<ApplicationListItemDto> Queue(IEnumerable<ApplicationListItemDto> items) =>
        items.OrderByDescending(a => a.Score).ThenBy(a => a.CreatedAt).ToList();

    // Оставляет отклики выбранной вакансии и, если нужно, только пограничные; порядок не меняется.
    public static List<ApplicationListItemDto> Filter(
        IEnumerable<ApplicationListItemDto> items, Guid? vacancyId, bool onlyBorderline) =>
        items.Where(a => vacancyId is null || a.VacancyId == vacancyId)
            .Where(a => !onlyBorderline || a.Verdict == ScreeningVerdict.Borderline)
            .ToList();

    // Вакансии из списка откликов без повторов, по названию.
    public static List<VacancyOption> Vacancies(IEnumerable<ApplicationListItemDto> items) =>
        items.DistinctBy(a => a.VacancyId)
            .Select(a => new VacancyOption(a.VacancyId, a.VacancyTitle))
            .OrderBy(v => v.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
