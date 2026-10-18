using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Domain;
using HireFlow.Domain.JobApplications;
using HireFlow.Domain.Notifications;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Оптимистичная блокировка на системной колонке xmin: проверяется на настоящем PostgreSQL двумя независимыми DbContext.
public class ConcurrencyTests(ApiAppFixture app) : ApiTestBase(app)
{
    // Загружает отклик вместе с историей и уведомлениями в отдельном scope, как это делает сервис.
    private static Task<JobApplication> LoadApplicationAsync(AppDbContext db, Guid id) =>
        db.JobApplications.Include(a => a.History).Include(a => a.Notifications).SingleAsync(a => a.Id == id);

    [Fact]
    public async Task TwoRecruiterDecisions_SecondSaveThrowsConcurrencyException()
    {
        var application = await CreateReviewApplicationAsync();
        await using var scope1 = App.Services.CreateAsyncScope();
        await using var scope2 = App.Services.CreateAsyncScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await LoadApplicationAsync(db1, application.Id);
        var second = await LoadApplicationAsync(db2, application.Id);

        first.Invite(null);
        second.Invite(null);
        await db1.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task ConflictingDecision_LeavesOnlyFirstResultInDatabase()
    {
        var application = await CreateReviewApplicationAsync();
        await using var scope1 = App.Services.CreateAsyncScope();
        await using var scope2 = App.Services.CreateAsyncScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await LoadApplicationAsync(db1, application.Id);
        var second = await LoadApplicationAsync(db2, application.Id);

        first.Invite(null);
        second.Reject(null, TimeSpan.FromHours(4));
        await db1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());

        var notification = Assert.Single(await GetNotificationsAsync(application.Id));
        Assert.Equal(NotificationType.Invited, notification.Type);
        await using var scope3 = App.Services.CreateAsyncScope();
        var stored = await LoadApplicationAsync(scope3.ServiceProvider.GetRequiredService<AppDbContext>(), application.Id);
        Assert.Equal(ApplicationStatus.Invited, stored.Status);
        Assert.Equal(3, stored.History.Count);
    }

    [Fact]
    public async Task SequentialDecisionsInFreshContexts_AreNotConcurrencyConflicts()
    {
        var application = await CreateReviewApplicationAsync();
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await LoadApplicationAsync(db, application.Id)).Invite(null);
            await db.SaveChangesAsync();
        }

        await using var scope2 = App.Services.CreateAsyncScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await LoadApplicationAsync(db2, application.Id);

        Assert.Equal(ApplicationStatus.Invited, reloaded.Status);
        Assert.Throws<DomainException>(() => reloaded.Reject(null, TimeSpan.Zero));
    }

    [Fact]
    public async Task SentAndFailedFromTwoBots_SecondSaveThrowsConcurrencyException()
    {
        var application = await CreateReviewApplicationAsync();
        (await DecideAsync(application.Id, "invite")).EnsureSuccessStatusCode();
        var id = Assert.Single(await GetNotificationsAsync(application.Id)).Id;
        await using var scope1 = App.Services.CreateAsyncScope();
        await using var scope2 = App.Services.CreateAsyncScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await db1.Notifications.SingleAsync(n => n.Id == id);
        var second = await db2.Notifications.SingleAsync(n => n.Id == id);

        first.MarkSent();
        second.MarkFailed("boom");
        await db1.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
        var stored = Assert.Single(await GetNotificationsAsync(application.Id));
        Assert.Equal(NotificationStatus.Sent, stored.Status);
        Assert.Equal(0, stored.Attempts);
    }

    [Fact]
    public async Task InviteFromAutoRejectedAndConcurrentSent_DoNotOverwriteEachOther()
    {
        var application = await CreateAutoRejectedApplicationAsync();
        var rejectionId = Assert.Single(await GetNotificationsAsync(application.Id)).Id;
        await using var scope1 = App.Services.CreateAsyncScope();
        await using var scope2 = App.Services.CreateAsyncScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var recruiterView = await LoadApplicationAsync(db1, application.Id);
        var botView = await db2.Notifications.SingleAsync(n => n.Id == rejectionId);

        recruiterView.Invite(null);
        botView.MarkSent();
        await db2.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db1.SaveChangesAsync());
        var rejection = (await GetNotificationsAsync(application.Id)).Single(n => n.Id == rejectionId);
        Assert.Equal(NotificationStatus.Sent, rejection.Status);
    }
}
