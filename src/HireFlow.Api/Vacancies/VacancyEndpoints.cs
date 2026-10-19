using HireFlow.Api.Common;
using HireFlow.Api.Security;
using HireFlow.Application.Vacancies;
using HireFlow.Domain.Vacancies;

namespace HireFlow.Api.Vacancies;

public static class VacancyEndpoints
{
    // Регистрирует HTTP-маршруты /vacancies.
    public static void MapVacancyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/vacancies");

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
