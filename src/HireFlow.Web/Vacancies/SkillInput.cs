using HireFlow.Web.Api;

namespace HireFlow.Web.Vacancies;

// Строка формы: навык и синонимы через запятую.
public class SkillRow
{
    public string Name { get; set; } = "";
    public string Synonyms { get; set; } = "";
}

// Преобразование навыков между формой и API.
public static class SkillInput
{
    // Разбирает синонимы через запятую: trim, без пустых и без повторов без учёта регистра.
    public static List<string> ParseSynonyms(string? text) =>
        (text ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Собирает синонимы в строку для поля формы.
    public static string FormatSynonyms(IEnumerable<string> synonyms) => string.Join(", ", synonyms);

    // Превращает строки формы в навыки, отбрасывая строки без названия.
    public static List<SkillDto> ToSkills(IEnumerable<SkillRow> rows) =>
        rows.Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => new SkillDto(r.Name.Trim(), ParseSynonyms(r.Synonyms)))
            .ToList();

    // Превращает навыки из API в строки формы.
    public static List<SkillRow> ToRows(IEnumerable<SkillDto> skills) =>
        skills.Select(s => new SkillRow { Name = s.Name, Synonyms = FormatSynonyms(s.Synonyms) }).ToList();
}
