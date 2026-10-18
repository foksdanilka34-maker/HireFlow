using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Candidates;
using HireFlow.Application.JobApplications;

namespace HireFlow.Api.Tests;

// Статусы отклика так, как их видит кандидат: об отказе он узнаёт только после отправки уведомления.
public class CandidateApplicationsApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    private async Task<List<CandidateApplicationResponse>> GetApplicationsAsync(long telegramId)
    {
        var response = await Client.GetAsync($"/candidates/{telegramId}/applications");
        response.EnsureSuccessStatusCode();
        return await ReadAsync<List<CandidateApplicationResponse>>(response);
    }

    // Автоотказ и отказ рекрутера: кандидат видит «на рассмотрении», пока уведомление об отказе не отправлено.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rejection_IsInReviewUntilRejectionIsSent(bool autoRejected)
    {
        var telegramId = NewTelegramId();
        var title = Unique("Reject ");
        ApplicationResponse application;
        if (autoRejected)
        {
            application = await CreateAutoRejectedApplicationAsync(title, telegramId);
        }
        else
        {
            application = await CreateReviewApplicationAsync(title, telegramId);
            (await DecideAsync(application.Id, "reject")).EnsureSuccessStatusCode();
        }
        var notification = Assert.Single(await GetNotificationsAsync(application.Id));

        var before = Assert.Single(await GetApplicationsAsync(telegramId));
        Assert.Equal(CandidateStatus.InReview, before.Status);
        Assert.Equal(title, before.VacancyTitle);
        Assert.Equal(application.Id, before.Id);

        (await Client.PostAsync($"/internal/notifications/{notification.Id}/sent", null)).EnsureSuccessStatusCode();

        var after = Assert.Single(await GetApplicationsAsync(telegramId));
        Assert.Equal(CandidateStatus.Rejected, after.Status);
    }

    [Fact]
    public async Task Invited_IsShownAsInvited()
    {
        var telegramId = NewTelegramId();
        var application = await CreateReviewApplicationAsync(telegramId: telegramId);
        (await DecideAsync(application.Id, "invite")).EnsureSuccessStatusCode();

        Assert.Equal(CandidateStatus.Invited, Assert.Single(await GetApplicationsAsync(telegramId)).Status);
    }

    [Fact]
    public async Task Applications_AreNewestFirstAndBelongToTheCandidateOnly()
    {
        var telegramId = NewTelegramId();
        var candidate = await CreateCandidateAsync(telegramId);
        var skill = Unique("s");
        var (first, second) = (await CreateVacancyAsync([skill]), await CreateVacancyAsync([skill]));
        var other = await CreateCandidateAsync();
        var a1 = await ApplyOkAsync(candidate.TelegramId, first.Id, ResumeWith(skill));
        var a2 = await ApplyOkAsync(candidate.TelegramId, second.Id, ResumeWith(skill));
        await ApplyOkAsync(other.TelegramId, first.Id, ResumeWith(skill));

        var items = await GetApplicationsAsync(telegramId);

        Assert.Equal([a2.Id, a1.Id], items.Select(i => i.Id).ToList());
    }

    [Fact]
    public async Task Applications_ContainVacancyIdOfEachApplication()
    {
        var telegramId = NewTelegramId();
        var candidate = await CreateCandidateAsync(telegramId);
        var skill = Unique("s");
        var (first, second) = (await CreateVacancyAsync([skill]), await CreateVacancyAsync([skill]));
        var a1 = await ApplyOkAsync(candidate.TelegramId, first.Id, ResumeWith(skill));
        var a2 = await ApplyOkAsync(candidate.TelegramId, second.Id, ResumeWith(skill));

        var items = await GetApplicationsAsync(telegramId);

        Assert.Equal(first.Id, items.Single(i => i.Id == a1.Id).VacancyId);
        Assert.Equal(second.Id, items.Single(i => i.Id == a2.Id).VacancyId);
    }
}
