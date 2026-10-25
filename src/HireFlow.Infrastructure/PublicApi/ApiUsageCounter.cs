using HireFlow.Application.PublicApi;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Infrastructure.PublicApi;

public class ApiUsageCounter(AppDbContext db) : IApiUsageCounter
{
    // Одним запросом создаёт строку дня или увеличивает счётчик, пока он меньше квоты: параллельные
    // запросы не превысят квоту. Нет возвращённой строки — квота исчерпана.
    public async Task<bool> TryIncrementAsync(Guid clientId, DateOnly day, int quota, CancellationToken ct)
    {
        if (quota <= 0)
            return false;

        var counts = await db.Database.SqlQuery<int>($"""
            INSERT INTO api_usages (api_client_id, day, count) VALUES ({clientId}, {day}, 1)
            ON CONFLICT (api_client_id, day) DO UPDATE SET count = api_usages.count + 1
            WHERE api_usages.count < {quota}
            RETURNING count AS "Value"
            """).ToListAsync(ct);
        return counts.Count > 0;
    }

    public async Task<int> GetAsync(Guid clientId, DateOnly day, CancellationToken ct)
    {
        return await db.ApiUsages
            .Where(u => u.ApiClientId == clientId && u.Day == day)
            .Select(u => u.Count)
            .FirstOrDefaultAsync(ct);
    }
}
