using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record AuthorizedPartyReportQueryRequest(
    ExecutionScope Scope,
    Guid CompanyId,
    string RequiredPermissionCode,
    Guid CrossFootId,
    Guid StatementId,
    Guid AgingReportId);

public static class AuthorizedPartyReportQuery
{
    public static ValueTask<PartyStatementAgingCrossFoot?> ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedPartyReportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        request.Scope.EnsureAllowed(request.Scope.TenantId, request.CompanyId);
        string permissionCode = request.RequiredPermissionCode?.Trim()
            ?? throw new ArgumentException("Required report permission code is required.", nameof(request));
        if (permissionCode.Length == 0)
        {
            throw new ArgumentException("Required report permission code is required.", nameof(request));
        }
        if (!request.Scope.HasPermission(request.CompanyId, permissionCode))
        {
            throw new PartyReportQueryDeniedException();
        }
        return PostgresPartyReportCrossFootLoader.LoadAsync(
            connection, transaction, request.Scope, request.CompanyId, request.CrossFootId,
            request.StatementId, request.AgingReportId, cancellationToken);
    }
}

public sealed class PartyReportQueryDeniedException : Exception
{
    public PartyReportQueryDeniedException() : base("Party report query is not allowed in the active scope.")
    {
    }

    public string Code { get; } = "PARTY_REPORT_QUERY_DENIED";
}
