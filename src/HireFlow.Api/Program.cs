using System.Text.Json;
using System.Text.Json.Serialization;
using HireFlow.Api.Candidates;
using HireFlow.Api.Errors;
using HireFlow.Api.JobApplications;
using HireFlow.Api.Notifications;
using HireFlow.Api.Vacancies;
using HireFlow.Application;
using HireFlow.Application.Notifications;
using HireFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Postgres' is not set.");
var storagePath = builder.Configuration["Storage:LocalPath"]
    ?? throw new InvalidOperationException("Storage path 'Storage:LocalPath' is not set.");

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, storagePath);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "HireFlow API работает");
app.MapGet("/health", () => new { status = "ok", time = DateTime.UtcNow });
app.MapVacancyEndpoints();
app.MapCandidateEndpoints();
app.MapJobApplicationEndpoints();
app.MapNotificationEndpoints();

app.Run();
