using System.Text.RegularExpressions;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Infrastructure.Ai;

// Выбирает только короткие фрагменты возле навыков и удаляет распознаваемые личные данные.
public static partial class ResumeExcerptSanitizer
{
    [GeneratedRegex(@"[\w.+-]+@[\w.-]+\.[\p{L}]{2,}|(?:https?://|www\.)\S+|(?<!\w)@[A-Za-z0-9_]{3,}", RegexOptions.IgnoreCase, 200)]
    private static partial Regex Contacts();
    [GeneratedRegex(@"(?<!\w)\+?\d[\d ()\-]{6,}\d(?!\w)|\b\d{1,2}[./-]\d{1,2}[./-]\d{2,4}\b|\b\d{4}-\d{1,2}-\d{1,2}\b", RegexOptions.IgnoreCase, 200)]
    private static partial Regex Numbers();
    [GeneratedRegex(@"(?im)^.*(?:дата рождения|родил[ас]|место рождения|адрес|прожива|паспорт|возраст\s*:|семейное положение|инвалидность|национальность|религия|birth\s*date|date of birth|address|passport|marital status|disability|nationality|имя\s*:|фио\s*:).*$", RegexOptions.IgnoreCase, 200)]
    private static partial Regex SensitiveLines();
    [GeneratedRegex(@"\b(?:[А-ЯЁ][а-яё]{2,}\s+){1,2}[А-ЯЁ][а-яё]{2,}\b", RegexOptions.CultureInvariant, 200)]
    private static partial Regex RussianNames();
    [GeneratedRegex(@"(?<!\w)(?:[A-Z][a-z]{2,}\s+){1,2}[A-Z][a-z]{2,}(?!\w)", RegexOptions.CultureInvariant, 200)]
    private static partial Regex LatinNames();

    public static string Extract(string text, VacancyRequirements requirements, string? candidateName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sanitized = Sanitize(text, candidateName, phone);
        var skills = requirements.RequiredSkills.Concat(requirements.NiceToHaveSkills)
            .SelectMany(s => s.Synonyms.Prepend(s.Name))
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 100 && s.TrimEnd('*').Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(100);
        var excerpts = new List<string>();
        foreach (var skill in skills)
        {
            var index = sanitized.IndexOf(skill.TrimEnd('*'), StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;
            var start = Math.Max(0, index - 100);
            var length = Math.Min(sanitized.Length - start, skill.Length + 200);
            var excerpt = sanitized.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (excerpt.Length > 0 && !excerpts.Contains(excerpt, StringComparer.OrdinalIgnoreCase)) excerpts.Add(excerpt);
            if (excerpts.Count >= 8) break;
        }
        return string.Join("\n", excerpts);
    }

    public static string Sanitize(string text, string? candidateName, string? phone)
    {
        // Защита основана на шаблонах, поэтому не гарантирует обнаружение всех личных данных.
        var sanitized = SensitiveLines().Replace(text, " ");
        sanitized = Contacts().Replace(sanitized, "[контакт удалён]");
        sanitized = Numbers().Replace(sanitized, "[данные удалены]");
        foreach (var part in (candidateName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (part.Length >= 2) sanitized = Regex.Replace(sanitized, $@"(?<!\p{{L}}){Regex.Escape(part)}(?!\p{{L}})", "[имя удалено]", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        if (!string.IsNullOrWhiteSpace(phone)) sanitized = sanitized.Replace(phone, "[телефон удалён]", StringComparison.OrdinalIgnoreCase);
        sanitized = RussianNames().Replace(sanitized, "[имя удалено]");
        sanitized = LatinNames().Replace(sanitized, "[имя удалено]");

        return sanitized;
    }
}
