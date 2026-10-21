namespace HireFlow.Web.Vacancies;

public static class VacancyLinks
{
    // Ссылка на бота с вакансией: id без дефисов, бот разбирает его как Guid.
    public static string Telegram(string botUsername, Guid vacancyId) =>
        $"https://t.me/{botUsername}?start={vacancyId:N}";
}
