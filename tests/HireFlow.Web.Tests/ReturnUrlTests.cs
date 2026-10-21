using HireFlow.Web.Auth;

namespace HireFlow.Web.Tests;

public class ReturnUrlTests
{
    // Локальные пути проходят, всё остальное заменяется на /vacancies (браузер выбрасывает TAB/CR/LF, поэтому они опасны).
    [Theory]
    [InlineData("/vacancies?x=1", "/vacancies?x=1")]
    [InlineData("/vacancies/abc/edit", "/vacancies/abc/edit")]
    [InlineData(null, "/vacancies")]
    [InlineData("", "/vacancies")]
    [InlineData("//evil.example", "/vacancies")]
    [InlineData("/\\evil.example", "/vacancies")]
    [InlineData("/\t/evil.example", "/vacancies")]
    [InlineData("/\n/evil.example", "/vacancies")]
    [InlineData("https://evil.example", "/vacancies")]
    [InlineData("/login", "/vacancies")]
    [InlineData("/login?returnUrl=/x", "/vacancies")]
    public void Safe_AllowsOnlyLocalPaths(string? url, string expected)
    {
        Assert.Equal(expected, ReturnUrl.Safe(url));
    }
}
