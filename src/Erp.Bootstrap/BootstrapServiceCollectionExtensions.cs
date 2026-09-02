using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Observability;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Application.PartyReports;
using KaguERP.Modules.Reporting.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace KaguERP.Bootstrap;

public static class BootstrapServiceCollectionExtensions
{
    public static IServiceCollection AddKaguErpBootstrap(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string? connectionString = configuration["KAGU_ERP_APP_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.TryAddScoped<IExecutionScopeResolver, DenyAllExecutionScopeResolver>();
            services.TryAddScoped<IAuthorizationAuditWriter, UnavailableAuthorizationAuditWriter>();
            services.TryAddScoped<IReadinessProbe, UnavailableReadinessProbe>();
            services.TryAddScoped<IPartyAccountDetailReportQuery, UnavailablePartyAccountDetailReportQuery>();
        }
        else
        {
            services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            services.TryAddSingleton<AppendPartyReportAudit>(
                _ => PostgresAuthorizationAuditWriter.AppendAsync);
            services.TryAddScoped<IExecutionScopeResolver, PostgresExecutionScopeResolver>();
            services.TryAddScoped<IAuthorizationAuditWriter, PostgresAuthorizationAuditWriter>();
            services.TryAddScoped<IReadinessProbe, PostgresReadinessProbe>();
            services.TryAddScoped<IPartyAccountDetailReportQuery, PostgresPartyAccountDetailReportQuery>();
        }

        return services;
    }
}
