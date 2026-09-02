using KaguERP.Modules.Reporting.Application.PartyReports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KaguERP.Bootstrap;

public sealed record PartyReportRefreshWorkerRuntimeOptions(
    bool Enabled,
    TimeSpan PollInterval);

internal sealed record PartyReportRefreshWorkerSettings(
    Guid TenantId,
    Guid ActorId,
    IReadOnlyList<Guid> CompanyIds,
    TimeSpan PollInterval,
    TimeSpan LeaseDuration);

public static class PartyReportRefreshWorkerServiceCollectionExtensions
{
    private const string TenantKey = "KAGU_ERP_REPORT_WORKER_TENANT_ID";
    private const string ActorKey = "KAGU_ERP_REPORT_WORKER_ACTOR_ID";
    private const string CompanyKey = "KAGU_ERP_REPORT_WORKER_COMPANY_IDS";
    private const string PollKey = "KAGU_ERP_REPORT_WORKER_POLL_INTERVAL_SECONDS";
    private const string LeaseKey = "KAGU_ERP_REPORT_WORKER_LEASE_SECONDS";

    public static IServiceCollection AddKaguErpPartyReportRefreshWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string? tenantText = configuration[TenantKey];
        string? actorText = configuration[ActorKey];
        string? companyText = configuration[CompanyKey];
        bool hasAnyIdentitySetting =
            !string.IsNullOrWhiteSpace(tenantText) ||
            !string.IsNullOrWhiteSpace(actorText) ||
            !string.IsNullOrWhiteSpace(companyText);
        if (!hasAnyIdentitySetting)
        {
            services.TryAddSingleton(new PartyReportRefreshWorkerRuntimeOptions(false, TimeSpan.FromSeconds(5)));
            return services;
        }
        if (string.IsNullOrWhiteSpace(configuration["KAGU_ERP_APP_CONNECTION_STRING"]))
        {
            throw new InvalidOperationException(
                "KAGU_ERP_APP_CONNECTION_STRING is required when the Party report Worker is configured.");
        }
        if (!Guid.TryParse(tenantText, out Guid tenantId) || tenantId == Guid.Empty)
        {
            throw InvalidSetting(TenantKey);
        }
        if (!Guid.TryParse(actorText, out Guid actorId) || actorId == Guid.Empty)
        {
            throw InvalidSetting(ActorKey);
        }
        Guid[] companyIds = ParseCompanyIds(companyText);
        TimeSpan pollInterval = ParseSeconds(configuration[PollKey], PollKey, 1, 300, 5);
        TimeSpan leaseDuration = ParseSeconds(configuration[LeaseKey], LeaseKey, 5, 900, 120);
        var settings = new PartyReportRefreshWorkerSettings(
            tenantId,
            actorId,
            companyIds,
            pollInterval,
            leaseDuration);
        services.TryAddSingleton(settings);
        services.TryAddSingleton(new PartyReportRefreshWorkerRuntimeOptions(true, pollInterval));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PartyReportWorkerExecutionScopeProvider>();
        services.TryAddSingleton<IPartyReportRefreshCycle, PostgresScopedPartyReportRefreshCycle>();
        return services;
    }

    private static Guid[] ParseCompanyIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidSetting(CompanyKey);
        }
        string[] parts = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 100)
        {
            throw InvalidSetting(CompanyKey);
        }
        var result = new List<Guid>(parts.Length);
        foreach (string part in parts)
        {
            if (!Guid.TryParse(part, out Guid companyId) || companyId == Guid.Empty)
            {
                throw InvalidSetting(CompanyKey);
            }
            result.Add(companyId);
        }
        Guid[] unique = result.Distinct().Order().ToArray();
        if (unique.Length != result.Count)
        {
            throw new InvalidOperationException($"{CompanyKey} cannot contain duplicate company IDs.");
        }
        return unique;
    }

    private static TimeSpan ParseSeconds(
        string? value,
        string key,
        int minimum,
        int maximum,
        int defaultValue)
    {
        int seconds = string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : throw InvalidSetting(key);
        if (seconds < minimum || seconds > maximum)
        {
            throw InvalidSetting(key);
        }
        return TimeSpan.FromSeconds(seconds);
    }

    private static InvalidOperationException InvalidSetting(string key) =>
        new($"{key} is missing or invalid; Party report Worker startup is fail-closed.");
}
