using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HireFlow.Domain.BotSessions;

namespace HireFlow.Application.BotSessions;

public record BotSessionRequest
{
    [Required, MaxLength(BotSession.StepMaxLength)]
    public string Step { get; init; } = "";

    public Guid? LinkedVacancyId { get; init; }

    public JsonElement Draft { get; init; }
}

public record BotSessionResponse(
    long TelegramId,
    string Step,
    Guid? LinkedVacancyId,
    JsonElement Draft,
    DateTime UpdatedAt)
{
    // Собирает ответ API из сущности.
    public static BotSessionResponse From(BotSession s) => new(
        s.TelegramId, s.Step, s.LinkedVacancyId, JsonDocument.Parse(s.Draft).RootElement.Clone(), s.UpdatedAt);
}
