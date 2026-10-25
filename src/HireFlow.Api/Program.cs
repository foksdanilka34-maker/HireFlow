using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using HireFlow.Api.Auth;
using HireFlow.Api.BotSessions;
using HireFlow.Api.Candidates;
using HireFlow.Api.Database;
using HireFlow.Api.Errors;
using HireFlow.Api.Hosting;
using HireFlow.Api.JobApplications;
using HireFlow.Api.Notifications;
using HireFlow.Api.PublicApi;
using HireFlow.Api.Recruiters;
using HireFlow.Api.Security;
using HireFlow.Api.Vacancies;
using HireFlow.Application;
using HireFlow.Application.Common;
using HireFlow.Application.Resumes;
using HireFlow.Application.Notifications;
using HireFlow.Application.PublicApi;
using HireFlow.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Консольная команда для healthcheck контейнера: строка подключения и ключ бота не нужны.
if (args is ["healthcheck"])
    return await HealthcheckCommand.RunAsync();

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Postgres' is not set.");
var storagePath = builder.Configuration["Storage:LocalPath"]
    ?? throw new InvalidOperationException("Storage path 'Storage:LocalPath' is not set.");
var botApiKey = builder.Configuration[$"{BotApiKeyOptions.SectionName}:{nameof(BotApiKeyOptions.ApiKey)}"];
if (string.IsNullOrWhiteSpace(botApiKey))
    throw new InvalidOperationException("Bot API key 'BotApi:ApiKey' is not set (use user secrets or env BotApi__ApiKey).");

var hosting = builder.Configuration.GetSection(HostingOptions.SectionName).Get<HostingOptions>()
    ?? new HostingOptions();

// За reverse proxy берём IP клиента и схему из X-Forwarded-*; доверяем любому прокси в закрытой сети.
if (hosting.TrustForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Ключи cookie-сессий в общей папке переживают перезапуск контейнера.
if (!string.IsNullOrWhiteSpace(hosting.DataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("HireFlow")
        .PersistKeysToFileSystem(new DirectoryInfo(hosting.DataProtectionKeysPath));
}

// internal — все маршруты, только в Development; public — только /v1, отдаётся всегда (/v1/openapi.json).
builder.Services.AddOpenApi("internal");
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.Configure<ResumeUploadOptions>(builder.Configuration.GetSection(ResumeUploadOptions.SectionName));
builder.Services.Configure<BotApiKeyOptions>(builder.Configuration.GetSection(BotApiKeyOptions.SectionName));
builder.Services.Configure<PublicApiOptions>(builder.Configuration.GetSection(PublicApiOptions.SectionName));
var publicApi = builder.Configuration.GetSection(PublicApiOptions.SectionName).Get<PublicApiOptions>()
    ?? new PublicApiOptions();
builder.Services.AddPublicApiDocs(publicApi);
var recruiterAuth = builder.Configuration.GetSection(RecruiterAuthOptions.SectionName).Get<RecruiterAuthOptions>()
    ?? new RecruiterAuthOptions();

// Рекрутер входит по cookie-сессии, бот — по служебному ключу в X-Api-Key, клиент публичного API — по Bearer-ключу.
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
    .AddScheme<AuthenticationSchemeOptions, BotApiKeyHandler>(BotApiKeyHandler.SchemeName, null)
    .AddScheme<AuthenticationSchemeOptions, PublicApiKeyHandler>(PublicApiKeyHandler.SchemeName, null);

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
    // Только ключ публичного API: другие политики эту схему не включают.
    .AddPolicy(AuthPolicies.PublicApi, policy => policy
        .AddAuthenticationSchemes(PublicApiKeyHandler.SchemeName)
        .RequireAuthenticatedUser())
    .SetFallbackPolicy(recruiterPolicy);

// Лимит попыток входа на IP и запросов публичного API на клиента за минуту, сверх лимита — 429 с Retry-After.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        if (policy == AuthPolicies.LoginRateLimit)
            context.HttpContext.RequestServices.GetRequiredService<HireFlowMetrics>().RecruiterLogin("limited");
        return ValueTask.CompletedTask;
    };
    options.AddPolicy(AuthPolicies.LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = recruiterAuth.LoginAttemptsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy(AuthPolicies.PublicApiRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = publicApi.RequestsPerMinute,
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

// /health/ready проверяет базу; /health/live — только что процесс отвечает.
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(3));

// Снимок из БД для gauges; singleton, чтобы его можно было получить из DI.
builder.Services.AddSingleton<MetricsSnapshotService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MetricsSnapshotService>());

// Метрики, трейсы и логи; экспорт по OTLP в коллектор только при заданном адресе.
var telemetry = builder.Configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>()
    ?? new TelemetryOptions();
builder.Logging.AddOpenTelemetry(o => o.IncludeFormattedMessage = true);
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("hireflow-api"))
    .WithMetrics(metrics => metrics.AddMeter(
        "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "System.Runtime", "Npgsql",
        HireFlowMetrics.MeterName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(o =>
        {
            o.Filter = context => !context.Request.Path.StartsWithSegments("/health");
            o.RecordException = false;
        })
        .AddNpgsql()
        .AddProcessor(new UrlTagsRemover()));
if (!string.IsNullOrWhiteSpace(telemetry.OtlpEndpoint))
{
    // Экспортёр подключается после процессора UrlTagsRemover: порядок важен, иначе url.* уйдут в коллектор.
    otel.UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, new Uri(telemetry.OtlpEndpoint));
    builder.Services.Configure<MetricReaderOptions>(reader =>
        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
            (int)telemetry.ExportInterval.TotalMilliseconds);
}

var app = builder.Build();

// Консольная команда: создать рекрутера или сменить ему пароль, веб-сервер не запускается.
if (args is ["recruiter", var recruiterLogin])
    return await RecruiterCommand.RunAsync(app.Services, recruiterLogin);

// Консольная команда: выдать клиенту публичного API ключ или отозвать его, веб-сервер не запускается.
if (args is ["api-client", var apiClientName])
    return await ApiClientCommand.RunAsync(app.Services, apiClientName, revoke: false);
if (args is ["api-client", var revokedClientName, "revoke"])
    return await ApiClientCommand.RunAsync(app.Services, revokedClientName, revoke: true);

// Консольная команда: применить миграции, веб-сервер не запускается.
if (args is ["migrate"])
    return await MigrateCommand.RunAsync(app.Services);

if (hosting.TrustForwardedHeaders)
    app.UseForwardedHeaders();

// До обработчика ошибок, чтобы видеть итоговый код ответа /v1, в том числе 401 и 429.
app.UseMiddleware<PublicApiMetricsMiddleware>();
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
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();
app.MapAuthEndpoints();
app.MapVacancyEndpoints();
app.MapCandidateEndpoints();
app.MapJobApplicationEndpoints();
app.MapNotificationEndpoints();
app.MapBotSessionEndpoints();
app.MapPublicApiEndpoints();
app.MapPublicApiDocs();

await app.RunAsync();
return 0;
