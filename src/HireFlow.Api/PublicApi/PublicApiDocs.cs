using HireFlow.Application.PublicApi;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace HireFlow.Api.PublicApi;

// OpenAPI-документ публичного API (/v1/openapi.json) и страница документации Scalar (/v1/docs).
public static class PublicApiDocs
{
    public const string DocumentName = "public";
    public const string DocumentPath = "/v1/openapi.json";
    private const string SchemeName = "Bearer";

    // Регистрирует документ public: только маршруты /v1, описание, условия использования и схема Bearer.
    public static void AddPublicApiDocs(this IServiceCollection services, PublicApiOptions publicApi)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.ShouldInclude = description => description.RelativePath?.StartsWith("v1/", StringComparison.Ordinal) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "HireFlow Screening API",
                    Version = "v1",
                    Description = BuildDescription(publicApi),
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    [SchemeName] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        Description = "API key: `Authorization: Bearer hf_...`",
                    },
                };
                document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, document)] = [] }];
                return Task.CompletedTask;
            });
        });
    }

    // Отдаёт документ public по /v1/openapi.json и страницу Scalar по /v1/docs, обе без ключа.
    public static void MapPublicApiDocs(this IEndpointRouteBuilder app)
    {
        // MapOpenApi требует {documentName} в пути, поэтому документ отдаётся обычным маршрутом.
        app.MapGet(DocumentPath, async ([FromKeyedServices(DocumentName)] IOpenApiDocumentProvider provider, CancellationToken ct) =>
            {
                var document = await provider.GetOpenApiDocumentAsync(ct);
                return Results.Text(await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, ct), "application/json");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Scalar ищет документ и адрес для запросов относительно своей страницы, поэтому работает и за Caddy под /api/.
        app.MapScalarApiReference("/v1/docs", options => options
                .WithTitle("HireFlow Screening API")
                .AddDocument(DocumentName, "HireFlow Screening API", DocumentPath)
                .WithDynamicBaseServerUrl(true))
            .AllowAnonymous();
    }

    // Собирает markdown-описание: что делает API, ограничения, ошибки, версии, условия использования и ПД.
    private static string BuildDescription(PublicApiOptions publicApi) => $"""
        Checks a resume against your requirements with the same rule engine HireFlow uses for job applications:
        required and nice-to-have skills with synonyms, years of experience, score and verdict
        (`passed`, `borderline`, `failed`). The API is stateless and does not use AI.

        Send the resume as `multipart/form-data` (`file` — PDF or DOCX, `requirements` — JSON string,
        optional `yearsOfExperience`) or as `application/json` with `text` instead of a file.

        ## Authentication
        Every request needs `Authorization: Bearer <key>`. Keys are issued by the HireFlow team; keep the key on your
        server, CORS is not enabled.

        ## Limits
        - File up to 10 MB, resume text up to 100,000 characters.
        - Up to 50 skills, up to 20 synonyms per skill.
        - Up to {publicApi.RequestsPerMinute} requests per minute per key and a daily quota per key (UTC day),
          see `GET /v1/usage`. Every `POST /v1/screenings` with a valid key counts towards the quota,
          including `400` and `413` responses.
        - Over a limit — `429` with `Retry-After` (seconds).

        ## Errors
        Errors are returned as Problem Details (`application/problem+json`): `400` — invalid requirements or unreadable
        file, `401` — missing, invalid or revoked key, `413` — file larger than 10 MB, `429` — rate limit or daily quota.

        ## Versioning
        Breaking changes are released only under a new path version (`/v2`); `/v1` keeps working for at least
        6 months after `/v2` is released. Adding new response fields is not a breaking change.

        ## Terms of use and personal data
        - The file and the text are processed in memory only: they are not stored and not logged.
        - The candidate's name and phone number are never returned.
        - The request log contains only the client, route, time, response code and duration.
        - Do not send a resume without the candidate's consent to its processing.
        """;
}
