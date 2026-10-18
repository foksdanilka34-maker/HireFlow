using HireFlow.Domain.Candidates;

namespace HireFlow.Domain.Tests;

public class CandidateTests
{
    [Fact]
    public void Constructor_AddsPlusToPhoneWithoutIt()
    {
        var candidate = new Candidate(1, "Иван", "79991234567");

        Assert.Equal("+79991234567", candidate.Phone);
    }

    [Fact]
    public void Constructor_KeepsPhoneThatAlreadyHasPlus()
    {
        var candidate = new Candidate(1, "Иван", "+79991234567");

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

        candidate.Update("  Пётр ", "78885554433");

        Assert.Equal("Пётр", candidate.Name);
        Assert.Equal("+78885554433", candidate.Phone);
        Assert.Equal(createdAt, candidate.CreatedAt);
        Assert.True(candidate.UpdatedAt >= createdAt);
    }

    [Fact]
    public void Update_KeepsPhoneThatAlreadyHasPlus()
    {
        var candidate = new Candidate(1, "Иван", "79991234567");

        candidate.Update("Иван", "+70001112233");

        Assert.Equal("+70001112233", candidate.Phone);
    }
}
