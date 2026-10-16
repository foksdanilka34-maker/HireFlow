using HireFlow.Application.Candidates;
using HireFlow.Application.JobApplications;
using HireFlow.Application.Matching;
using HireFlow.Application.Notifications;
using HireFlow.Application.Resumes;
using HireFlow.Application.Vacancies;
using Microsoft.Extensions.DependencyInjection;

namespace HireFlow.Application;

public static class DependencyInjection
{
    // Регистрирует в DI сервисы слоя Application.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IVacancyService, VacancyService>();
        services.AddScoped<ICandidateService, CandidateService>();
        services.AddScoped<IJobApplicationService, JobApplicationService>();
        services.AddScoped<IResumeReader, ResumeReader>();
        services.AddScoped<IVacancyMatchingService, VacancyMatchingService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
