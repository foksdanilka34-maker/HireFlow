using System.ComponentModel.DataAnnotations;
using HireFlow.Domain.Vacancies;
using static HireFlow.Domain.Tests.TestData;

namespace HireFlow.Domain.Tests;

public class VacancyRequirementsTests
{
    private static List<ValidationResult> Validate(VacancyRequirements requirements)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(requirements, new ValidationContext(requirements), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_BorderlineWidthGreaterThanPassingScoreIsError()
    {
        var requirements = Requirements(["alpha"], passingScore: 5, borderlineWidth: 6);

        var errors = requirements.Validate(new ValidationContext(requirements)).ToList();

        var error = Assert.Single(errors);
        Assert.Contains(nameof(VacancyRequirements.BorderlineWidth), error.MemberNames);
    }

    [Theory]
    [InlineData(70, 70)]
    [InlineData(70, 0)]
    [InlineData(0, 0)]
    [InlineData(100, 10)]
    public void Validate_WidthNotGreaterThanPassingScoreIsValid(int passingScore, int width)
    {
        var requirements = Requirements(["alpha"], passingScore: passingScore, borderlineWidth: width);

        Assert.Empty(requirements.Validate(new ValidationContext(requirements)));
        Assert.Empty(Validate(requirements));
    }

    [Fact]
    public void Validator_ReportsBorderlineWidthError()
    {
        var requirements = Requirements(["alpha"], passingScore: 5, borderlineWidth: 10);

        var results = Validate(requirements);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(VacancyRequirements.BorderlineWidth)));
    }

    [Fact]
    public void Default_BorderlineWidthIsTen() =>
        Assert.Equal(10, new VacancyRequirements().BorderlineWidth);

    [Fact]
    public void Validator_EmptyRequiredSkillsIsError()
    {
        var requirements = new VacancyRequirements { PassingScore = 70 };

        var results = Validate(requirements);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(VacancyRequirements.RequiredSkills)));
    }
}
