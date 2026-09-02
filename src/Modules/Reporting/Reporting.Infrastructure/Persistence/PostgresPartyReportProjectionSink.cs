using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Application.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed class PostgresPartyReportProjectionSink(
    NpgsqlDataSource dataSource,
    ExecutionScope scope) : IPartyReportProjectionSink
{
    public async ValueTask<PartyReportProjectionJobResult> PublishAsync(
        PartyReportProjectionPublication publication,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publication);
        scope.EnsureAllowed(publication.Source.TenantId, publication.Source.CompanyId);
        EnsurePublicationContext(publication);

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);
        var generation = new ProjectionGenerationPersistenceCommand(
            scope, publication.Pair.Statement.ReportSlice, publication.Command.GenerationReason,
            publication.Source.SourceWatermarkFrom, publication.Source.SourceWatermarkTo,
            publication.Source.SourceChecksumSha256);
        PartyReportProjectionPublicationResult result = await PostgresPartyReportProjectionPublisher.PublishAsync(
            connection, transaction,
            new PartyReportProjectionPublicationCommand(
                generation, publication.Pair.Statement, publication.Pair.Aging,
                publication.ControlAccounts.Subledger, publication.ControlAccounts.GeneralLedger,
                publication.Command.PartyCrossFootId, publication.Command.ControlAccountReconciliationId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PartyReportProjectionJobResult(
            result.ProjectionGenerationId, result.StatementId, result.AgingReportId, result.Created);
    }

    private static void EnsurePublicationContext(PartyReportProjectionPublication publication)
    {
        PartyReportProjectionJobCommand command = publication.Command;
        var source = publication.Source;
        var statement = publication.Pair.Statement;
        var aging = publication.Pair.Aging;
        var slice = statement.ReportSlice;
        bool queryMatches = command.SourceQuery.TenantId == source.TenantId &&
            command.SourceQuery.CompanyId == source.CompanyId &&
            command.SourceQuery.PartyAccountId == source.PartyAccountId &&
            command.SourceQuery.EffectiveAsOf == source.EffectiveAsOf &&
            command.SourceQuery.RecordedCutoff == source.RecordedCutoff;
        bool reportMatches = statement.PartyAccountId == source.PartyAccountId &&
            aging.PartyAccountId == source.PartyAccountId &&
            statement.ControlAccountId == source.ControlAccountId &&
            aging.ControlAccountId == source.ControlAccountId &&
            slice.TenantId == source.TenantId && slice.CompanyId == source.CompanyId &&
            slice.Currency.Value == source.Currency && slice.EffectiveAsOf == source.EffectiveAsOf &&
            slice.DataCutoffAt == source.RecordedCutoff && slice.ReportCode == command.ReportCode &&
            slice.ReportDefinitionVersion == command.ReportDefinitionVersion &&
            slice.ProjectionGenerationId == command.ProjectionGenerationId &&
            statement.StatementId == command.StatementId && aging.AgingReportId == command.AgingReportId &&
            publication.Pair.CrossFoot.CrossFootId == command.PartyCrossFootId;
        if (!queryMatches || !reportMatches)
        {
            throw new PartyReportProjectionSinkException();
        }
    }

    private async ValueTask SetExecutionContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                set_config('app.tenant_id', $1, true),
                set_config('app.actor_id', $2, true),
                set_config('app.company_ids', $3, true)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId.ToString());
        command.Parameters.AddWithValue(scope.ActorId.ToString());
        command.Parameters.AddWithValue("{" + string.Join(',', scope.CompanyIds.Order()) + "}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class PartyReportProjectionSinkException()
    : InvalidOperationException("The publication context does not match its Party source and job command."),
      IPartyReportRefreshFailure
{
    public string Code { get; } = "PARTY_REPORT_PUBLICATION_CONTEXT_MISMATCH";
}
