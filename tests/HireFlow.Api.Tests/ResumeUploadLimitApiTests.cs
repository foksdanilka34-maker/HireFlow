using System.Net;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Лимит загрузок резюме при подборе: 3 разных файла за 24 часа на одного Telegram ID.
public class ResumeUploadLimitApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    // Резюме с уникальным текстом, а значит и с уникальным хешем.
    private static byte[] NewResume() => ResumeWith(Unique("s"));

    [Fact]
    public async Task Matches_FourthDifferentFileReturns429ButSameFileOtherCandidateAndRetryAreAllowed()
    {
        var telegramId = NewTelegramId();
        var first = NewResume();
        Assert.Equal(HttpStatusCode.OK, (await MatchesAsync(telegramId, first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MatchesAsync(telegramId, NewResume())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MatchesAsync(telegramId, NewResume())).StatusCode);
        var fourth = NewResume();

        var limited = await MatchesAsync(telegramId, fourth);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.DoesNotContain("vacancies", await limited.Content.ReadAsStringAsync());
        // Отказанный файл не записан: повтор тоже 429. Уже учтённый файл (в том числе повторный подбор с опытом) проходит.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await MatchesAsync(telegramId, fourth)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MatchesAsync(telegramId, first, years: 3)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MatchesAsync(NewTelegramId(), fourth)).StatusCode);
    }

    [Fact]
    public async Task Matches_UploadOlderThanWindowDoesNotCount()
    {
        var telegramId = NewTelegramId();
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await MatchesAsync(telegramId, NewResume())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await MatchesAsync(telegramId, NewResume())).StatusCode);
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var old = DateTime.UtcNow.AddHours(-25);
            var rows = await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE resume_uploads SET uploaded_at = {old} WHERE id = (SELECT id FROM resume_uploads WHERE telegram_id = {telegramId} LIMIT 1)");
            Assert.Equal(1, rows);
        }

        var response = await MatchesAsync(telegramId, NewResume());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Старая запись удалена при новой загрузке: в таблице ровно три строки.
        await using var checkScope = App.Services.CreateAsyncScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await checkDb.ResumeUploads.CountAsync(u => u.TelegramId == telegramId));
    }
}
