using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record AuthorizedPartyReportQueryRequest(
    ExecutionScope Scope,
    Guid CompanyId,
    string ExpectedReportCode,
    long ExpectedReportDefinitionVersion,
    string RequiredPermissionCode,
    Guid CrossFootId,
    Guid StatementId,
    Guid AgingReportId);

public static class AuthorizedPartyReportQuery
{
    public static async ValueTask<PartyStatementAgingCrossFoot?> ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedPartyReportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        request.Scope.EnsureAllowed(request.Scope.TenantId, request.CompanyId);
        string reportCode = request.ExpectedReportCode?.Trim()
            ?? throw new ArgumentException("Expected report code is required.", nameof(request));
        if (reportCode.Length == 0 || request.ExpectedReportDefinitionVersion <= 0)
        {
            throw new ArgumentException("Expected report code and definition version are required.", nameof(request));
        }
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
        PartyStatementAgingCrossFoot? result = await PostgresPartyReportCrossFootLoader.LoadAsync(
            connection, transaction, request.Scope, request.CompanyId, request.CrossFootId,
            request.StatementId, request.AgingReportId, cancellationToken);
        if (result is null)
        {
            return null;
        }
        var slice = result.Statement.ReportSlice;
        return string.Equals(slice.ReportCode, reportCode, StringComparison.Ordinal) &&
               slice.ReportDefinitionVersion == request.ExpectedReportDefinitionVersion
            ? result
            : null;
    }
}

public sealed class PartyReportQueryDeniedException : Exception
{
    public PartyReportQueryDeniedException() : base("Party report query is not allowed in the active scope.")
    {
    }

    public string Code { get; } = "PARTY_REPORT_QUERY_DENIED";
}
