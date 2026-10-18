using System.Net;
using System.Net.Http.Json;
using HireFlow.Api.Tests.Infrastructure;
using HireFlow.Application.Candidates;

namespace HireFlow.Api.Tests;

public class CandidateApiTests(ApiAppFixture app) : ApiTestBase(app)
{
    private Task<HttpResponseMessage> PutAsync(long telegramId, string name, string phone) =>
        Client.PutAsJsonAsync($"/candidates/{telegramId}", new CandidateRequest { Name = name, Phone = phone }, Json);

    [Fact]
    public async Task Put_CreatesCandidateAndAddsPlusToPhone()
    {
        var telegramId = NewTelegramId();

        var response = await PutAsync(telegramId, "  Иван Петров ", "79991234567");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidate = await ReadAsync<CandidateResponse>(response);
        Assert.Equal(telegramId, candidate.TelegramId);
        Assert.Equal("Иван Петров", candidate.Name);
        Assert.Equal("+79991234567", candidate.Phone);
        Assert.Equal(7, candidate.Id.Version);

        var loaded = await ReadAsync<CandidateResponse>(await Client.GetAsync($"/candidates/{telegramId}"));
        Assert.Equal(candidate.Id, loaded.Id);
        Assert.Equal("Иван Петров", loaded.Name);
        Assert.Equal("+79991234567", loaded.Phone);
    }

    [Fact]
    public async Task Put_ExistingCandidateIsUpdatedNotDuplicated()
    {
        var telegramId = NewTelegramId();
        var first = await ReadAsync<CandidateResponse>(await PutAsync(telegramId, "Иван", "79991234567"));
        var before = DateTime.UtcNow;

        var second = await ReadAsync<CandidateResponse>(await PutAsync(telegramId, "Пётр", "78885554433"));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Пётр", second.Name);
        Assert.Equal("+78885554433", second.Phone);
        Assert.Equal(first.CreatedAt, second.CreatedAt);
        Assert.InRange(second.UpdatedAt, before, DateTime.UtcNow);

        var loaded = await ReadAsync<CandidateResponse>(await Client.GetAsync($"/candidates/{telegramId}"));
        Assert.Equal(first.Id, loaded.Id);
        Assert.Equal("Пётр", loaded.Name);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("7999123456789012345")]
    public async Task Put_InvalidPhoneReturns400(string phone)
    {
        var telegramId = NewTelegramId();

        var response = await PutAsync(telegramId, "Иван", phone);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/candidates/{telegramId}")).StatusCode);
    }

    [Fact]
    public async Task Get_UnknownCandidateReturns404()
    {
        var response = await Client.GetAsync($"/candidates/{NewTelegramId()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
