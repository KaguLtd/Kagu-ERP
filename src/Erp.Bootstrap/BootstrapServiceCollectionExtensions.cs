using Microsoft.Extensions.DependencyInjection;

namespace KaguERP.Bootstrap;

public static class BootstrapServiceCollectionExtensions
{
    public static IServiceCollection AddKaguErpBootstrap(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}

