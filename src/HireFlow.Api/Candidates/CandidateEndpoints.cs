using System.ComponentModel.DataAnnotations;
using HireFlow.Api.Common;
using HireFlow.Api.Security;
using HireFlow.Application.Candidates;
using HireFlow.Application.Matching;
using Microsoft.AspNetCore.Mvc;

namespace HireFlow.Api.Candidates;

public record FindMatchesForm
{
    [Range(0, 50)]
    public int? YearsOfExperience { get; init; }

    [Required]
    public IFormFile File { get; init; } = null!;
}

public static class CandidateEndpoints
{
    // Регистрирует HTTP-маршруты /candidates.
    public static void MapCandidateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/candidates").RequireAuthorization(AuthPolicies.Bot);

        group.MapGet("/{telegramId:long:min(1)}", (long telegramId, ICandidateService service, CancellationToken ct) =>
            service.GetAsync(telegramId, ct));

        group.MapPut("/{telegramId:long:min(1)}",
            (long telegramId, CandidateRequest request, ICandidateService service, CancellationToken ct) =>
                service.UpsertAsync(telegramId, request, ct));

        group.MapGet("/{telegramId:long:min(1)}/applications", (long telegramId, ICandidateService service, CancellationToken ct) =>
            service.GetApplicationsAsync(telegramId, ct));

        group.MapPost("/{telegramId:long:min(1)}/matches",
            async (long telegramId, [FromForm] FindMatchesForm form, IVacancyMatchingService service, CancellationToken ct) =>
            {
                var content = await form.File.ReadAllBytesAsync(ct);
                var request = new FindMatchesRequest(telegramId, form.YearsOfExperience, content);
                return await service.FindMatchesAsync(request, ct);
            })
            .DisableAntiforgery();
    }
}
