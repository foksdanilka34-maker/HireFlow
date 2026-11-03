using HireFlow.Domain.Vacancies;

namespace HireFlow.Domain.JobApplications;

public class ApplicationQuestion
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; } = "";
    public string? Criterion { get; private set; }
    public string? Answer { get; private set; }
    private ApplicationQuestion() { }
    internal ApplicationQuestion(VacancyQuestion source, int position)
    {
        Id = Guid.CreateVersion7();
        Position = position;
        Text = source.Text.Trim();
        Criterion = source.Criterion?.Trim();
    }
    internal void SetAnswer(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 4000) throw new DomainException("Answer must contain 1–4000 characters.");
        if (Answer is not null) throw new DomainException("Question was already answered.");
        Answer = answer.Trim();
    }
}
