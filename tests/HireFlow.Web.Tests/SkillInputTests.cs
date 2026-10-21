using HireFlow.Web.Api;
using HireFlow.Web.Vacancies;

namespace HireFlow.Web.Tests;

public class SkillInputTests
{
    [Theory]
    [InlineData(" Golang, goland,,GOLANG ", new[] { "Golang", "goland" })]
    [InlineData("", new string[0])]
    [InlineData("  ,  , ", new string[0])]
    [InlineData(null, new string[0])]
    public void ParseSynonyms_TrimsDropsEmptyAndDuplicatesIgnoringCase(string? text, string[] expected)
    {
        Assert.Equal(expected, SkillInput.ParseSynonyms(text));
    }

    [Fact]
    public void ToSkills_DropsRowsWithoutNameAndTrimsName()
    {
        var rows = new[]
        {
            new SkillRow { Name = "  C#  ", Synonyms = "csharp, CSharp" },
            new SkillRow { Name = "   ", Synonyms = "orphan" },
            new SkillRow { Name = "", Synonyms = "" },
            new SkillRow { Name = "SQL", Synonyms = "" },
        };

        var skills = SkillInput.ToSkills(rows);

        Assert.Equal(2, skills.Count);
        Assert.Equal("C#", skills[0].Name);
        Assert.Equal(["csharp"], skills[0].Synonyms);
        Assert.Equal("SQL", skills[1].Name);
        Assert.Empty(skills[1].Synonyms);
    }
}
