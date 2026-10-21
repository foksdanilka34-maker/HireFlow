using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HireFlow.Api.Security;

public class BotApiKeyHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<BotApiKeyOptions> botApiKeyOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "BotApiKey";

    private readonly byte[] _expected = Encoding.UTF8.GetBytes(botApiKeyOptions.Value.ApiKey);

    // Аутентифицирует бота по заголовку X-Api-Key: нет заголовка — пропуск, неверный ключ — ошибка.
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(BotApiKeyOptions.HeaderName, out var header))
            return Task.FromResult(AuthenticateResult.NoResult());

        var provided = Encoding.UTF8.GetBytes(header.ToString());
        if (!CryptographicOperations.FixedTimeEquals(provided, _expected))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "bot")], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
