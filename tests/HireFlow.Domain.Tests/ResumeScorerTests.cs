using HireFlow.Domain.Screening;
using static HireFlow.Domain.Tests.TestData;

namespace HireFlow.Domain.Tests;

public class ResumeScorerTests
{
    // Названия вымышленных навыков, чтобы случайно не пересечься с обычными словами.
    private const string FiveSkills = "alpha,bravo,charlie,delta,echo";
    private const string EightSkills = "alpha,bravo,charlie,delta,echo,foxtrot,golf,hotel";

    // Разбирает список навыков через запятую; пустая строка — нет навыков.
    private static string[] Split(string list) => list.Length == 0 ? [] : list.Split(',');

    [Theory]
    [InlineData("alpha,bravo", "charlie,delta", "alpha bravo charlie", 90)]      // веса 80/20: 80 + 1 из 2 желательных = 10
    [InlineData("alpha,bravo", "charlie", "alpha charlie", 60)]                  // доли, а не количество: 40 + 20
    [InlineData("alpha,bravo,charlie", "", "alpha bravo", 67)]                   // без желательных: 100 * доля обязательных
    [InlineData("alpha", "bravo", "ничего общего", 0)]
    [InlineData("alpha", "bravo", "bravo", 20)]                                  // найдено только желательное
    [InlineData(EightSkills, "", "alpha, bravo, charlie, delta, echo", 63)]      // 5 из 8 = 62.5 -> 63 (банковское округление дало бы 62)
    [InlineData("alpha", "bravo,charlie,delta,echo,foxtrot,golf,hotel,india", "alpha bravo", 83)] // 80 + 20/8 = 82.5 -> 83
    public void Score_UsesWeightsSharesAndRoundsHalfUp(string required, string niceToHave, string resume, int expected)
    {
        var result = ResumeScorer.Evaluate(resume, 5, Requirements(Split(required), Split(niceToHave)));

        Assert.Equal(expected, result.Score);
    }

    [Theory]
    // Балл равен порогу / на 1 ниже порога.
    [InlineData("alpha", "bravo,charlie", "alpha bravo", 5, 0, 90, 10, ScreeningVerdict.Passed, 90)]
    [InlineData("alpha", "bravo,charlie", "alpha bravo", 5, 0, 91, 10, ScreeningVerdict.Borderline, 90)]
    // Ровно один недостающий навык делает отклик пограничным, даже если балл далеко от порога.
    [InlineData(FiveSkills, "", "alpha, bravo, charlie, delta", 5, 0, 100, 0, ScreeningVerdict.Borderline, 80)]
    // Не хватает только опыта / опыт равен минимуму.
    [InlineData("alpha,bravo", "", "alpha bravo", 2, 3, 70, 10, ScreeningVerdict.Borderline, 100)]
    [InlineData("alpha", "", "alpha", 3, 3, 70, 10, ScreeningVerdict.Passed, 100)]
    // Балл равен порогу минус ширина зоны (два недостающих навыка) / на 1 ниже зоны.
    [InlineData(FiveSkills, "", "alpha, bravo, charlie", 5, 0, 70, 10, ScreeningVerdict.Borderline, 60)]
    [InlineData(FiveSkills, "", "alpha, bravo, charlie", 5, 0, 71, 10, ScreeningVerdict.Failed, 60)]
    // Два недостающих навыка и балл ниже зоны.
    [InlineData(FiveSkills, "", "alpha, bravo, charlie", 5, 0, 90, 10, ScreeningVerdict.Failed, 60)]
    // Не хватает навыка и опыта: в зоне — пограничный, ниже зоны — отказ.
    [InlineData(FiveSkills, "", "alpha, bravo, charlie, delta", 1, 5, 90, 10, ScreeningVerdict.Borderline, 80)]
    [InlineData(FiveSkills, "", "alpha, bravo, charlie, delta", 1, 5, 95, 5, ScreeningVerdict.Failed, 80)]
    public void Verdict_FollowsThresholdsMissingSkillsAndExperience(
        string required, string niceToHave, string resume, int years, int minYears,
        int passingScore, int borderlineWidth, ScreeningVerdict expected, int expectedScore)
    {
        var requirements = Requirements(Split(required), Split(niceToHave), minYears, passingScore, borderlineWidth);

        var result = ResumeScorer.Evaluate(resume, years, requirements);

        Assert.Equal(expectedScore, result.Score);
        Assert.Equal(expected, result.Verdict);
    }

    [Fact]
    public void Result_ListsMissingSkillsByNameAndScoreIsNotAffectedByExperience()
    {
        var requirements = Requirements(["alpha", "bravo", "charlie"], ["delta", "echo"], minYears: 10);

        var lacking = ResumeScorer.Evaluate("alpha delta", 0, requirements);
        var enough = ResumeScorer.Evaluate("alpha delta", 10, requirements);
        var allFound = ResumeScorer.Evaluate("alpha bravo charlie delta echo", 10, requirements);

        Assert.Equal(["bravo", "charlie"], lacking.MissingRequiredSkills);
        Assert.Equal(["echo"], lacking.MissingNiceToHaveSkills);
        Assert.Equal(enough.Score, lacking.Score);
        Assert.Empty(allFound.MissingRequiredSkills);
        Assert.Empty(allFound.MissingNiceToHaveSkills);
    }

    [Fact]
    public void Evaluate_NormalizesCyrillicCInResume()
    {
        var requirements = new HireFlow.Domain.Vacancies.VacancyRequirements
        {
            RequiredSkills = [Skill("C#")],
            PassingScore = 70,
        };

        var result = ResumeScorer.Evaluate("Знаю С#", 5, requirements);

        Assert.Equal(ScreeningVerdict.Passed, result.Verdict);
    }
}
