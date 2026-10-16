using HireFlow.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireFlow.Infrastructure.Persistence.Configurations;

public class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>
{
    // Описывает, как Vacancy хранится в таблице vacancies.
    public void Configure(EntityTypeBuilder<Vacancy> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(v => v.Title).HasMaxLength(Vacancy.TitleMaxLength);
        builder.Property(v => v.Direction).HasMaxLength(Vacancy.DirectionMaxLength);

        builder.Property(v => v.Status)
            .HasConversion(new SnakeCaseEnumConverter<VacancyStatus>())
            .HasMaxLength(20);

        builder.ComplexProperty(v => v.Requirements, requirements =>
        {
            requirements.ToJson();

            ConfigureSkills(requirements.ComplexCollection(r => r.RequiredSkills), "required_skills");
            ConfigureSkills(requirements.ComplexCollection(r => r.NiceToHaveSkills), "nice_to_have_skills");
            requirements.Property(r => r.MinYearsOfExperience).HasJsonPropertyName("min_years_of_experience");
            requirements.Property(r => r.PassingScore).HasJsonPropertyName("passing_score");
            requirements.Property(r => r.BorderlineWidth).HasJsonPropertyName("borderline_width");
        });
    }

    // Задаёт snake_case-имена ключей для списка навыков внутри jsonb.
    private static void ConfigureSkills(ComplexCollectionBuilder<Skill> skills, string jsonName)
    {
        skills.HasJsonPropertyName(jsonName);
        skills.Property(s => s.Name).HasJsonPropertyName("name");
        skills.PrimitiveCollection(s => s.Synonyms).HasJsonPropertyName("synonyms");
    }
}
