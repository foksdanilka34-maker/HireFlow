using HireFlow.Bot.Api;

namespace HireFlow.Bot.Tests;

public class DialogQuestionsTests
{
    [Fact]
    public async Task PendingQuestionsAreAnsweredInOrderAndCommandsDoNotConsumeAnswer()
    {
        var h = new DialogHarness();
        var appId = Guid.NewGuid();
        h.Api.PendingQuestion = new PendingQuestionDto(appId, "Разработчик", 0, 2, "Какой формат?", DateTime.UtcNow.AddDays(7));
        var answers = new List<string>();
        h.Api.OnAnswer = answer =>
        {
            answers.Add(answer);
            if (answers.Count == 1)
            {
                h.Api.PendingQuestion = h.Api.PendingQuestion! with { Position = 1, Question = "Когда начать?" };
                return new AnswerQuestionDto(false, h.Api.PendingQuestion);
            }
            h.Api.PendingQuestion = null;
            return new AnswerQuestionDto(true, null);
        };

        await h.SendAsync("/questions");
        Assert.Contains("Какой формат?", h.Last);
        await h.SendAsync("/help");
        Assert.Empty(answers);
        await h.SendAsync("Удалённо");
        Assert.Contains("Когда начать?", h.Last);
        await h.SendAsync("Через месяц");
        Assert.Contains("ответы сохранены", h.Last);
        Assert.Equal(new[] { "Удалённо", "Через месяц" }, answers);
    }
}
