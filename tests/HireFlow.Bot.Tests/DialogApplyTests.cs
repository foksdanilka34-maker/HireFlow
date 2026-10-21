using HireFlow.Bot.Api;
using HireFlow.Bot.Dialog;

namespace HireFlow.Bot.Tests;

// Отклик: телефон, ответы API 201/400/409 и команда /status.
public class DialogApplyTests
{
    private readonly DialogHarness _bot = new();

    // Доводит диалог до списка из одной вакансии «A».
    private async Task<VacancyDto> ReachListAsync(bool phoneFound, bool candidateExists)
    {
        var vacancy = _bot.AddVacancy("A");
        _bot.Api.CandidateExists = candidateExists;
        _bot.SetMatches([vacancy], phoneFound: phoneFound);
        await _bot.SendDocumentAsync();
        return vacancy;
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public async Task Apply_AsksForPhoneOnlyForNewCandidateWithoutPhoneInResume(
        bool candidateExists, bool phoneFound, bool asksPhone)
    {
        await ReachListAsync(phoneFound, candidateExists);

        await _bot.SendAsync("1");

        Assert.Equal(asksPhone, _bot.Last == BotTexts.AskPhone);
        Assert.Equal(asksPhone ? 0 : 1, _bot.Api.ApplicationCalls.Count);
        Assert.Equal(asksPhone ? BotSteps.AwaitingPhone : BotSteps.ChoosingVacancy, _bot.Session.Step);
    }

    [Fact]
    public async Task Apply_InvalidPhoneIsRetriedThenValidPhoneIsSentWithName()
    {
        var vacancy = await ReachListAsync(phoneFound: false, candidateExists: false);
        await _bot.SendAsync("1");
        _bot.Api.OnCreateApplication = call => call.Phone == "не телефон"
            ? ApiResult<ApplicationCreatedDto>.Failure(ApiStatus.BadRequest)
            : ApiResult<ApplicationCreatedDto>.Ok(new ApplicationCreatedDto(Guid.NewGuid()));

        await _bot.SendAsync("не телефон");

        Assert.Equal(BotTexts.InvalidPhone, _bot.Last);
        Assert.Equal(BotSteps.AwaitingPhone, _bot.Session.Step);

        await _bot.SendAsync("+7 999 123-45-67");

        var call = _bot.Api.ApplicationCalls[^1];
        Assert.Equal("+7 999 123-45-67", call.Phone);
        Assert.Equal(vacancy.Id, call.VacancyId);
        Assert.Equal("Иван Петров", call.Name);
        Assert.Equal(DialogHarness.UserId, call.TelegramId);
        Assert.Equal(BotTexts.Applied("A"), _bot.Last);
        Assert.Equal(BotSteps.ChoosingVacancy, _bot.Session.Step);
    }

    [Fact]
    public async Task Apply_AcceptedThenSessionSaveFailsStillTellsApplicationAccepted()
    {
        await ReachListAsync(phoneFound: true, candidateExists: true);
        _bot.Api.FailPutSession = true;

        await _bot.SendAsync("1");

        Assert.Equal(BotTexts.Applied("A"), _bot.Last);
        Assert.Single(_bot.Api.ApplicationCalls);
    }

    [Fact]
    public async Task SessionSaveFailureWithoutApplicationAnswersError()
    {
        _bot.Api.CandidateExists = true;
        _bot.SetMatches(_bot.AddVacancies(4));
        await _bot.SendDocumentAsync();
        _bot.Api.FailPutSession = true;

        await _bot.SendAsync("ещё");

        Assert.Equal(BotTexts.Error, _bot.Last);
        Assert.Empty(_bot.Api.ApplicationCalls);
    }

    [Fact]
    public async Task Apply_SuccessKeepsListForNextChoice()
    {
        var vacancies = _bot.AddVacancies(2);
        _bot.Api.CandidateExists = true;
        _bot.SetMatches(vacancies);
        await _bot.SendDocumentAsync();

        await _bot.SendAsync("1");
        await _bot.SendAsync("2");

        Assert.Equal([vacancies[0].Id, vacancies[1].Id], _bot.Api.ApplicationCalls.Select(c => c.VacancyId));
        Assert.Equal([vacancies[0].Id, vacancies[1].Id], _bot.Session.Draft.VacancyIds);
    }

    [Fact]
    public async Task Apply_ConflictOnExistingApplicationShowsItsStatus()
    {
        var vacancy = await ReachListAsync(phoneFound: true, candidateExists: true);
        _bot.Api.Applications.Add(new CandidateApplicationDto(Guid.NewGuid(), vacancy.Id, "A", CandidateStatus.Invited));
        _bot.Api.OnCreateApplication = _ => ApiResult<ApplicationCreatedDto>.Failure(ApiStatus.Conflict);

        await _bot.SendAsync("1");

        Assert.Equal("Вы уже откликались на «A», статус: Приглашение на созвон.", _bot.Last);
    }

    [Fact]
    public async Task Apply_ConflictWithoutExistingApplicationMeansVacancyClosed()
    {
        var closing = _bot.AddVacancy("Closing");
        var other = _bot.AddVacancy("Other");
        _bot.Api.CandidateExists = true;
        _bot.SetMatches([closing, other]);
        await _bot.SendDocumentAsync();
        _bot.Api.Applications.Add(new CandidateApplicationDto(Guid.NewGuid(), Guid.NewGuid(), "Unrelated", CandidateStatus.InReview));
        _bot.Api.OnCreateApplication = _ => ApiResult<ApplicationCreatedDto>.Failure(ApiStatus.Conflict);

        await _bot.SendAsync("1");

        Assert.DoesNotContain("уже откликались", _bot.AllReplies);
        Assert.Contains(BotTexts.Closed("Closing"), _bot.AllReplies);
        Assert.Contains("1. Other (QA)", _bot.Last);
        Assert.DoesNotContain("Closing (QA)", _bot.Last);
        Assert.Equal([other.Id], _bot.Session.Draft.VacancyIds);
    }

    [Fact]
    public async Task Status_ShowsCandidateFacingStatusTexts()
    {
        _bot.Api.Applications.AddRange(
        [
            new CandidateApplicationDto(Guid.NewGuid(), Guid.NewGuid(), "Первая", CandidateStatus.InReview),
            new CandidateApplicationDto(Guid.NewGuid(), Guid.NewGuid(), "Вторая", CandidateStatus.Invited),
            new CandidateApplicationDto(Guid.NewGuid(), Guid.NewGuid(), "Третья", CandidateStatus.Rejected),
        ]);
        _bot.SeedSession(BotSteps.AwaitingExperience);

        await _bot.SendAsync("/status");

        Assert.Equal("Первая — На рассмотрении\nВторая — Приглашение на созвон\nТретья — Отказ", _bot.Last);
        Assert.Equal(BotSteps.AwaitingExperience, _bot.Session.Step);
        Assert.Equal(0, _bot.Api.PutSessionCalls);
    }
}
