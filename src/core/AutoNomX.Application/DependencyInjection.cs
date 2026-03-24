using AutoNomX.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AutoNomX.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // MediatR (CQRS)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Application Services
        services.AddScoped<TaskBoardService>();
        services.AddScoped<WorkerPoolService>();
        services.AddScoped<ChatService>();
        services.AddScoped<MetricsService>();
        services.AddScoped<ModelManagerService>();
        services.AddScoped<OrchestratorService>();

        // Background event handler
        services.AddHostedService<PipelineEventHandler>();

        return services;
    }
}
