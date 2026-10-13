using HireFlow.Api.Vacancies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<VacancyStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "HireFlow API работает");
app.MapGet("/health", () => new {status = "ok", time = DateTime.UtcNow});
app.MapVacancyEndpoints();

app.Run();