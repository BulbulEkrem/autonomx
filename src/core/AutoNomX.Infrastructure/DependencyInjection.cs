using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace AutoNomX.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO (M1): Register DbContext, Repositories, EventBus, gRPC clients
        return services;
    }
}
