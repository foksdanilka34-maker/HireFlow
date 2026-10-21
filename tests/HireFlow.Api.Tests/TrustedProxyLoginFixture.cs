using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// Фикстура с маленьким лимитом входа и включённым доверием к X-Forwarded-For (API за reverse proxy).
public class TrustedProxyLoginFixture : LoginLimitFixture
{
    protected override IReadOnlyDictionary<string, string> ExtraSettings { get; } =
        new Dictionary<string, string> { ["Hosting:TrustForwardedHeaders"] = "true" };
}
