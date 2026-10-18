using HireFlow.Domain.Screening;
using static HireFlow.Domain.Tests.TestData;

namespace HireFlow.Domain.Tests;

public class SkillMatcherTests
{
    // Ищет навык в произвольном тексте резюме (текст нормализуется, как в скоринге).
    private static bool Found(string resumeText, string skill, params string[] synonyms) =>
        SkillMatcher.Contains(SkillMatcher.Normalize(resumeText), Skill(skill, synonyms));

    [Theory]
    [InlineData("опыт работы с PYTHON и Django", "Python")]
    [InlineData("python", "PYTHON")]
    [InlineData("Знаю PyThOn", "pYtHoN")]
    public void Contains_IgnoresCase(string text, string skill) =>
        Assert.True(Found(text, skill));

    [Theory]
    [InlineData("Ведение учет и отчётности", "Учёт")]
    [InlineData("Ведение учёт и отчётности", "Учет")]
    [InlineData("ЁЖ", "еж")]
    public void Contains_TreatsYoAsYe(string text, string skill) =>
        Assert.True(Found(text, skill));

    [Theory]
    [InlineData("Знаю С#, Java", "C#")]
    [InlineData("Знаю с#, Java", "C#")]
    [InlineData("Работал с С++ и Rust", "C++")]
    [InlineData("Знаю C#", "С#")]
    public void Contains_TreatsCyrillicCBeforeSymbolsAsLatin(string text, string skill) =>
        Assert.True(Found(text, skill));

    [Theory]
    [InlineData("Знаю C#, Java", "C")]
    [InlineData("Знаю C++, Java", "C")]
    [InlineData("Знаю С#, Java", "C")]
    public void Contains_DoesNotFindCInsideCSharpOrCpp(string text, string skill) =>
        Assert.False(Found(text, skill));

    [Fact]
    public void Contains_FindsPlainC() =>
        Assert.True(Found("Языки: C, Pascal", "C"));

    [Fact]
    public void Contains_DoesNotFindSqlInsidePostgreSql() =>
        Assert.False(Found("Опыт работы с PostgreSQL", "SQL"));

    [Fact]
    public void Contains_FindsSqlThroughSynonym() =>
        Assert.True(Found("Опыт работы с PostgreSQL", "SQL", "PostgreSQL"));

    [Theory]
    [InlineData("Spring Boot")]
    [InlineData("SpringBoot")]
    [InlineData("spring-boot")]
    [InlineData("SPRING   BOOT")]
    [InlineData("Spring-Boot 3")]
    public void Contains_TreatsSpaceHyphenAndNothingAsEqual(string text) =>
        Assert.True(Found(text, "Spring Boot"));

    [Theory]
    [InlineData("Пишу автотесты на Python")]
    [InlineData("Написал много автотестов")]
    [InlineData("Занимаюсь автотестированием и автотестирование")]
    [InlineData("Автотесты")]
    public void Contains_WildcardMatchesAnyEnding(string text) =>
        Assert.True(Found(text, "автотест*"));

    [Fact]
    public void Contains_WithoutWildcardDoesNotMatchLongerWord() =>
        Assert.False(Found("Пишу автотесты", "автотест"));

    [Fact]
    public void Contains_WildcardDoesNotMatchBeforeUnrelatedPrefix() =>
        Assert.False(Found("суперавтотесты", "автотест*"));

    [Theory]
    [InlineData("Docker, Kubernetes", "Docker")]
    [InlineData("Kubernetes, Docker", "Docker")]
    [InlineData("Docker", "Docker")]
    [InlineData("Опыт: Docker.", "Docker")]
    public void Contains_FindsSkillAtStartAndEndOfText(string text, string skill) =>
        Assert.True(Found(text, skill));

    [Fact]
    public void Contains_FindsSkillBySynonym() =>
        Assert.True(Found("Пишу на dotnet", "C#", ".NET", "dotnet"));

    [Fact]
    public void Contains_ReturnsFalseWhenNeitherNameNorSynonymsPresent() =>
        Assert.False(Found("Пишу на Java", "C#", ".NET", "dotnet"));

    [Theory]
    [InlineData("JavaScript", "Java")]
    [InlineData("Java8", "Java")]
    [InlineData("Python3", "Python")]
    public void Contains_RequiresWholeWord(string text, string skill) =>
        Assert.False(Found(text, skill));

    [Theory]
    [InlineData("Backend на Node.js", "Node.js")]
    [InlineData("ASP.NET Core", "ASP.NET")]
    public void Contains_EscapesRegexSpecialCharacters(string text, string skill) =>
        Assert.True(Found(text, skill));

    [Fact]
    public void Contains_DotIsNotWildcard() =>
        Assert.False(Found("Backend на NodeXjs", "Node.js"));
}
