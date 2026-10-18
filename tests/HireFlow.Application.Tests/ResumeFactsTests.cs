using HireFlow.Application.Resumes;

namespace HireFlow.Application.Tests;

public class ResumeFactsTests
{
    [Theory]
    [InlineData("Опыт работы — 5 лет 3 месяца", 5)]
    [InlineData("Опыт работы 1 год", 1)]
    [InlineData("Более 3 лет опыта в разработке", 3)]
    [InlineData("3+ года опыта", 3)]
    [InlineData("Опыт 5 лет", 5)]
    [InlineData("5+ years of experience", 5)]
    [InlineData("Experience: 5 years", 5)]
    [InlineData("Опыт работы: 0 лет", 0)]
    [InlineData("Опыт работы: 50 лет", 50)]
    [InlineData("Опыт работы: 51 год", null)]
    [InlineData("Более 99 лет опыта", null)]
    [InlineData("Работал программистом, знаю C# и SQL", null)]
    [InlineData("2019 – 2023 Компания А\n2015 – 2019 Компания Б", null)]
    [InlineData("Опыт работы\n2019 – 2023 Компания А", null)]
    [InlineData("", null)]
    [InlineData("Опыт работы: 5 лет. Более 10 лет опыта в продажах", 5)]
    [InlineData("Более 10 лет опыта в продажах. Опыт работы: 5 лет", 10)]
    public void FindYearsOfExperience_TakesFirstExplicitStatement(string text, int? expected)
    {
        Assert.Equal(expected, ResumeFacts.FindYearsOfExperience(text));
    }

    [Theory]
    [InlineData("Иван Петров. Тел.: +7 (999) 123-45-67, ivan@example.com", "+79991234567")]
    [InlineData("Тел.: +7 999 123-45-67 1990 г.р.", "+79991234567")]
    [InlineData("2019 8 999 123 45 67", "+79991234567")]
    [InlineData("Работал в компании 2019–2023", null)]
    [InlineData("Принят 01.02.2020", null)]
    [InlineData("Период 01.02.2020 – 01.03.2023", null)]
    [InlineData("Индекс 123456, Москва", null)]
    [InlineData("", null)]
    public void FindPhone_ReturnsPhoneAndIgnoresYearsDatesAndPostalCodes(string text, string? expected)
    {
        Assert.Equal(expected, ResumeFacts.FindPhone(text));
    }
}
