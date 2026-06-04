using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Application.Common.Behaviors;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Application.Features.OsuIntegration.Synchronization.Services;
using System.Reflection;

namespace OsuStocks.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        var mappingConfig = TypeAdapterConfig.GlobalSettings;
        mappingConfig.Scan(assembly);

        services.AddSingleton(mappingConfig);
        services.AddScoped<IMapper, ServiceMapper>();

        services.AddScoped<ISnapshotComparisonService, SnapshotComparisonService>();
        services.AddScoped<IPlayerSynchronizationService, PlayerSynchronizationService>();

        return services;
    }
}
