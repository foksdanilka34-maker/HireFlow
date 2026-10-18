using System.Text;

namespace HireFlow.Domain.Candidates;

public static class PhoneNumber
{
    private const int MinDigits = 7;
    private const int MaxDigits = 15;

    // Приводит номер к формату E.164 («+79991234567»); false, если на номер не похоже.
    public static bool TryNormalize(string? input, out string e164)
    {
        e164 = "";
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = new StringBuilder();

        foreach (var ch in hasPlus ? trimmed[1..] : trimmed)
        {
            if (char.IsAsciiDigit(ch))
                digits.Append(ch);
            else if (!(char.IsWhiteSpace(ch) || ch is '-' or '(' or ')' or '.'))
                return false;
        }

        if (!hasPlus && digits.Length == 11 && digits[0] == '8')
            digits[0] = '7';

        if (digits.Length is < MinDigits or > MaxDigits)
            return false;

        // Номера РФ/КЗ (код 7) — ровно 11 цифр, иначе фрагмент неоднозначен (например, номер с приклеенным годом).
        if (digits[0] == '7' && digits.Length != 11)
            return false;

        e164 = "+" + digits;
        return true;
    }
}
