using System.Text;

namespace HireFlow.Domain.Candidates;

public static class PhoneNumber
{
    private const int MinDigits = 7;
    private const int MaxDigits = 15;

    // Приводит номер к формату E.164 («+79991234567»); без «+» принимает только российские форматы (11 цифр на 7/8, 10 цифр на 9), иначе false.
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

        if (!hasPlus)
        {
            // Без «+» принимаем только российский формат: 11 цифр на 7/8 или 10 цифр мобильного на 9.
            if (digits.Length == 11 && digits[0] is '7' or '8')
            {
                digits[0] = '7';
            }
            else if (digits.Length == 10 && digits[0] == '9')
            {
                digits.Insert(0, '7');
            }
            else
            {
                return false;
            }
        }

        if (digits.Length is < MinDigits or > MaxDigits)
            return false;

        // Номера РФ/КЗ (код 7) — ровно 11 цифр, иначе фрагмент неоднозначен (например, номер с приклеенным годом).
        if (digits[0] == '7' && digits.Length != 11)
            return false;

        e164 = "+" + digits;
        return true;
    }
}
