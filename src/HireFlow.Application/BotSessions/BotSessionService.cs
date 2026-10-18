using System.Text.Json;
using HireFlow.Application.Common;
using HireFlow.Domain.BotSessions;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Application.BotSessions;

public class BotSessionService(IAppDbContext db) : IBotSessionService
{
    public async Task<BotSessionResponse> GetAsync(long telegramId, CancellationToken ct)
    {
        var session = await db.BotSessions.AsNoTracking().FirstOrDefaultAsync(s => s.TelegramId == telegramId, ct)
            ?? throw new NotFoundException($"Bot session for Telegram ID {telegramId} not found.");

        return BotSessionResponse.From(session);
    }

    public async Task<BotSessionResponse> UpsertAsync(long telegramId, BotSessionRequest request, CancellationToken ct)
    {
        var draft = SerializeDraft(request.Draft);
        var session = await db.BotSessions.FirstOrDefaultAsync(s => s.TelegramId == telegramId, ct);

        if (session is null)
        {
            session = new BotSession(telegramId, request.Step, request.LinkedVacancyId, draft);
            db.BotSessions.Add(session);
        }
        else
        {
            session.Update(request.Step, request.LinkedVacancyId, draft);
        }

        await db.SaveChangesAsync(ct);
        return BotSessionResponse.From(session);
    }

    // Проверяет, что черновик — JSON-объект не больше 8 КБ, и возвращает его текстом.
    private static string SerializeDraft(JsonElement draft)
    {
        if (draft.ValueKind != JsonValueKind.Object)
            throw new BadRequestException("Draft must be a JSON object.");

        var json = draft.GetRawText();
        if (System.Text.Encoding.UTF8.GetByteCount(json) > BotSession.DraftMaxBytes)
            throw new BadRequestException("Draft must not exceed 8 KB.");

        return json;
    }
}
