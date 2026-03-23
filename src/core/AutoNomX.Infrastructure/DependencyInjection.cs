using AutoNomX.Domain.Interfaces;
using AutoNomX.Infrastructure.Persistence;
using AutoNomX.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoNomX.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL + EF Core
        services.AddDbContext<AutoNomXDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(AutoNomXDbContext).Assembly.FullName)
            ));

        // Unit of Work
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AutoNomXDbContext>());

        // Repositories
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IPipelineRunRepository, PipelineRunRepository>();
        services.AddScoped<ICoderWorkerRepository, CoderWorkerRepository>();
        services.AddScoped<IAgentHistoryRepository, AgentHistoryRepository>();
        services.AddScoped<IAgentMetricsRepository, AgentMetricsRepository>();
        services.AddScoped<IProjectFileRepository, ProjectFileRepository>();

        // TODO (M1-#13): Register EventBus

        return services;
    }
}
