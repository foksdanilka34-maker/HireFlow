using System.Globalization;
using System.Text.RegularExpressions;
using HireFlow.Domain.JobApplications;

namespace HireFlow.Infrastructure.Ai;

// Только однозначные суммы в рублях за месяц; свободные формулировки остаются на проверку человеку и модели.
internal static partial class SalaryLimitCheck
{
    [GeneratedRegex(@"зарплат|оклад|оплат|доход", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex SalaryQuestion();

    [GeneratedRegex(@"^\s*(?:до|не более|максимум)\s+(\d[\d\s]*)\s*(?:руб(?:лей|ля|ль|\.)?|₽)(?:\s+в\s+месяц)?\s*[.!]?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex Maximum();

    [GeneratedRegex(@"^\s*(\d[\d\s]*)\s*(?:руб(?:лей|ля|ль|\.)?|₽)(?:\s+в\s+месяц)?\s*[.!]?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 200)]
    private static partial Regex Amount();

    public static bool ExceedsExplicitLimit(ApplicationQuestion question)
    {
        if (!SalaryQuestion().IsMatch(question.Text) || question.Criterion is null || question.Answer is null) return false;
        var maximum = Maximum().Match(question.Criterion);
        var answer = Amount().Match(question.Answer);
        return maximum.Success && answer.Success && TryNumber(maximum.Groups[1].Value, out var limit)
            && TryNumber(answer.Groups[1].Value, out var expected) && expected > limit;
    }

    private static bool TryNumber(string value, out long amount) =>
        long.TryParse(string.Concat(value.Where(c => !char.IsWhiteSpace(c))), NumberStyles.None, CultureInfo.InvariantCulture, out amount);
}
