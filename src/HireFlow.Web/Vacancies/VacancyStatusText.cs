using HireFlow.Web.Api;

namespace HireFlow.Web.Vacancies;

public enum VacancyAction
{
    Open,
    Close,
    Archive,
    Unarchive,
}

// Русские названия статусов вакансии и доступные по статусу действия.
public static class VacancyStatusText
{
    // Русское название статуса.
    public static string Name(VacancyStatus status) => status switch
    {
        VacancyStatus.Open => "Открыта",
        VacancyStatus.Closed => "Закрыта",
        VacancyStatus.Archived => "В архиве",
        _ => status.ToString(),
    };

    // Подпись кнопки действия.
    public static string ActionName(VacancyAction action) => action switch
    {
        VacancyAction.Open => "Открыть",
        VacancyAction.Close => "Закрыть",
        VacancyAction.Archive => "В архив",
        VacancyAction.Unarchive => "Из архива",
        _ => action.ToString(),
    };

    // Действия смены статуса, которые допустимы для статуса.
    public static IReadOnlyList<VacancyAction> Actions(VacancyStatus status) => status switch
    {
        VacancyStatus.Open => [VacancyAction.Close, VacancyAction.Archive],
        VacancyStatus.Closed => [VacancyAction.Open, VacancyAction.Archive],
        VacancyStatus.Archived => [VacancyAction.Unarchive],
        _ => [],
    };

    // Редактировать можно всё, кроме архива.
    public static bool CanEdit(VacancyStatus status) => status != VacancyStatus.Archived;
}
