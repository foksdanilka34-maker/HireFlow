using HireFlow.Domain.Candidates;

namespace HireFlow.Domain.Tests;

public class CandidateTests
{
    [Theory]
    [InlineData("79991234567")]
    [InlineData("+79991234567")]
    public void Constructor_StoresPhoneInE164(string input)
    {
        var candidate = new Candidate(1, "Иван", input);

        Assert.Equal("+79991234567", candidate.Phone);
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var candidate = new Candidate(42, "  Иван Петров ", "79991234567");

        Assert.Equal("Иван Петров", candidate.Name);
        Assert.Equal(42, candidate.TelegramId);
    }

    [Fact]
    public void Update_NormalizesPhoneTrimsNameAndKeepsCreatedAt()
    {
        var candidate = new Candidate(1, "Иван", "+79991234567");
        var createdAt = candidate.CreatedAt;
        var before = DateTime.UtcNow;

        candidate.Update("  Пётр ", "78885554433");

        Assert.Equal("Пётр", candidate.Name);
        Assert.Equal("+78885554433", candidate.Phone);
        Assert.Equal(createdAt, candidate.CreatedAt);
        Assert.InRange(candidate.UpdatedAt, before, DateTime.UtcNow);
    }
}
