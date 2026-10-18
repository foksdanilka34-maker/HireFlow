using HireFlow.Bot.Api;

namespace HireFlow.Bot.Dialog;

public static class BotTexts
{
    public const string About =
        "Я помогаю найти вакансию и откликнуться на неё. Пришлите резюме в PDF или DOCX — я подберу подходящие " +
        "вакансии, вы выберете номер и откликнетесь. Результат отклика пришлю сюда.\n" +
        "Команды: /start — начать заново, /help — справка, /status — ваши отклики.";

    public const string Welcome = "Здравствуйте! " + About + "\n\nПришлите резюме в PDF или DOCX.";
    public const string SendResumeHint = "Пришлите резюме файлом PDF или DOCX.";
    public const string FileTooBig = "Файл больше 10 МБ. Пришлите файл поменьше в PDF или DOCX.";
    public const string UnreadableFile = "Не получилось прочитать файл. Пришлите PDF с текстом или DOCX.";
    public const string AskExperience = "Сколько лет у вас опыта в профессии? Напишите число.";
    public const string ExperienceHint = "Напишите число лет опыта, например 3. Или пришлите другое резюме.";
    public const string AskPhone = "В резюме нет телефона. Напишите номер для связи.";
    public const string PhoneHint = "Напишите номер телефона для связи, например +7 999 123-45-67. Или пришлите другое резюме.";
    public const string InvalidPhone = "Не похоже на номер телефона, пример: +7 999 123-45-67.";
    public const string NoApplications = "Откликов пока нет.";
    public const string Error = "Что-то пошло не так, попробуйте ещё раз через минуту.";
    public const string NoOpenVacancies = "Открытых вакансий сейчас нет. Загляните позже или пришлите другое резюме.";
    public const string NoMoreVacancies = "Это все вакансии из списка.";
    public const string ClosedLinked = "Набор на эту вакансию закрыт. Пришлите резюме — подберём другие.";
    public const string ClosedVacancy = "Набор на эту вакансию уже закрыт.";

    // Приглашение прислать резюме для вакансии по ссылке.
    public static string LinkedWelcome(string title) => $"Вы откликаетесь на «{title}». Пришлите резюме в PDF или DOCX.";

    // Сообщение о закрытом наборе с названием.
    public static string Closed(string title) => $"Набор на «{title}» уже закрыт.";

    // Вопрос, откликаться ли на вакансию по ссылке, когда она подходит.
    public static string ConfirmLinked(string title) =>
        $"Вакансия «{title}» вам подходит. Откликнуться? Напишите «да» или «нет».";

    public const string ConfirmHint = "Напишите «да», чтобы откликнуться, или «нет», чтобы посмотреть другие вакансии.";

    // Пустой подбор.
    public static string NoMatches(string? linkedTitle) =>
        "Сейчас подходящих вакансий нет. Напишите «все», чтобы посмотреть все открытые."
        + (linkedTitle is null ? "" : $"\n0 — всё равно откликнуться на «{linkedTitle}»");

    // Страница списка вакансий с подсказкой, что можно написать.
    public static string VacancyPage(string? header, IEnumerable<string> lines, bool hasMore, string? linkedTitle)
    {
        var text = (header is null ? "" : header + "\n") + string.Join("\n", lines)
            + "\nНапишите номер, чтобы откликнуться" + (hasMore ? " или «ещё», чтобы увидеть следующие" : "") + ".";
        return linkedTitle is null ? text : text + $"\n0 — всё равно откликнуться на «{linkedTitle}»";
    }

    // Строка списка вакансий.
    public static string VacancyLine(int number, string title, string direction) => $"{number}. {title} ({direction})";

    // Подсказка на шаге выбора вакансии.
    public static string ChooseHint(bool hasMore, bool hasLinkedOption, bool hasList) =>
        (hasList ? "Напишите номер вакансии из списка, чтобы откликнуться" : "Подходящих вакансий в списке нет")
        + (hasMore ? ", «ещё» — следующие вакансии" : "")
        + ", «все» — все открытые вакансии"
        + (hasLinkedOption ? ", «0» — откликнуться на вакансию по ссылке" : "")
        + ". Или пришлите другое резюме.";

    // Отклик принят.
    public static string Applied(string title) =>
        $"Отклик на «{title}» принят, сообщим результат. Можете выбрать ещё вакансию из списка или прислать другое резюме.";

    // Повторный отклик.
    public static string AlreadyApplied(string title, CandidateStatus status) =>
        $"Вы уже откликались на «{title}», статус: {StatusText(status)}.";

    // Статус отклика для кандидата.
    public static string StatusText(CandidateStatus status) => status switch
    {
        CandidateStatus.Invited => "Приглашение на созвон",
        CandidateStatus.Rejected => "Отказ",
        _ => "На рассмотрении",
    };

    // Список откликов кандидата.
    public static string Applications(IEnumerable<CandidateApplicationDto> applications) =>
        string.Join("\n", applications.Select(a => $"{a.VacancyTitle} — {StatusText(a.Status)}"));

    // Уведомление кандидату по типу.
    public static string Notification(NotificationKind kind, string vacancyTitle) => kind switch
    {
        NotificationKind.Invited => $"Вас приглашают на созвон по вакансии «{vacancyTitle}». Рекрутер свяжется с вами.",
        _ => $"Спасибо за отклик на «{vacancyTitle}». К сожалению, сейчас мы не готовы продолжить.",
    };
}
