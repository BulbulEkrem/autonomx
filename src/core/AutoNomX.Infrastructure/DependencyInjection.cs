using AutoNomX.Domain.Interfaces;
using AutoNomX.Infrastructure.Persistence;
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

        // TODO (M1-#12): Register Repository implementations
        // TODO (M1-#13): Register EventBus

        return services;
    }
}
