using System.Security.Claims;
using HireFlow.Api.Security;
using HireFlow.Application.Common;
using HireFlow.Application.Recruiters;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HireFlow.Api.Auth;

public static class AuthEndpoints
{
    // Регистрирует маршруты входа и выхода рекрутера /auth.
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (LoginRequest request, IRecruiterService service, HireFlowMetrics metrics, HttpContext http, CancellationToken ct) =>
        {
            var recruiter = await service.LoginAsync(request, ct);
            metrics.RecruiterLogin(recruiter is null ? "failure" : "success");
            if (recruiter is null)
                return Results.Problem("Invalid login or password.", statusCode: StatusCodes.Status401Unauthorized);

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, recruiter.Id.ToString()),
                    new Claim(ClaimTypes.Name, recruiter.Login),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Ok(recruiter);
        })
        .AllowAnonymous()
        .RequireRateLimiting(AuthPolicies.LoginRateLimit);

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        .AllowAnonymous();

        group.MapGet("/me", (ClaimsPrincipal user) => new RecruiterResponse(user.GetRecruiterId(), user.Identity!.Name!))
            .RequireAuthorization(AuthPolicies.Recruiter);
    }

    // Достаёт id рекрутера из claim NameIdentifier сессии.
    public static Guid GetRecruiterId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
