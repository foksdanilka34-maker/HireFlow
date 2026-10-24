using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// Фикстура с дневной квотой нового клиента 3 запроса.
public class PublicApiQuotaFixture : ApiAppFixture
{
    public const int Quota = 3;

    protected override IReadOnlyDictionary<string, string> ExtraSettings { get; } =
        new Dictionary<string, string> { ["PublicApi:DailyQuota"] = Quota.ToString() };
}

// Фикстура с лимитом публичного API 2 запроса в минуту на клиента.
public class PublicApiRateLimitFixture : ApiAppFixture
{
    public const int Limit = 2;

    protected override IReadOnlyDictionary<string, string> ExtraSettings { get; } =
        new Dictionary<string, string> { ["PublicApi:RequestsPerMinute"] = Limit.ToString() };
}
