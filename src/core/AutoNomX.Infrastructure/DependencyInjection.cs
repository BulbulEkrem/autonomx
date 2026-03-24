using AutoNomX.Domain.Interfaces;
using AutoNomX.Infrastructure.EventBus;
using AutoNomX.Infrastructure.Git;
using AutoNomX.Infrastructure.Grpc;
using AutoNomX.Infrastructure.Persistence;
using AutoNomX.Infrastructure.Persistence.Repositories;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IChangeLogRepository, ChangeLogRepository>();

        // Git Service
        services.AddSingleton<IGitService, GitCliService>();

        // EventBus (PostgreSQL LISTEN/NOTIFY)
        services.AddSingleton<PostgresEventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<PostgresEventBus>());

        // Agent Gateway (gRPC client → Python agent runtime)
        services.AddAgentGateway(configuration);

        return services;
    }

    /// <summary>
    /// Registers the gRPC-based <see cref="IAgentGateway"/> with connection pooling and retry.
    /// Configuration key: AgentService:GrpcUrl (default: http://localhost:50051)
    /// </summary>
    public static IServiceCollection AddAgentGateway(this IServiceCollection services, IConfiguration configuration)
    {
        var grpcUrl = configuration["AgentService:GrpcUrl"] ?? "http://localhost:50051";

        services.AddSingleton(sp =>
        {
            var channel = GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
            {
                MaxRetryAttempts = 3,
                MaxReceiveMessageSize = 50 * 1024 * 1024,  // 50 MB
                MaxSendMessageSize = 50 * 1024 * 1024,
            });
            return channel;
        });

        services.AddSingleton<IAgentGateway>(sp =>
        {
            var channel = sp.GetRequiredService<GrpcChannel>();
            var logger = sp.GetRequiredService<ILogger<AgentGrpcClient>>();
            return new AgentGrpcClient(channel, logger);
        });

        return services;
    }
}
