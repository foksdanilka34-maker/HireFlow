using System.Text.RegularExpressions;
using HireFlow.Domain.Candidates;

namespace HireFlow.Application.Resumes;

public static partial class ResumeFacts
{
    private const int MaxYears = 50;
    private const int MinPhoneDigits = 10;

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Явные формулировки опыта: число лет всегда в первой группе. Диапазоны дат намеренно не учитываются.
    private static readonly Regex[] ExperiencePatterns =
    [
        // «Опыт работы — 5 лет 3 месяца», «опыт работы: 5 лет», «опыт 5 лет», «опыт работы более 3 лет».
        new(@"(?<!\p{L})опыт(?:\s+работы)?\s*[:\-–—]?\s*(?:более\s+|свыше\s+|около\s+|от\s+)?(?<![\d])(\d{1,3})\s*\+?\s*(?:лет|года|год)(?![\p{L}\d])", Options),
        // «более 3 лет опыта», «3+ года опыта», «5 лет опыта».
        new(@"(?<![\d])(\d{1,3})\s*\+?\s*(?:лет|года|год)\s+опыта(?![\p{L}])", Options),
        // «5+ years of experience», «5 years of commercial experience».
        new(@"(?<![\d])(\d{1,3})\s*\+?\s*years?\s+of\s+(?:\p{L}+\s+)?experience(?![\p{L}])", Options),
        // «Experience: 5 years».
        new(@"(?<!\p{L})experience\s*[:\-–—]?\s*(?<![\d])(\d{1,3})\s*\+?\s*years?(?![\p{L}])", Options),
    ];

    // Фрагмент, похожий на телефон: цифры с пробелами, дефисами, точками и скобками.
    [GeneratedRegex(@"(?<![\p{L}\d])\+?\(?\d[\d \t \-().]{7,22}\d(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneCandidate();

    // Одни годы («2019–2023», «2015 2019»).
    [GeneratedRegex(@"^(?:\s*(?:19|20)\d{2}\s*[\-–—.]?)+\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex OnlyYears();

    // Дата вида «01.02.2020» внутри фрагмента.
    [GeneratedRegex(@"\d{1,2}\.\d{1,2}\.\d{2,4}", RegexOptions.CultureInvariant)]
    private static partial Regex DateLike();

    // Ищет в резюме явную формулировку опыта и возвращает полные годы (месяцы отбрасываются); нет или вне 0–50 — null.
    public static int? FindYearsOfExperience(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        Match? first = null;
        foreach (var pattern in ExperiencePatterns)
        {
            var match = pattern.Match(text);
            if (match.Success && (first is null || match.Index < first.Index))
                first = match;
        }

        if (first is null || !int.TryParse(first.Groups[1].Value, out var years))
            return null;

        return years is >= 0 and <= MaxYears ? years : null;
    }

    // Отбрасывает ведущие и хвостовые группы, похожие на год («2019 8 999…», «…67 1990»), если остаётся достаточно цифр.
    private static string TrimYearGroups(string fragment)
    {
        var groups = fragment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();

        while (groups.Count > 1 && YearGroup().IsMatch(groups[0]) && DigitCount(groups.Skip(1)) >= MinPhoneDigits)
            groups.RemoveAt(0);
        while (groups.Count > 1 && YearGroup().IsMatch(groups[^1]) && DigitCount(groups.Take(groups.Count - 1)) >= MinPhoneDigits)
            groups.RemoveAt(groups.Count - 1);

        return string.Join(' ', groups);
    }

    // Считает цифры в группах.
    private static int DigitCount(IEnumerable<string> groups) => groups.Sum(g => g.Count(char.IsAsciiDigit));

    // Группа из четырёх цифр, похожая на год.
    [GeneratedRegex(@"^(?:19|20)\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex YearGroup();

    // Ищет в резюме первый фрагмент, похожий на телефон, и возвращает его в формате E.164; годы и даты не берёт.
    public static string? FindPhone(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (Match match in PhoneCandidate().Matches(text))
        {
            var fragment = TrimYearGroups(match.Value.Trim());
            if (OnlyYears().IsMatch(fragment) || DateLike().IsMatch(fragment))
                continue;
            if (fragment.Count(char.IsAsciiDigit) < MinPhoneDigits)
                continue;
            if (PhoneNumber.TryNormalize(fragment, out var e164))
                return e164;
        }

        return null;
    }
}
