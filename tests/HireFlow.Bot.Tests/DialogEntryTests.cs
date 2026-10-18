using HireFlow.Bot.Api;
using HireFlow.Bot.Dialog;

namespace HireFlow.Bot.Tests;

// Вход в диалог: /start со ссылкой, подсказки на шаге «жду резюме», /help, размер файла, нечитаемый файл.
public class DialogEntryTests
{
    private readonly DialogHarness _bot = new();

    [Fact]
    public async Task Start_WithOpenVacancyLinksItAndShowsTitle()
    {
        var vacancy = _bot.AddVacancy("Тестировщик");

        await _bot.SendAsync($"/start {vacancy.Id}");

        Assert.Contains("«Тестировщик»", _bot.Last);
        Assert.Equal(vacancy.Id, _bot.Session.LinkedVacancyId);
        Assert.Equal(BotSteps.AwaitingResume, _bot.Session.Step);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("archived")]
    [InlineData("missing")]
    [InlineData("garbage")]
    public async Task Start_WithClosedOrInvalidLinkSaysClosedAndDoesNotLink(string kind)
    {
        var argument = kind switch
        {
            "closed" or "archived" => _bot.AddVacancy("Закрытая", status: kind).Id.ToString(),
            "missing" => Guid.NewGuid().ToString(),
            _ => "not-a-guid",
        };

        await _bot.SendAsync($"/start {argument}");

        Assert.Equal(BotTexts.ClosedLinked, _bot.Last);
        Assert.Null(_bot.Session.LinkedVacancyId);
        Assert.Equal(BotSteps.AwaitingResume, _bot.Session.Step);
    }

    [Fact]
    public async Task Start_ResetsPreviousLinkAndProgress()
    {
        var old = _bot.AddVacancy("Старая");
        _bot.SeedSession(BotSteps.ChoosingVacancy, new BotDraft { ResumeFileId = "f", VacancyIds = [old.Id] }, old.Id);

        await _bot.SendAsync("/start");

        Assert.Equal(BotSteps.AwaitingResume, _bot.Session.Step);
        Assert.Null(_bot.Session.LinkedVacancyId);
        Assert.Null(_bot.Session.Draft.ResumeFileId);
    }

    [Theory]
    [InlineData("Здравствуйте!")]
    [InlineData("привет, я хочу работать")]
    [InlineData("1")]
    public async Task AwaitingResume_AnyTextGivesHintAndKeepsStep(string text)
    {
        await _bot.SendAsync("/start");

        await _bot.SendAsync(text);

        Assert.Equal(BotTexts.SendResumeHint, _bot.Last);
        Assert.Equal(BotSteps.AwaitingResume, _bot.Session.Step);
        Assert.Empty(_bot.Api.MatchCalls);
    }

    [Fact]
    public async Task AwaitingResume_PhotoOrStickerGivesSameHint()
    {
        await _bot.SendAsync("/start");

        await _bot.SendNonTextAsync();

        Assert.Equal(BotTexts.SendResumeHint, _bot.Last);
        Assert.Equal(BotSteps.AwaitingResume, _bot.Session.Step);
    }

    [Theory]
    [InlineData(BotSteps.AwaitingResume, "Пришлите резюме файлом")]
    [InlineData(BotSteps.AwaitingExperience, "число лет")]
    [InlineData(BotSteps.AwaitingPhone, "номер телефона")]
    [InlineData(BotSteps.ConfirmingLinked, "«да»")]
    public async Task Help_ExplainsCurrentStepAndKeepsIt(string step, string expectedHint)
    {
        _bot.SeedSession(step, new BotDraft { ResumeFileId = "f" });
        var before = _bot.Api.Sessions[DialogHarness.UserId];

        await _bot.SendAsync("/help");

        Assert.Contains(expectedHint, _bot.Last);
        Assert.Contains("/status", _bot.Last);
        Assert.Equal(before, _bot.Api.Sessions[DialogHarness.UserId]);
        Assert.Equal(0, _bot.Api.PutSessionCalls);
    }

    [Theory]
    [InlineData(10 * 1024 * 1024 + 1, false)]
    [InlineData(10 * 1024 * 1024, true)]
    public async Task Document_OverTenMegabytesIsRejectedBeforeDownload(long size, bool processed)
    {
        await _bot.SendAsync("/start");

        await _bot.SendDocumentAsync(fileSize: size);

        Assert.Equal(processed, _bot.Chat.Downloads.Count == 1);
        Assert.Equal(processed ? 1 : 0, _bot.Api.MatchCalls.Count);
        if (!processed)
            Assert.Equal(BotTexts.FileTooBig, _bot.Last);
    }

    [Fact]
    public async Task Document_UnreadableByApiAsksForAnotherFileAndKeepsStep()
    {
        await _bot.SendAsync("/start");
        _bot.Api.OnMatches = _ => ApiResult<MatchesDto>.Failure(ApiStatus.BadRequest);

        await _bot.SendDocumentAsync();

        Assert.Equal(BotTexts.UnreadableFile, _bot.Last);
        Assert.Equal(BotSteps.AwaitingResume, _bot.Session.Step);
        Assert.Null(_bot.Session.Draft.ResumeFileId);
    }
}
