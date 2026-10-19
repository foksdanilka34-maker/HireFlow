using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// Фикстура с маленьким лимитом входа и без входа основного клиента: все попытки считает сам тест.
public class LoginLimitFixture : ApiAppFixture
{
    public const int Limit = 3;

    protected override int LoginAttemptsPerMinute => Limit;
    protected override bool LoginMainClient => false;
}
