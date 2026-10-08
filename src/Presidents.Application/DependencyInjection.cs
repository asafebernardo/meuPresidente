using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Presidents.Application.Rag;
using Presidents.Application.Services;
using Presidents.Application.Validation;

namespace Presidents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<PresidentFormValidator>();
        services.AddScoped<PresidentService>();
        services.AddScoped<PresidencyService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<EventService>();
        services.AddScoped<LawService>();
        services.AddScoped<PolicyService>();
        services.AddScoped<SourceService>();
        services.AddScoped<IndicatorService>();
        services.AddScoped<StatementService>();
        services.AddScoped<PublicationService>();
        services.AddScoped<AdminCatalogService>();
        services.AddScoped<QueryService>();
        services.AddScoped<ImportCoordinator>();
        services.AddScoped<IHistoricalAnswerService, HistoricalAnswerService>();
        return services;
    }
}
