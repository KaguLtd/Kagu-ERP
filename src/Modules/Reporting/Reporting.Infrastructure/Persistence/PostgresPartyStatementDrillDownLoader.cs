using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public static class PostgresPartyStatementDrillDownLoader
{
    public static async ValueTask<PartyStatementDrillDownAnchor?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid projectionGenerationId,
        Guid statementId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (projectionGenerationId == Guid.Empty || statementId == Guid.Empty || eventId == Guid.Empty)
        {
            throw new ArgumentException("Projection generation, statement and event IDs are required.");
        }
        scope.EnsureAllowed(scope.TenantId, companyId);
        ValidatedPartyStatement? statement = await PostgresPartyStatementProjectionLoader.LoadAsync(
            connection, transaction, scope, companyId, statementId, cancellationToken);
        if (statement is null || statement.ReportSlice.ProjectionGenerationId != projectionGenerationId)
        {
            return null;
        }
        PartyStatementLine? line = statement.Lines.SingleOrDefault(
            candidate => candidate.EventSnapshot.EventId == eventId);
        return line is null
            ? null
            : PartyStatementDrillDownAnchor.Create(
                statement.StatementId, statement.ReportSlice, line.EventSnapshot, line.RunningExposure);
    }
}
