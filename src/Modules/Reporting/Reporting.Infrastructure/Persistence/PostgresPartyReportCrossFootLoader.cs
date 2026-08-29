using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public static class PostgresPartyReportCrossFootLoader
{
    public static async ValueTask<PartyStatementAgingCrossFoot?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid crossFootId,
        Guid statementId,
        Guid agingReportId,
        CancellationToken cancellationToken = default)
    {
        if (crossFootId == Guid.Empty)
        {
            throw new ArgumentException("Cross-foot ID is required.", nameof(crossFootId));
        }
        ValidatedPartyStatement? statement = await PostgresPartyStatementProjectionLoader.LoadAsync(
            connection, transaction, scope, companyId, statementId, cancellationToken);
        if (statement is null)
        {
            return null;
        }
        ValidatedPartyAgingReport? aging = await PostgresPartyAgingProjectionLoader.LoadAsync(
            connection, transaction, scope, companyId, agingReportId, cancellationToken);
        return aging is null ? null : PartyStatementAgingCrossFoot.Create(crossFootId, statement, aging);
    }
}
