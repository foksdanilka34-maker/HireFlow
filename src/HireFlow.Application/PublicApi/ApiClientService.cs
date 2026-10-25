using HireFlow.Application.Common;
using HireFlow.Domain.ApiClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HireFlow.Application.PublicApi;

public class ApiClientService(IAppDbContext db, IOptions<PublicApiOptions> options) : IApiClientService
{
    public async Task<IssuedApiKey> IssueKeyAsync(string name, CancellationToken ct)
    {
        var normalized = ApiClient.NormalizeName(name);
        var key = ApiKeys.Generate();
        var prefix = ApiKeys.Prefix(key);
        var hash = ApiKeys.Hash(key);

        var client = await db.ApiClients.FirstOrDefaultAsync(c => c.Name == normalized, ct);
        if (client is null)
            db.ApiClients.Add(new ApiClient(normalized, prefix, hash, options.Value.DailyQuota));
        else
            client.ReplaceKey(prefix, hash);

        await db.SaveChangesAsync(ct);
        return new IssuedApiKey(normalized, key);
    }

    public async Task RevokeAsync(string name, CancellationToken ct)
    {
        var normalized = ApiClient.NormalizeName(name);
        var client = await db.ApiClients.FirstOrDefaultAsync(c => c.Name == normalized, ct)
            ?? throw new NotFoundException($"API client '{normalized}' not found.");

        client.Revoke();
        await db.SaveChangesAsync(ct);
    }
}
