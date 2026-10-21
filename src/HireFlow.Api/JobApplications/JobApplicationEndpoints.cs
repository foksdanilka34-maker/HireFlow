using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HireFlow.Api.Auth;
using HireFlow.Api.Common;
using HireFlow.Api.Security;
using HireFlow.Application.JobApplications;
using HireFlow.Domain.Candidates;
using HireFlow.Domain.JobApplications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace HireFlow.Api.JobApplications;

public record CreateApplicationForm
{
    [Range(1, long.MaxValue)]
    public long TelegramId { get; init; }

    public Guid VacancyId { get; init; }

    [Range(0, 50)]
    public int YearsOfExperience { get; init; }

    [MaxLength(Candidate.NameMaxLength)]
    public string? Name { get; init; }

    public string? Phone { get; init; }

    [Required]
    public IFormFile File { get; init; } = null!;
}

public static class JobApplicationEndpoints
{
    // Регистрирует HTTP-маршруты /applications.
    public static void MapJobApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/applications");

        group.MapPost("/", async ([FromForm] CreateApplicationForm form, IJobApplicationService service, CancellationToken ct) =>
        {
            var content = await form.File.ReadAllBytesAsync(ct);
            var request = new CreateApplicationRequest(
                form.TelegramId, form.VacancyId, form.YearsOfExperience, form.File.FileName, content, form.Name, form.Phone);

            var application = await service.CreateAsync(request, ct);
            return Results.Created($"/applications/{application.Id}", application);
        })
        .DisableAntiforgery()
        .RequireAuthorization(AuthPolicies.Bot);

        group.MapGet("/", async (Guid? vacancyId, string? status, IJobApplicationService service, CancellationToken ct) =>
            EnumQuery.TryParse<ApplicationStatus>(status, out var parsed)
                ? Results.Ok(await service.ListAsync(vacancyId, parsed, ct))
                : Results.Problem($"Unknown status '{status}'.", statusCode: StatusCodes.Status400BadRequest));

        group.MapGet("/{id:guid}", (Guid id, IJobApplicationService service, CancellationToken ct) =>
            service.GetAsync(id, ct));

        group.MapPost("/{id:guid}/decision",
            (Guid id, DecisionRequest request, ClaimsPrincipal user, IJobApplicationService service, CancellationToken ct) =>
                service.DecideAsync(id, user.GetRecruiterId(), request, ct));

        group.MapGet("/{id:guid}/resume", async (
            Guid id, bool? inline, HttpContext http, IJobApplicationService service, CancellationToken ct) =>
        {
            var file = await service.GetResumeAsync(id, ct);
            http.Response.Headers.XContentTypeOptions = "nosniff";
            if (inline == true && file.ContentType == "application/pdf")
            {
                // inline с именем файла: браузер покажет PDF во встроенном просмотрщике.
                var disposition = new ContentDispositionHeaderValue("inline");
                disposition.SetHttpFileName(file.FileName);
                http.Response.Headers.ContentDisposition = disposition.ToString();
                return Results.Stream(file.Content, file.ContentType);
            }
            return Results.File(file.Content, file.ContentType, fileDownloadName: file.FileName);
        });

        group.MapGet("/{id:guid}/resume/text", (Guid id, IJobApplicationService service, CancellationToken ct) =>
            service.GetResumeTextAsync(id, ct));
    }
}
