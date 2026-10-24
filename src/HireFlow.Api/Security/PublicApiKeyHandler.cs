using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using HireFlow.Application.Common;
using HireFlow.Application.PublicApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HireFlow.Api.Security;

public class PublicApiKeyHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PublicApiKey";
    public const string DailyQuotaClaim = "daily_quota";

    private const string BearerPrefix = "Bearer ";

    // Аутентифицирует клиента публичного API по Authorization: Bearer <ключ>: нет заголовка — пропуск,
    // неизвестный или отозванный ключ — ошибка. Ключ и его хэш не логируются.
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var key = header[BearerPrefix.Length..].Trim();
        var hash = ApiKeys.Hash(key);
        var client = await db.ApiClients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.KeyHash == hash, Context.RequestAborted);
        if (client is null || !client.IsActive)
            return AuthenticateResult.Fail("Invalid API key.");

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, client.Id.ToString()),
                new Claim(ClaimTypes.Name, client.Name),
                new Claim(DailyQuotaClaim, client.DailyQuota.ToString(CultureInfo.InvariantCulture)),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
