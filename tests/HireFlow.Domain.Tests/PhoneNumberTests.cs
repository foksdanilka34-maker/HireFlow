using HireFlow.Domain.Candidates;

namespace HireFlow.Domain.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("+7 (999) 123-45-67", "+79991234567")]
    [InlineData("8 999 123 45 67", "+79991234567")]
    [InlineData("89991234567", "+79991234567")]
    [InlineData("79991234567", "+79991234567")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    [InlineData("9991234567", "+79991234567")]
    [InlineData("7712345678", null)]
    [InlineData("4510123456", null)]
    [InlineData("4420794609", null)]
    [InlineData("123", null)]
    [InlineData("abc", null)]
    [InlineData("1234567890123456", null)]
    [InlineData("+799912345671990", null)]
    [InlineData("+7 999 abc", null)]
    public void TryNormalize_ReturnsE164OrRejectsNonPhone(string input, string? expected)
    {
        var ok = PhoneNumber.TryNormalize(input, out var e164);

        Assert.Equal(expected is not null, ok);
        Assert.Equal(expected ?? "", e164);
    }
}
