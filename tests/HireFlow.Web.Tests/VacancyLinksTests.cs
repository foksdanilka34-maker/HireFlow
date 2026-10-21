using System.Text.RegularExpressions;
using HireFlow.Web.Vacancies;

namespace HireFlow.Web.Tests;

public class VacancyLinksTests
{
    [Fact]
    public void Telegram_ContainsBotAnd32HexIdThatParsesBackToSameGuid()
    {
        var id = Guid.Parse("0197a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

        var link = VacancyLinks.Telegram("AiHireFlow_Bot", id);

        var match = Regex.Match(link, "^https://t\\.me/AiHireFlow_Bot\\?start=([0-9a-f]{32})$");
        Assert.True(match.Success, link);
        Assert.True(Guid.TryParse(match.Groups[1].Value, out var parsed));
        Assert.Equal(id, parsed);
    }
}
