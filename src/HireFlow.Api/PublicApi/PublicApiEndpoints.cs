using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using HireFlow.Api.Common;
using HireFlow.Api.Security;
using HireFlow.Application.Common;
using HireFlow.Application.PublicApi;
using HireFlow.Application.Resumes;
using HireFlow.Domain.Vacancies;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace HireFlow.Api.PublicApi;

// Тело JSON-варианта POST /v1/screenings: текст резюме вместо файла.
public record PublicScreeningJson(string? Text, VacancyRequirements? Requirements, int? YearsOfExperience = null);

public static class PublicApiEndpoints
{
    // Файл до 10 МБ плюс поля формы; больше — 413 от сервера ещё при чтении тела.
    private const long MaxRequestSize = 11 * 1024 * 1024;

    // Схема multipart-варианта POST /v1/screenings для OpenAPI.
    private static readonly OpenApiSchema MultipartSchema = new()
    {
        Type = JsonSchemaType.Object,
        Required = new HashSet<string> { "file", "requirements" },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary", Description = "PDF or DOCX, up to 10 MB." },
            ["requirements"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Requirements as a JSON string, same format as in the JSON body." },
            ["yearsOfExperience"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Description = "Optional; searched in the resume when omitted." },
        },
    };

    // Регистрирует маршруты публичного API скрининга /v1 (только ключ клиента, без CORS).
    public static void MapPublicApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1")
            .RequireAuthorization(AuthPolicies.PublicApi)
            .RequireRateLimiting(AuthPolicies.PublicApiRateLimit)
            .WithMetadata(new DisableCorsAttribute())
            .DisableAntiforgery();

        group.MapPost("/screenings", async (HttpRequest http, ClaimsPrincipal user, IPublicScreeningService service, CancellationToken ct) =>
            {
                // Квота списывается до чтения тела, чтобы её тратили и запросы с ответом 400/413.
                await service.ChargeQuotaAsync(GetClientId(user), GetDailyQuota(user), ct);
                var request = http.HasFormContentType ? await ReadFormAsync(http, ct) : await ReadJsonAsync(http, ct);
                return await service.ScreenAsync(request, ct);
            })
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestSize))
            .WithSummary("Screen a resume")
            .WithDescription("Checks a resume (PDF/DOCX file or text) against the requirements and returns score and verdict.")
            // Тело читается вручную, поэтому оба варианта описываются только в OpenAPI: Accepts включил бы
            // проверку Content-Type в маршрутизации, и multipart получал бы 415 от служебного эндпоинта.
            .AddOpenApiOperationTransformer(async (operation, context, ct) =>
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new() { Schema = await context.GetOrCreateSchemaAsync(typeof(PublicScreeningJson), null, ct) },
                        ["multipart/form-data"] = new() { Schema = MultipartSchema },
                    },
                };
            })
            .Produces<PublicScreeningResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/usage", (ClaimsPrincipal user, IPublicScreeningService service, CancellationToken ct) =>
                service.GetUsageAsync(GetClientId(user), GetDailyQuota(user), ct))
            .WithSummary("Key usage")
            .WithDescription("Requests made today, the daily quota and when it resets (UTC). Does not count towards the quota.")
            .Produces<PublicUsageResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    // Читает multipart-форму: file, requirements (JSON-строка) и необязательный yearsOfExperience.
    private static async Task<PublicScreeningRequest> ReadFormAsync(HttpRequest http, CancellationToken ct)
    {
        IFormCollection form;
        try
        {
            form = await http.ReadFormAsync(ct);
        }
        catch (InvalidDataException)
        {
            throw new BadRequestException("The form is malformed.");
        }

        var file = form.Files.GetFile("file");
        if (file is not null && file.Length > ResumeReader.MaxFileSize)
            throw new BadHttpRequestException("The file must not exceed 10 MB.", StatusCodes.Status413PayloadTooLarge);

        VacancyRequirements? requirements = null;
        var requirementsJson = form["requirements"].ToString();
        if (requirementsJson.Length > 0)
        {
            try
            {
                requirements = JsonSerializer.Deserialize<VacancyRequirements>(requirementsJson, JsonSerializerOptions.Web);
            }
            catch (JsonException)
            {
                throw new BadRequestException("requirements must be valid JSON.");
            }
        }

        int? years = null;
        var yearsText = form["yearsOfExperience"].ToString();
        if (yearsText.Length > 0)
        {
            if (!int.TryParse(yearsText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                throw new BadRequestException("yearsOfExperience must be a whole number.");
            years = parsed;
        }

        var content = file is null ? null : await file.ReadAllBytesAsync(ct);
        return new PublicScreeningRequest(content, null, requirements, years);
    }

    // Читает JSON-тело: text, requirements и необязательный yearsOfExperience.
    private static async Task<PublicScreeningRequest> ReadJsonAsync(HttpRequest http, CancellationToken ct)
    {
        if (!http.HasJsonContentType())
            throw new BadRequestException("Send multipart/form-data with a file or application/json with a text.");

        PublicScreeningJson? body;
        try
        {
            body = await http.ReadFromJsonAsync<PublicScreeningJson>(ct);
        }
        // InvalidOperationException здесь — неизвестная кодировка в Content-Type (charset=foo).
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new BadRequestException("The request body must be valid JSON.");
        }

        if (body is null)
            throw new BadRequestException("The request body is required.");
        return new PublicScreeningRequest(null, body.Text, body.Requirements, body.YearsOfExperience);
    }

    // Достаёт id клиента публичного API из claim NameIdentifier.
    private static Guid GetClientId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Достаёт дневную квоту клиента из claims, выданных при проверке ключа.
    private static int GetDailyQuota(ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(PublicApiKeyHandler.DailyQuotaClaim)!, CultureInfo.InvariantCulture);
}
