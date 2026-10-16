using System.Text;
using System.Text.RegularExpressions;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Domain.Screening;

public static class SkillMatcher
{
    private static readonly Regex CyrillicCBeforeSymbols = new(@"с(?=#|\+\+)");

    // Приводит текст к виду для поиска: нижний регистр, «ё» → «е», кириллическая «с» в C#/C++ → латинская.
    public static string Normalize(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Replace('ё', 'е');
        return CyrillicCBeforeSymbols.Replace(normalized, "c");
    }

    // Проверяет, есть ли в нормализованном тексте навык или один из его синонимов.
    public static bool Contains(string normalizedText, Skill skill)
    {
        return skill.Synonyms.Prepend(skill.Name).Any(term => ContainsTerm(normalizedText, term));
    }

    // Ищет одно написание навыка целым словом. «*» в конце — любое окончание: «автотест*» найдёт «автотестов».
    private static bool ContainsTerm(string normalizedText, string term)
    {
        var normalizedTerm = Normalize(term).Trim();
        var anyEnding = normalizedTerm.EndsWith('*');
        normalizedTerm = normalizedTerm.TrimEnd('*');

        var words = Regex.Split(normalizedTerm, @"[\s\-]+").Where(w => w.Length > 0).Select(Regex.Escape).ToList();
        if (words.Count == 0)
            return false;

        var body = string.Join(@"[\s\-]*", words) + (anyEnding ? @"[\p{L}\p{N}]*" : "");
        var pattern = $@"(?<![\p{{L}}\p{{N}}#+]){body}(?![\p{{L}}\p{{N}}#+])";

        return Regex.IsMatch(normalizedText, pattern);
    }
}
