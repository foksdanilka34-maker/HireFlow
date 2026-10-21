using HireFlow.Domain.Recruiters;

namespace HireFlow.Domain.Tests;

public class RecruiterTests
{
    [Theory]
    [InlineData(" Anna ", "anna")]
    [InlineData("ANNA", "anna")]
    [InlineData("a.b_c-9", "a.b_c-9")]
    public void NormalizeLogin_TrimsAndLowercases(string login, string expected)
    {
        Assert.Equal(expected, Recruiter.NormalizeLogin(login));
    }

    [Fact]
    public void NormalizeLogin_AcceptsMaxLengthAndRejectsOneMore()
    {
        var max = new string('a', Recruiter.LoginMaxLength);

        Assert.Equal(max, Recruiter.NormalizeLogin(max));
        Assert.Throws<DomainException>(() => Recruiter.NormalizeLogin(max + "a"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("an na")]
    [InlineData("анна")]
    [InlineData("anna@mail")]
    public void NormalizeLogin_EmptyOrWithForbiddenCharsThrows(string login)
    {
        Assert.Throws<DomainException>(() => Recruiter.NormalizeLogin(login));
    }
}
