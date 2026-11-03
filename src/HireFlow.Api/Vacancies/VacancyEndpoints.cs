using HireFlow.Api.Common;
using HireFlow.Api.Security;
using HireFlow.Application.Vacancies;
using HireFlow.Domain.Vacancies;
using HireFlow.Application.Ai;
using Microsoft.Extensions.Options;

namespace HireFlow.Api.Vacancies;

public static class VacancyEndpoints
{
    // Регистрирует HTTP-маршруты /vacancies.
    public static void MapVacancyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/vacancies");

        group.MapPost("/ai-suggestions", async (VacancyAiRequest request, IVacancyAiAssistant assistant, IOptions<AiReviewOptions> options, CancellationToken ct) =>
        {
            if (!options.Value.Enabled) return Results.Problem("ИИ отключён в настройках.", statusCode: 503);
            if (string.IsNullOrWhiteSpace(request.Title) || request.RequiredSkills is null || request.NiceToHaveSkills is null ||
                request.RequiredSkills.Count + request.NiceToHaveSkills.Count > 50 ||
                request.RequiredSkills.Concat(request.NiceToHaveSkills).Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 100))
                return Results.BadRequest();
            try { return Results.Ok(await assistant.SuggestAsync(request, ct)); }
            catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or InvalidDataException or TaskCanceledException or KeyNotFoundException or InvalidOperationException)
            {
                if (ct.IsCancellationRequested) throw;
                return Results.Problem("Не удалось получить предложения ИИ. Проверьте модель и попробуйте снова.", statusCode: 503);
            }
        }).RequireAuthorization(AuthPolicies.Recruiter);

        group.MapGet("/", async (string? status, IVacancyService service, CancellationToken ct) =>
            EnumQuery.TryParse<VacancyStatus>(status, out var parsed)
                ? Results.Ok(await service.GetAllAsync(parsed, ct))
                : Results.Problem($"Unknown status '{status}'.", statusCode: StatusCodes.Status400BadRequest))
            .RequireAuthorization(AuthPolicies.RecruiterOrBot);

        group.MapGet("/{id:guid}", (Guid id, IVacancyService service, CancellationToken ct) =>
            service.GetAsync(id, ct))
            .RequireAuthorization(AuthPolicies.RecruiterOrBot);

        group.MapPost("/", async (VacancyRequest request, IVacancyService service, CancellationToken ct) =>
        {
            var vacancy = await service.CreateAsync(request, ct);
            return Results.Created($"/vacancies/{vacancy.Id}", vacancy);
        });

        group.MapPut("/{id:guid}", (Guid id, VacancyRequest request, IVacancyService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));

        group.MapPost("/{id:guid}/open", (Guid id, IVacancyService service, CancellationToken ct) =>
            service.OpenAsync(id, ct));

        group.MapPost("/{id:guid}/close", (Guid id, IVacancyService service, CancellationToken ct) =>
            service.CloseAsync(id, ct));

        group.MapPost("/{id:guid}/archive", (Guid id, IVacancyService service, CancellationToken ct) =>
            service.ArchiveAsync(id, ct));

        group.MapPost("/{id:guid}/unarchive", (Guid id, IVacancyService service, CancellationToken ct) =>
            service.UnarchiveAsync(id, ct));

        group.MapDelete("/{id:guid}", async (Guid id, IVacancyService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
