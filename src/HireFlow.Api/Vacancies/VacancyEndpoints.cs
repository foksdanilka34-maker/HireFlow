namespace HireFlow.Api.Vacancies;

public record CreateVacancyRequest(
    string Title, string? Description, VacancyRequirements Requirements
);

public record UpdateVacancyRequest(
    string Title, string? Description, VacancyRequirements Requirements, bool IsOpen
);

public static class VacancyEndpoints
{
    public static void MapVacancyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/vacancies");

        group.MapGet("/", (VacancyStore store) => store.GetAll());
        group.MapGet("/{id:int}", (int id, VacancyStore store) => {
            var vacancy = store.Get(id);
            return vacancy is null ? Results.NotFound() : Results.Ok(vacancy);
        });

        group.MapPost("/", (CreateVacancyRequest request, VacancyStore store) =>
        {
            Console.WriteLine(request);
            var error = Validate(request.Title, request.Requirements);
            if (error is not null)
                return Results.BadRequest(new {error});

            var vacancy = store.Add(request.Title, request.Description, request.Requirements);
            return Results.Created($"/vacancies/{vacancy.Id}", vacancy);
        });

        group.MapDelete("/{id:int}", (int id, VacancyStore store) =>
            store.Delete(id) ? Results.NoContent() : Results.NotFound());
    }

    private static string? Validate(string title, VacancyRequirements? requirements)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Title is required";
        if (requirements is null)
            return "Requirements are required";
        if (requirements.RequiredSkills is null || requirements.RequiredSkills.Count == 0)
            return "At least one required skill is needed";
        if (requirements.MinYearsOfExperience < 0)
            return "Experience cannot be negative";
        if (requirements.PassingScore is < 0 or > 100)
            return "Passing score must be between 0 and 100";
        return null;
    }
}