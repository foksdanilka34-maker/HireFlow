using HireFlow.Domain.Screening;
using static HireFlow.Domain.Tests.TestData;

namespace HireFlow.Domain.Tests;

public class ResumeScorerTests
{
    // Названия вымышленных навыков, чтобы случайно не пересечься с обычными словами.
    private static readonly string[] Skills = ["alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel"];

    private static string[] First(int count) => Skills.Take(count).ToArray();

    // Текст резюме, в котором упомянуты первые count навыков.
    private static string ResumeWith(int count) => string.Join(", ", First(count));

    [Fact]
    public void Score_UsesEightyTwentyWeights()
    {
        // 2 из 2 обязательных (80) + 1 из 2 желательных (10).
        var requirements = Requirements(["alpha", "bravo"], ["charlie", "delta"]);

        var result = ResumeScorer.Evaluate("alpha bravo charlie", 5, requirements);

        Assert.Equal(90, result.Score);
    }

    [Fact]
    public void Score_UsesShareNotCount()
    {
        // Половина обязательных (40) + все желательные (20).
        var requirements = Requirements(["alpha", "bravo"], ["charlie"]);

        var result = ResumeScorer.Evaluate("alpha charlie", 5, requirements);

        Assert.Equal(60, result.Score);
    }

    [Fact]
    public void Score_WithoutNiceToHaveIsHundredTimesRequiredShare()
    {
        var requirements = Requirements(["alpha", "bravo", "charlie"]);

        var result = ResumeScorer.Evaluate("alpha bravo", 5, requirements);

        Assert.Equal(67, result.Score);
    }

    [Fact]
    public void Score_WithoutNiceToHaveAllFoundIsHundred()
    {
        var result = ResumeScorer.Evaluate("alpha bravo", 5, Requirements(["alpha", "bravo"]));

        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void Score_NothingFoundIsZero()
    {
        var result = ResumeScorer.Evaluate("ничего общего", 5, Requirements(["alpha"], ["bravo"]));

        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Score_OnlyNiceToHaveFoundGivesTwenty()
    {
        var result = ResumeScorer.Evaluate("bravo", 5, Requirements(["alpha"], ["bravo"]));

        Assert.Equal(20, result.Score);
    }

    [Fact]
    public void Score_RoundsHalfUp_WithoutNiceToHave()
    {
        // 5 из 8 = 62.5 -> 63 (банковское округление дало бы 62).
        var result = ResumeScorer.Evaluate(ResumeWith(5), 5, Requirements(First(8)));

        Assert.Equal(63, result.Score);
    }

    [Fact]
    public void Score_RoundsHalfUp_LowHalf()
    {
        // 1 из 8 = 12.5 -> 13.
        var result = ResumeScorer.Evaluate(ResumeWith(1), 5, Requirements(First(8)));

        Assert.Equal(13, result.Score);
    }

    [Fact]
    public void Score_RoundsHalfUp_WithNiceToHave()
    {
        // 80 + 20 * 1/8 = 82.5 -> 83.
        var requirements = Requirements(["alpha"], ["bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india"]);

        var result = ResumeScorer.Evaluate("alpha bravo", 5, requirements);

        Assert.Equal(83, result.Score);
    }

    [Fact]
    public void Verdict_PassedWhenScoreEqualsPassingScore()
    {
        // Все обязательные найдены, из желательных 1 из 2: балл 90, порог 90.
        var requirements = Requirements(["alpha"], ["bravo", "charlie"], passingScore: 90);

        var result = ResumeScorer.Evaluate("alpha bravo", 5, requirements);

        Assert.Equal(90, result.Score);
        Assert.Equal(ScreeningVerdict.Passed, result.Verdict);
    }

    [Fact]
    public void Verdict_NotPassedWhenScoreIsOneBelowPassingScore()
    {
        var requirements = Requirements(["alpha"], ["bravo", "charlie"], passingScore: 91);

        var result = ResumeScorer.Evaluate("alpha bravo", 5, requirements);

        Assert.Equal(90, result.Score);
        Assert.Equal(ScreeningVerdict.Borderline, result.Verdict);
    }

    [Fact]
    public void Verdict_BorderlineWhenExactlyOneRequiredSkillIsMissing()
    {
        // Балл 80 далеко от порога 100 и зоны нет (ширина 0): пограничный только из-за одного недостающего навыка.
        var requirements = Requirements(First(5), passingScore: 100, borderlineWidth: 0);

        var result = ResumeScorer.Evaluate(ResumeWith(4), 5, requirements);

        Assert.Equal(80, result.Score);
        Assert.Equal(ScreeningVerdict.Borderline, result.Verdict);
        Assert.Equal(["echo"], result.MissingRequiredSkills);
    }

    [Fact]
    public void Verdict_BorderlineWhenOnlyExperienceIsLacking()
    {
        var requirements = Requirements(["alpha", "bravo"], minYears: 3, passingScore: 70);

        var result = ResumeScorer.Evaluate("alpha bravo", 2, requirements);

        Assert.Equal(100, result.Score);
        Assert.False(result.ExperienceIsEnough);
        Assert.Empty(result.MissingRequiredSkills);
        Assert.Equal(ScreeningVerdict.Borderline, result.Verdict);
    }

    [Fact]
    public void Verdict_PassedWhenExperienceEqualsMinimum()
    {
        var requirements = Requirements(["alpha"], minYears: 3);

        var result = ResumeScorer.Evaluate("alpha", 3, requirements);

        Assert.True(result.ExperienceIsEnough);
        Assert.Equal(ScreeningVerdict.Passed, result.Verdict);
    }

    [Fact]
    public void Verdict_BorderlineWhenScoreEqualsPassingScoreMinusWidth()
    {
        // 3 из 5 найдено: балл 60, не хватает двух навыков; порог 70, ширина 10 -> 60 попадает в зону.
        var requirements = Requirements(First(5), passingScore: 70, borderlineWidth: 10);

        var result = ResumeScorer.Evaluate(ResumeWith(3), 5, requirements);

        Assert.Equal(60, result.Score);
        Assert.Equal(2, result.MissingRequiredSkills.Count);
        Assert.Equal(ScreeningVerdict.Borderline, result.Verdict);
    }

    [Fact]
    public void Verdict_FailedWhenScoreIsOneBelowBorderlineZone()
    {
        var requirements = Requirements(First(5), passingScore: 71, borderlineWidth: 10);

        var result = ResumeScorer.Evaluate(ResumeWith(3), 5, requirements);

        Assert.Equal(60, result.Score);
        Assert.Equal(ScreeningVerdict.Failed, result.Verdict);
    }

    [Fact]
    public void Verdict_FailedWhenTwoSkillsMissingAndScoreBelowZone()
    {
        var requirements = Requirements(First(5), passingScore: 90, borderlineWidth: 10);

        var result = ResumeScorer.Evaluate(ResumeWith(3), 5, requirements);

        Assert.Equal(ScreeningVerdict.Failed, result.Verdict);
    }

    [Fact]
    public void Verdict_BorderlineWhenSkillAndExperienceLackingButScoreInZone()
    {
        var requirements = Requirements(First(5), minYears: 5, passingScore: 90, borderlineWidth: 10);

        var result = ResumeScorer.Evaluate(ResumeWith(4), 1, requirements);

        Assert.Equal(80, result.Score);
        Assert.False(result.ExperienceIsEnough);
        Assert.Single(result.MissingRequiredSkills);
        Assert.Equal(ScreeningVerdict.Borderline, result.Verdict);
    }

    [Fact]
    public void Verdict_SkillAndExperienceLackingWithLowScoreIsFailed()
    {
        var requirements = Requirements(First(5), minYears: 5, passingScore: 95, borderlineWidth: 5);

        var result = ResumeScorer.Evaluate(ResumeWith(4), 1, requirements);

        // Балл 80 < 95 - 5, недостающих требований два (навык и опыт).
        Assert.Equal(ScreeningVerdict.Failed, result.Verdict);
    }

    [Fact]
    public void Score_IsNotAffectedByExperience()
    {
        var requirements = Requirements(["alpha", "bravo"], ["charlie"], minYears: 10);

        var lacking = ResumeScorer.Evaluate("alpha bravo", 0, requirements);
        var enough = ResumeScorer.Evaluate("alpha bravo", 10, requirements);

        Assert.Equal(enough.Score, lacking.Score);
        Assert.Equal(80, enough.Score);
    }

    [Fact]
    public void Result_ListsMissingRequiredAndNiceToHaveSkillsByName()
    {
        var requirements = Requirements(["alpha", "bravo", "charlie"], ["delta", "echo"]);

        var result = ResumeScorer.Evaluate("alpha delta", 5, requirements);

        Assert.Equal(["bravo", "charlie"], result.MissingRequiredSkills);
        Assert.Equal(["echo"], result.MissingNiceToHaveSkills);
    }

    [Fact]
    public void Result_ListsNoMissingSkillsWhenAllFound()
    {
        var result = ResumeScorer.Evaluate("alpha bravo", 5, Requirements(["alpha"], ["bravo"]));

        Assert.Empty(result.MissingRequiredSkills);
        Assert.Empty(result.MissingNiceToHaveSkills);
    }

    [Fact]
    public void Evaluate_FindsSkillBySynonymFromVacancy()
    {
        var requirements = new HireFlow.Domain.Vacancies.VacancyRequirements
        {
            RequiredSkills = [Skill("C#", ".NET", "dotnet")],
            PassingScore = 70,
        };

        var result = ResumeScorer.Evaluate("Пишу на dotnet", 5, requirements);

        Assert.Empty(result.MissingRequiredSkills);
        Assert.Equal(ScreeningVerdict.Passed, result.Verdict);
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
