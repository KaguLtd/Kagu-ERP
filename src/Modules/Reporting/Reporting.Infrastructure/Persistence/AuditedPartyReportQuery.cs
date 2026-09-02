using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public delegate ValueTask AppendPartyReportAudit(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    RequestAuditContext context,
    Guid auditEventId,
    AuthorizationAuditEvent auditEvent,
    CancellationToken cancellationToken);

public sealed record AuditedPartyReportQueryRequest(
    AuthorizedPartyReportQueryRequest Query,
    RequestAuditContext AuditContext,
    Guid AuditEventId);

public static class AuditedPartyReportQuery
{
    public static async ValueTask<PartyStatementAgingCrossFoot?> ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuditedPartyReportQueryRequest request,
        AppendPartyReportAudit appendAudit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(appendAudit);
        ValidateAuditContext(request);
        if (string.IsNullOrWhiteSpace(request.Query.RequiredPermissionCode))
        {
            throw new ArgumentException("Required report permission code is required.", nameof(request));
        }
        bool companyAllowed = request.Query.Scope.Allows(
            request.Query.Scope.TenantId, request.Query.CompanyId);
        bool permitted = companyAllowed && request.Query.Scope.HasPermission(
            request.Query.CompanyId, request.Query.RequiredPermissionCode.Trim());
        if (!permitted)
        {
            await appendAudit(connection, transaction, request.AuditContext, request.AuditEventId,
                new AuthorizationAuditEvent(
                    "report.party.query", "party-report", null, "denied", "PARTY_REPORT_QUERY_DENIED"),
                cancellationToken);
            throw new PartyReportQueryDeniedException();
        }

        PartyStatementAgingCrossFoot? result = await AuthorizedPartyReportQuery.ExecuteAsync(
            connection, transaction, request.Query, cancellationToken);
        await appendAudit(connection, transaction, request.AuditContext, request.AuditEventId,
            new AuthorizationAuditEvent(
                "report.party.query", "party-report",
                result?.CrossFootId.ToString("D"), result is null ? "denied" : "allowed",
                result is null ? "PARTY_REPORT_NOT_FOUND" : "PARTY_REPORT_QUERY_ALLOWED"),
            cancellationToken);
        return result;
    }

    private static void ValidateAuditContext(AuditedPartyReportQueryRequest request)
    {
        if (request.AuditEventId == Guid.Empty || request.AuditContext.TenantId != request.Query.Scope.TenantId ||
            request.AuditContext.ActorId != request.Query.Scope.ActorId ||
            !request.AuditContext.CompanyIds.SetEquals(request.Query.Scope.CompanyIds))
        {
            throw new ArgumentException("Party report audit context must match the trusted execution scope.", nameof(request));
        }
    }
}
