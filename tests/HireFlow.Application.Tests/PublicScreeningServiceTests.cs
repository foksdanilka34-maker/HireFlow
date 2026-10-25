using HireFlow.Application.Common;
using HireFlow.Application.PublicApi;
using HireFlow.Application.Resumes;
using HireFlow.Domain.Screening;
using HireFlow.Domain.Vacancies;
using Microsoft.Extensions.Options;

namespace HireFlow.Application.Tests;

public class PublicScreeningServiceTests
{
    // Фейк счётчика: запоминает аргументы и отвечает заданным числом разрешённых списаний.
    private sealed class FakeUsageCounter(bool allow = true, int count = 0) : IApiUsageCounter
    {
        public List<(Guid ClientId, DateOnly Day, int Quota)> Calls { get; } = [];

        public Task<bool> TryIncrementAsync(Guid clientId, DateOnly day, int quota, CancellationToken ct)
        {
            Calls.Add((clientId, day, quota));
            return Task.FromResult(allow);
        }

        public Task<int> GetAsync(Guid clientId, DateOnly day, CancellationToken ct) => Task.FromResult(count);
    }

    // Фейк читателя резюме: считает вызовы и отдаёт текст из делегата (по умолчанию фиксированный).
    private sealed class FakeResumeReader(Func<string>? text = null) : IResumeReader
    {
        public int Calls { get; private set; }

        public ResumeContent Read(byte[] content)
        {
            Calls++;
            return new ResumeContent(ResumeFormat.Pdf, text?.Invoke() ?? "C# developer", "hash");
        }
    }

    private static readonly byte[] SomeFile = [1, 2, 3];

    private static Skill S(string name, params string[] synonyms) => new() { Name = name, Synonyms = [.. synonyms] };

    private static VacancyRequirements Req(
        Skill[]? required = null, Skill[]? nice = null, int minYears = 0, int passing = 70, int borderline = 10) => new()
    {
        RequiredSkills = [.. required ?? [S("C#")]],
        NiceToHaveSkills = [.. nice ?? []],
        MinYearsOfExperience = minYears,
        PassingScore = passing,
        BorderlineWidth = borderline,
    };

    private static PublicScreeningService Service(
        FakeUsageCounter? counter = null, FakeResumeReader? reader = null, TimeSpan? timeout = null) =>
        new(counter ?? new FakeUsageCounter(), reader ?? new FakeResumeReader(),
            Options.Create(new PublicApiOptions { ExtractionTimeout = timeout ?? TimeSpan.FromSeconds(10) }));

    private static PublicScreeningRequest TextRequest(string text, VacancyRequirements? req = null, int? years = null) =>
        new(null, text, req ?? Req(), years);

    private static Skill[] Skills(int count) => Enumerable.Range(0, count).Select(i => S($"skill{i}")).ToArray();

    public static TheoryData<string, VacancyRequirements> InvalidRequirements() => new()
    {
        // 26 + 25 = 51 навык суммарно по обоим спискам.
        { "51 skills", Req(required: Skills(26), nice: Skills(25)) },
        { "21 synonyms", Req(required: [S("C#", [.. Enumerable.Range(0, 21).Select(i => $"syn{i}")])]) },
        // Вложенный список DataAnnotations сам не проверяет: навык с пустым именем должен ловиться вручную.
        { "empty skill name", Req(required: [S("C#")], nice: [S("")]) },
        { "borderline above passing", Req(passing: 50, borderline: 60) },
    };

    [Theory]
    [MemberData(nameof(InvalidRequirements))]
    public async Task Screen_InvalidRequirementsThrowBadRequestBeforeReadingFile(string _, VacancyRequirements requirements)
    {
        var reader = new FakeResumeReader();
        var request = new PublicScreeningRequest(SomeFile, null, requirements, null);

        await Assert.ThrowsAsync<BadRequestException>(() => Service(reader: reader).ScreenAsync(request, default));

        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task Screen_TextOverLimitThrowsBadRequestForTextAndForExtractedFileText()
    {
        var tooLong = new string('a', PublicScreeningService.MaxTextLength + 1);

        await Assert.ThrowsAsync<BadRequestException>(() => Service().ScreenAsync(TextRequest(tooLong), default));

        var reader = new FakeResumeReader(() => tooLong);
        var fileRequest = new PublicScreeningRequest(SomeFile, null, Req(), null);
        await Assert.ThrowsAsync<BadRequestException>(() => Service(reader: reader).ScreenAsync(fileRequest, default));
    }

    [Fact]
    public async Task Screen_TextOfExactlyLimitIsAccepted()
    {
        var text = "C# " + new string('a', PublicScreeningService.MaxTextLength - 3);

        var response = await Service().ScreenAsync(TextRequest(text), default);

        Assert.Equal(100, response.Score);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Screen_FileAndTextTogetherOrNeitherThrowBadRequest(bool withFile, bool withText)
    {
        var reader = new FakeResumeReader();
        var request = new PublicScreeningRequest(withFile ? SomeFile : null, withText ? "C#" : null, Req(), null);

        await Assert.ThrowsAsync<BadRequestException>(() => Service(reader: reader).ScreenAsync(request, default));

        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task Screen_RequestYearsOutsideZeroToFiftyThrowBadRequestBeforeExtraction(int years, bool accepted)
    {
        var reader = new FakeResumeReader();
        var request = new PublicScreeningRequest(SomeFile, null, Req(), years);

        if (accepted)
            Assert.Equal(YearsOfExperienceSource.Request, (await Service(reader: reader).ScreenAsync(request, default)).YearsOfExperienceSource);
        else
        {
            await Assert.ThrowsAsync<BadRequestException>(() => Service(reader: reader).ScreenAsync(request, default));
            Assert.Equal(0, reader.Calls);
        }
    }

    [Fact]
    public async Task Screen_YearsFromRequestWinOverResumeText()
    {
        var request = TextRequest("C# developer, 10 years of experience", Req(minYears: 5), years: 2);

        var response = await Service().ScreenAsync(request, default);

        Assert.Equal(2, response.YearsOfExperience);
        Assert.Equal(YearsOfExperienceSource.Request, response.YearsOfExperienceSource);
        Assert.False(response.ExperienceIsEnough);
    }

    [Fact]
    public async Task Screen_YearsFoundInResumeAreSourceResume()
    {
        var request = TextRequest("C# developer, 6 years of experience", Req(minYears: 5));

        var response = await Service().ScreenAsync(request, default);

        Assert.Equal(6, response.YearsOfExperience);
        Assert.Equal(YearsOfExperienceSource.Resume, response.YearsOfExperienceSource);
        Assert.True(response.ExperienceIsEnough);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(0, true)]
    public async Task Screen_NoYearsAnywhereIsSourceNoneWithNullYearsAndZeroForScoring(int minYears, bool experienceIsEnough)
    {
        var request = TextRequest("C# developer", Req(minYears: minYears));

        var response = await Service().ScreenAsync(request, default);

        Assert.Null(response.YearsOfExperience);
        Assert.Equal(YearsOfExperienceSource.None, response.YearsOfExperienceSource);
        Assert.Equal(experienceIsEnough, response.ExperienceIsEnough);
    }

    [Fact]
    public async Task Screen_MatchedAndMissingSkillsFollowRequirementsOrder()
    {
        var requirements = Req(
            required: [S("Alpha"), S("Beta"), S("Gamma")],
            nice: [S("Xray"), S("Yankee")]);
        // В резюме найденные навыки идут в другом порядке, чем в требованиях.
        var request = TextRequest("yankee, gamma, alpha", requirements, years: 1);

        var response = await Service().ScreenAsync(request, default);

        Assert.Equal(["Alpha", "Gamma"], response.MatchedRequiredSkills);
        Assert.Equal(["Beta"], response.MissingRequiredSkills);
        Assert.Equal(["Yankee"], response.MatchedNiceToHaveSkills);
        Assert.Equal(["Xray"], response.MissingNiceToHaveSkills);
        // 80 * 2/3 + 20 * 1/2 = 63,3; не хватает одного обязательного навыка — пограничный.
        Assert.Equal(63, response.Score);
        Assert.Equal(ScreeningVerdict.Borderline, response.Verdict);
    }

    [Fact]
    public async Task Screen_HangingExtractionThrowsBadRequestAfterTimeout()
    {
        using var gate = new ManualResetEventSlim();
        // Фейк «зависает» не навсегда, чтобы без таймаута в сервисе тест упал, а не повис.
        var reader = new FakeResumeReader(() =>
        {
            gate.Wait(TimeSpan.FromSeconds(5));
            return "C#";
        });
        var request = new PublicScreeningRequest(SomeFile, null, Req(), null);

        try
        {
            await Assert.ThrowsAsync<BadRequestException>(() =>
                Service(reader: reader, timeout: TimeSpan.FromMilliseconds(100)).ScreenAsync(request, default));
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task ChargeQuota_ExhaustedThrowsTooManyRequestsWithRetryAfterUntilNextUtcMidnight()
    {
        var clientId = Guid.NewGuid();
        var counter = new FakeUsageCounter(allow: false);

        var before = DateTime.UtcNow;
        var error = await Assert.ThrowsAsync<TooManyRequestsException>(() => Service(counter).ChargeQuotaAsync(clientId, 7, default));
        var after = DateTime.UtcNow;

        // Остаток до полуночи, посчитанный в момент вызова, лежит между оценками до и после вызова.
        Assert.NotNull(error.RetryAfter);
        Assert.InRange(error.RetryAfter.Value, before.Date.AddDays(1) - after, before.Date.AddDays(1) - before);
        var call = Assert.Single(counter.Calls);
        Assert.Equal((clientId, 7), (call.ClientId, call.Quota));
        Assert.Equal(DateOnly.FromDateTime(before), call.Day);
    }
}
