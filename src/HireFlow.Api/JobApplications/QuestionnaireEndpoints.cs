using HireFlow.Api.Security;
using HireFlow.Application.JobApplications;

namespace HireFlow.Api.JobApplications;

public static class QuestionnaireEndpoints
{
    public static void MapQuestionnaireEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/candidates/{telegramId:long}/questions").RequireAuthorization(AuthPolicies.Bot);
        group.MapGet("/", async (long telegramId, QuestionnaireService service, CancellationToken ct) =>
        {
            var pending = await service.GetPendingAsync(telegramId, ct);
            return pending is null ? Results.NotFound() : Results.Ok(pending);
        });
        group.MapPost("/{applicationId:guid}", async (long telegramId, Guid applicationId, AnswerQuestionRequest request, QuestionnaireService service, CancellationToken ct) =>
            Results.Ok(await service.AnswerAsync(telegramId, applicationId, request, ct)));
    }
}
