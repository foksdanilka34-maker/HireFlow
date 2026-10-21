using System.Net;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Api.Tests;

// Скачивание файла резюме из карточки отклика.
public class ResumeDownloadApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // Читает ключ файла резюме отклика прямо из БД.
    private async Task<string> ResumeFileKeyAsync(Guid applicationId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.JobApplications.AsNoTracking().Where(a => a.Id == applicationId)
            .Select(a => a.ResumeFileKey).SingleAsync();
    }

    // Файл отдаётся как вложение с исходным именем (в том числе не латинским) и типом по расширению.
    [Theory]
    [InlineData("my-resume.pdf", false, "application/pdf")]
    [InlineData("Резюме Иванова.docx", true, DocxType)]
    public async Task Download_ReturnsUploadedBytesWithTypeAndFileName(string fileName, bool docx, string contentType)
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        var bytes = docx ? ResumeFactory.Docx("Резюме", skill) : ResumeWith(skill);
        var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, bytes, fileName: fileName);

        var response = await Client.GetAsync($"/applications/{application.Id}/resume");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(contentType, response.Content.Headers.ContentType!.MediaType);
        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.Equal(fileName, disposition.FileNameStar ?? disposition.FileName);
    }

    // inline=true открывает в браузере только PDF; docx остаётся вложением, nosniff стоит всегда.
    [Theory]
    [InlineData("resume.pdf", "inline")]
    [InlineData("resume.docx", "attachment")]
    public async Task Download_InlineOnlyForPdfAndAlwaysNosniff(string fileName, string expected)
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        var bytes = fileName.EndsWith(".docx") ? ResumeFactory.Docx("Резюме", skill) : ResumeWith(skill);
        var application = await ApplyOkAsync(candidate.TelegramId, vacancy.Id, bytes, fileName: fileName);

        var response = await Client.GetAsync($"/applications/{application.Id}/resume?inline=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal(expected, disposition.DispositionType);
        Assert.Equal(fileName, disposition.FileNameStar ?? disposition.FileName);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    // Текст для docx-просмотра берётся из сохранённого при отклике текста резюме.
    [Fact]
    public async Task Text_ReturnsWordsFromUploadedResume()
    {
        var skill = Unique("s");
        var vacancy = await CreateVacancyAsync([skill]);
        var candidate = await CreateCandidateAsync();
        var application = await ApplyOkAsync(
            candidate.TelegramId, vacancy.Id, ResumeFactory.Docx("Инженер по тестированию", skill), fileName: "resume.docx");

        var response = await Client.GetAsync($"/applications/{application.Id}/resume/text");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = json.RootElement.GetProperty("text").GetString()!;
        Assert.Contains("Инженер по тестированию", text);
        Assert.Contains(skill, text);
    }

    [Fact]
    public async Task Text_UnknownApplicationReturns404()
    {
        var response = await Client.GetAsync($"/applications/{Guid.NewGuid()}/resume/text");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_FileDeletedFromDiskReturns404()
    {
        var application = await CreateReviewApplicationAsync();
        File.Delete(Path.Combine(App.StoragePath, await ResumeFileKeyAsync(application.Id)));

        var response = await Client.GetAsync($"/applications/{application.Id}/resume");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_UnknownApplicationReturns404()
    {
        var response = await Client.GetAsync($"/applications/{Guid.NewGuid()}/resume");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
