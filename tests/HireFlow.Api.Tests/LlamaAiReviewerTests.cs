using System.Net;
using System.Text;
using System.Text.Json;
using HireFlow.Application.Ai;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;
using HireFlow.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace HireFlow.Api.Tests;

public class LlamaAiReviewerTests
{
    private sealed class FakeHandler(string modelContent) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            var response = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = modelContent } } } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private static (JobApplication Application, Vacancy Vacancy) Example()
    {
        var requirements = new VacancyRequirements { RequiredSkills = [new Skill { Name = "C#" }, new Skill { Name = "SQL" }], PassingScore = 70, BorderlineWidth = 10 };
        var vacancy = new Vacancy("private@example.com", "secret name", "secret description", requirements, 90);
        var application = new JobApplication(Guid.NewGuid(), Guid.NewGuid(), 3, "secret path", "secret.pdf", new string('a', 64), "Name Private +79990000000 private@example.com");
        application.ApplyScreening(new ScreeningResult(50, ScreeningVerdict.Borderline, ["SQL"], [], true), TimeSpan.Zero, true);
        return (application, vacancy);
    }

    [Fact]
    public void ExcerptsKeepSkillContextAndRemoveRecognizablePersonalData()
    {
        var requirements = new VacancyRequirements { RequiredSkills = [new Skill { Name = "Kubernetes" }] };
        var resume = "Имя: Иван Петров\nEmail: ivan@example.com\nАдрес: Москва, ул. Ленина 1\nИван Петров: хочу изучить Kubernetes, телефон +7 999 123-45-67.";
        var excerpt = ResumeExcerptSanitizer.Extract(resume, requirements, "Иван Петров", "+79991234567");
        Assert.Contains("Kubernetes", excerpt);
        Assert.Contains("изучить", excerpt);
        Assert.DoesNotContain("Иван", excerpt);
        Assert.DoesNotContain("Петров", excerpt);
        Assert.DoesNotContain("ivan@example.com", excerpt);
        Assert.DoesNotContain("Ленина", excerpt);
        Assert.DoesNotContain("999 123", excerpt);
    }

    [Fact]
    public async Task AnswerReviewRedactsContactsAndKeepsExpectedCriterion()
    {
        var handler = new FakeHandler("{\"recommendation\":\"match\",\"score\":100,\"explanation\":\"Формат подходит.\"}");
        var reviewer = new LlamaAiReviewer(new HttpClient(handler), Options.Create(new AiReviewOptions()));
        var application = new JobApplication(Guid.NewGuid(), Guid.NewGuid(), 3, "resume.pdf", "resume.pdf", new string('a', 64), "C#",
            [new VacancyQuestion { Text = "Какой формат работы?", Criterion = "удалённо" }]);
        application.ApplyScreening(new ScreeningResult(100, ScreeningVerdict.Passed, [], [], true), TimeSpan.Zero, true);
        application.CompleteAiReview(new("pass", 100, "Подходит", "test", "v1", DateTime.UtcNow));
        application.AnswerNextQuestion("Удалённо, мой email: private@example.com и телефон +7 999 123-45-67", true);
        var result = await reviewer.ReviewAnswersAsync(application, "Иван Петров", "+79991234567", CancellationToken.None);
        Assert.Equal("match", result.Recommendation);
        Assert.DoesNotContain("private@example.com", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("999 123", handler.Body);
        Assert.Contains("answers-v1", result.PromptVersion);
    }

    [Fact]
    public async Task MissingCriteriaCannotBeReportedAsCompleteMatch()
    {
        var reviewer = new LlamaAiReviewer(new HttpClient(new FakeHandler("{\"recommendation\":\"match\",\"score\":100,\"explanation\":\"Всё подходит.\"}")), Options.Create(new AiReviewOptions()));
        var application = new JobApplication(Guid.NewGuid(), Guid.NewGuid(), 3, "resume.pdf", "resume.pdf", new string('a', 64), "C#",
            [new VacancyQuestion { Text = "Какой формат работы?" }]);
        application.ApplyScreening(new ScreeningResult(100, ScreeningVerdict.Passed, [], [], true), TimeSpan.Zero, true);
        application.CompleteAiReview(new("pass", 100, "Подходит", "test", "v1", DateTime.UtcNow));
        application.AnswerNextQuestion("Удалённо", true);
        var result = await reviewer.ReviewAnswersAsync(application, null, null, CancellationToken.None);
        Assert.Equal("attention", result.Recommendation);
        Assert.Contains("критерии не указаны", result.Explanation);
    }

    [Fact]
    public async Task SendsOnlyAllowedSignalsAndValidatesResponse()
    {
        var handler = new FakeHandler("{\"recommendation\":\"review\",\"score\":50,\"explanation\":\"Нужно уточнить SQL.\"}");
        var reviewer = new LlamaAiReviewer(new HttpClient(handler), Options.Create(new AiReviewOptions()));
        var (application, vacancy) = Example();
        var result = await reviewer.ReviewAsync(application, vacancy.Requirements, "Name Private", "+79990000000", CancellationToken.None);
        Assert.Equal("review", result.Recommendation);
        Assert.DoesNotContain("private@example.com", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("79990000000", handler.Body);
        Assert.DoesNotContain("secret", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C#", handler.Body);
        Assert.Contains("json_schema", handler.Body);
        Assert.Contains("enable_thinking", handler.Body);
    }

    [Fact]
    public async Task RussianEvidenceIsNotExpandedIntoUnicodeEscapesInsidePrompt()
    {
        var handler = new FakeHandler("{\"recommendation\":\"review\",\"score\":50,\"explanation\":\"Требует внимания.\"}");
        var reviewer = new LlamaAiReviewer(new HttpClient(handler), Options.Create(new AiReviewOptions()));
        var requirements = new VacancyRequirements { RequiredSkills = [new Skill { Name = "RabbitMQ" }] };
        var application = new JobApplication(Guid.NewGuid(), Guid.NewGuid(), 3, "resume.pdf", "resume.pdf", new string('a', 64), "RabbitMQ только изучаю, опыта нет.");
        application.ApplyScreening(new ScreeningResult(50, ScreeningVerdict.Borderline, [], [], true), TimeSpan.Zero, true);
        await reviewer.ReviewAsync(application, requirements, null, null, CancellationToken.None);
        using var request = JsonDocument.Parse(handler.Body);
        var prompt = request.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
        Assert.Contains("изучаю", prompt);
        Assert.DoesNotContain("\\u0438", prompt);
    }

    [Theory]
    [InlineData("180000 рублей в месяц", "difference")]
    [InlineData("150000 рублей в месяц", "match")]
    [InlineData("120000 рублей в месяц", "match")]
    [InlineData("от 180000 рублей в месяц", "match")]
    public async Task ExplicitSalaryLimitCorrectsOnlyUnambiguousExcess(string answer, string expected)
    {
        var reviewer = new LlamaAiReviewer(new HttpClient(new FakeHandler("{\"recommendation\":\"match\",\"score\":100,\"explanation\":\"Всё подходит.\"}")), Options.Create(new AiReviewOptions()));
        var application = new JobApplication(Guid.NewGuid(), Guid.NewGuid(), 3, "resume.pdf", "resume.pdf", new string('a', 64), "C#",
            [new VacancyQuestion { Text = "Каковы ваши зарплатные ожидания?", Criterion = "До 150000 рублей в месяц" }]);
        application.ApplyScreening(new ScreeningResult(100, ScreeningVerdict.Passed, [], [], true), TimeSpan.Zero, true);
        application.CompleteAiReview(new("pass", 100, "Подходит", "test", "v1", DateTime.UtcNow));
        application.AnswerNextQuestion(answer, true);
        var result = await reviewer.ReviewAnswersAsync(application, null, null, CancellationToken.None);
        Assert.Equal(expected, result.Recommendation);
        if (expected == "difference") Assert.Contains("выше", result.Explanation);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"recommendation\":\"reject\",\"score\":50,\"explanation\":\"x\"}")]
    [InlineData("{\"recommendation\":\"pass\",\"score\":101,\"explanation\":\"x\"}")]
    [InlineData("{\"recommendation\":\"pass\",\"score\":50,\"explanation\":\"x\",\"extra\":true}")]
    [InlineData("{\"recommendation\":\"pass\",\"recommendation\":\"review\",\"score\":50,\"explanation\":\"x\"}")]
    public async Task RejectsInvalidModelResponses(string content)
    {
        var reviewer = new LlamaAiReviewer(new HttpClient(new FakeHandler(content)), Options.Create(new AiReviewOptions()));
        var (application, vacancy) = Example();
        await Assert.ThrowsAnyAsync<Exception>(() => reviewer.ReviewAsync(application, vacancy.Requirements, "Name Private", "+79990000000", CancellationToken.None));
    }
}
