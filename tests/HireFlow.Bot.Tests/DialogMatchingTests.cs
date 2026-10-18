using HireFlow.Bot.Api;
using HireFlow.Bot.Dialog;

namespace HireFlow.Bot.Tests;

// Опыт, показ подбора, пагинация, выбор номером, «все» и вакансия по ссылке.
public class DialogMatchingTests
{
    private readonly DialogHarness _bot = new();

    public DialogMatchingTests() => _bot.Api.CandidateExists = true;

    [Fact]
    public async Task ExperienceFoundInResume_ShowsListAndAppliesWithThatExperience()
    {
        var vacancies = _bot.AddVacancies(2);
        _bot.SetMatches(vacancies, years: 7);

        await _bot.SendDocumentAsync();

        Assert.Contains("1. A (QA)", _bot.Last);
        Assert.Contains("2. B (QA)", _bot.Last);
        Assert.Equal(BotSteps.ChoosingVacancy, _bot.Session.Step);
        Assert.Equal([vacancies[0].Id, vacancies[1].Id], _bot.Session.Draft.VacancyIds);
        Assert.Null(_bot.Api.MatchCalls.Single().Years);

        await _bot.SendAsync("1");

        Assert.Equal(7, _bot.Api.ApplicationCalls.Single().YearsOfExperience);
    }

    [Fact]
    public async Task ExperienceRequired_AsksQuestionAndKeepsFileForNextStep()
    {
        _bot.SetMatches([], years: null, experienceRequired: true);

        await _bot.SendDocumentAsync("file-xyz");

        Assert.Equal(BotTexts.AskExperience, _bot.Last);
        Assert.Equal(BotSteps.AwaitingExperience, _bot.Session.Step);
        Assert.Equal("file-xyz", _bot.Session.Draft.ResumeFileId);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("60")]
    [InlineData("51")]
    [InlineData("-3")]
    [InlineData("три года")]
    public async Task AwaitingExperience_InvalidAnswerRepeatsQuestionWithoutMatching(string answer)
    {
        _bot.SetMatches([], years: null, experienceRequired: true);
        await _bot.SendDocumentAsync();

        await _bot.SendAsync(answer);

        Assert.Equal(BotTexts.ExperienceHint, _bot.Last);
        Assert.Equal(BotSteps.AwaitingExperience, _bot.Session.Step);
        Assert.Single(_bot.Api.MatchCalls);
    }

    [Theory]
    [InlineData("5 лет", 5)]
    [InlineData("5", 5)]
    [InlineData("1 год", 1)]
    [InlineData("0", 0)]
    [InlineData("50", 50)]
    public async Task AwaitingExperience_NumberRepeatsMatchingWithThatExperience(string answer, int expected)
    {
        var vacancy = _bot.AddVacancy("Backend");
        _bot.SetMatches([vacancy], years: null, experienceRequired: true);
        _bot.Chat.Files["file-1"] = [9, 8, 7];
        await _bot.SendDocumentAsync("file-1");

        await _bot.SendAsync(answer);

        Assert.Equal(2, _bot.Api.MatchCalls.Count);
        var (file, years) = _bot.Api.MatchCalls[1];
        Assert.Equal(expected, years);
        Assert.Equal([9, 8, 7], file);
        Assert.Equal(BotSteps.ChoosingVacancy, _bot.Session.Step);
        Assert.Contains("1. Backend (QA)", _bot.Last);

        await _bot.SendAsync("1");

        Assert.Equal(expected, _bot.Api.ApplicationCalls.Single().YearsOfExperience);
    }

    [Fact]
    public async Task List_ShowsThreeAtATimeAndNumberingContinuesAfterMore()
    {
        var vacancies = _bot.AddVacancies(5);
        _bot.SetMatches(vacancies);
        await _bot.SendDocumentAsync();

        Assert.Contains("1. A (QA)", _bot.Last);
        Assert.Contains("3. C (QA)", _bot.Last);
        Assert.DoesNotContain("4.", _bot.Last);
        Assert.Contains("ещё", _bot.Last);

        await _bot.SendAsync("ещё");

        Assert.Contains("4. D (QA)", _bot.Last);
        Assert.Contains("5. E (QA)", _bot.Last);
        Assert.DoesNotContain("1. A", _bot.Last);
        Assert.DoesNotContain("ещё", _bot.Last);

        await _bot.SendAsync("5");

        Assert.Equal(vacancies[4].Id, _bot.Api.ApplicationCalls.Single().VacancyId);
    }

    [Theory]
    [InlineData("1", 0)]
    [InlineData("2", 1)]
    [InlineData("3", 2)]
    public async Task List_NumberAppliesToTheChosenVacancy(string number, int index)
    {
        var vacancies = _bot.AddVacancies(4);
        _bot.SetMatches(vacancies);
        await _bot.SendDocumentAsync();

        await _bot.SendAsync(number);

        Assert.Equal(vacancies[index].Id, _bot.Api.ApplicationCalls.Single().VacancyId);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("0")]
    [InlineData("abc")]
    public async Task List_UnknownNumberOrTextGivesHintAndDoesNotApply(string answer)
    {
        _bot.SetMatches(_bot.AddVacancies(2));
        await _bot.SendDocumentAsync();

        await _bot.SendAsync(answer);

        Assert.Empty(_bot.Api.ApplicationCalls);
        Assert.Contains("номер", _bot.Last);
        Assert.Equal(BotSteps.ChoosingVacancy, _bot.Session.Step);
    }

    [Fact]
    public async Task EmptyMatches_OffersAllAndAllListsOpenVacanciesForChoosing()
    {
        _bot.SetMatches([]);
        var open = _bot.AddVacancies(2);
        _bot.Api.OpenVacancies.AddRange(open);

        await _bot.SendDocumentAsync();
        Assert.Contains("«все»", _bot.Last);
        Assert.Equal(BotSteps.ChoosingVacancy, _bot.Session.Step);

        await _bot.SendAsync("все");

        Assert.Equal(1, _bot.Api.OpenVacanciesRequests);
        Assert.Contains("1. A (QA)", _bot.Last);
        Assert.Contains("2. B (QA)", _bot.Last);

        await _bot.SendAsync("2");

        Assert.Equal(open[1].Id, _bot.Api.ApplicationCalls.Single().VacancyId);
    }

    [Fact]
    public async Task LinkedVacancyThatMatches_AsksConfirmationAndYesAppliesToIt()
    {
        var linked = _bot.AddVacancy("Ссылочная");
        var other = _bot.AddVacancy("Другая");
        _bot.SetMatches([other, linked]);
        await _bot.SendAsync($"/start {linked.Id}");

        await _bot.SendDocumentAsync();

        Assert.Equal(BotTexts.ConfirmLinked("Ссылочная"), _bot.Last);
        Assert.Equal(BotSteps.ConfirmingLinked, _bot.Session.Step);

        await _bot.SendAsync("да");

        Assert.Equal(linked.Id, _bot.Api.ApplicationCalls.Single().VacancyId);
    }

    [Fact]
    public async Task LinkedVacancyThatDoesNotMatch_ListsOthersAndZeroAppliesToIt()
    {
        var linked = _bot.AddVacancy("Ссылочная");
        var other = _bot.AddVacancy("Другая");
        _bot.SetMatches([other]);
        await _bot.SendAsync($"/start {linked.Id}");

        await _bot.SendDocumentAsync();

        Assert.Contains("1. Другая (QA)", _bot.Last);
        Assert.Contains("0 — всё равно откликнуться на «Ссылочная»", _bot.Last);
        Assert.Equal(BotSteps.ChoosingVacancy, _bot.Session.Step);

        await _bot.SendAsync("0");

        Assert.Equal(linked.Id, _bot.Api.ApplicationCalls.Single().VacancyId);
    }
}
