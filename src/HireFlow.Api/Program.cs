using System.Text.Json;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using HireFlow.Api.Auth;
using HireFlow.Api.BotSessions;
using HireFlow.Api.Candidates;
using HireFlow.Api.Errors;
using HireFlow.Api.JobApplications;
using HireFlow.Api.Notifications;
using HireFlow.Api.Recruiters;
using HireFlow.Api.Security;
using HireFlow.Api.Vacancies;
using HireFlow.Application;
using HireFlow.Application.Resumes;
using HireFlow.Application.Notifications;
using HireFlow.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Postgres' is not set.");
var storagePath = builder.Configuration["Storage:LocalPath"]
    ?? throw new InvalidOperationException("Storage path 'Storage:LocalPath' is not set.");
var botApiKey = builder.Configuration[$"{BotApiKeyOptions.SectionName}:{nameof(BotApiKeyOptions.ApiKey)}"];
if (string.IsNullOrWhiteSpace(botApiKey))
    throw new InvalidOperationException("Bot API key 'BotApi:ApiKey' is not set (use user secrets or env BotApi__ApiKey).");

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.Configure<ResumeUploadOptions>(builder.Configuration.GetSection(ResumeUploadOptions.SectionName));
builder.Services.Configure<BotApiKeyOptions>(builder.Configuration.GetSection(BotApiKeyOptions.SectionName));
var recruiterAuth = builder.Configuration.GetSection(RecruiterAuthOptions.SectionName).Get<RecruiterAuthOptions>()
    ?? new RecruiterAuthOptions();

// Рекрутер входит по cookie-сессии, бот — по служебному ключу в X-Api-Key.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "hireflow.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = recruiterAuth.SessionLifetime;
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, BotApiKeyHandler>(BotApiKeyHandler.SchemeName, null);

// Маршрут без явной политики закрыт сессией рекрутера.
var recruiterPolicy = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
    .RequireAuthenticatedUser()
    .Build();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.Recruiter, recruiterPolicy)
    .AddPolicy(AuthPolicies.Bot, policy => policy
        .AddAuthenticationSchemes(BotApiKeyHandler.SchemeName)
        .RequireAuthenticatedUser())
    .AddPolicy(AuthPolicies.RecruiterOrBot, policy => policy
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, BotApiKeyHandler.SchemeName)
        .RequireAuthenticatedUser())
    .SetFallbackPolicy(recruiterPolicy);

// Лимит попыток входа на IP за минуту, сверх лимита — 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthPolicies.LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = recruiterAuth.LoginAttemptsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

// Кабинет рекрутера на другом адресе ходит в API с cookie; разрешены только адреса из Web:Origins.
var webOrigins = builder.Configuration.GetSection(WebCorsOptions.SectionName).Get<WebCorsOptions>()?.Origins ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(webOrigins)
    .AllowCredentials()
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, storagePath);

var app = builder.Build();

// Консольная команда: создать рекрутера или сменить ему пароль, веб-сервер не запускается.
if (args is ["recruiter", var recruiterLogin])
    return await RecruiterCommand.RunAsync(app.Services, recruiterLogin);

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapGet("/", () => "HireFlow API работает").AllowAnonymous();
app.MapGet("/health", () => new { status = "ok", time = DateTime.UtcNow }).AllowAnonymous();
app.MapAuthEndpoints();
app.MapVacancyEndpoints();
app.MapCandidateEndpoints();
app.MapJobApplicationEndpoints();
app.MapNotificationEndpoints();
app.MapBotSessionEndpoints();

await app.RunAsync();
return 0;
