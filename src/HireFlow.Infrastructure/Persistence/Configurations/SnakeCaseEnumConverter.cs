using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ToSnakeCase(value),
    value => FromSnakeCase(value))
    where TEnum : struct, Enum
{
    // Переводит значение enum в строку snake_case, как в JSON API: RecruiterReview → 'recruiter_review'.
    private static string ToSnakeCase(TEnum value) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    // Находит значение enum по строке snake_case.
    private static TEnum FromSnakeCase(string value) =>
        Enum.GetValues<TEnum>().First(v => ToSnakeCase(v) == value);
}
