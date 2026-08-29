using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public static class PostgresPartyAgingProjectionLoader
{
    public static async ValueTask<ValidatedPartyAgingReport?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid agingReportId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (agingReportId == Guid.Empty)
        {
            throw new ArgumentException("Aging report ID is required.", nameof(agingReportId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string headerSql = """
            SELECT projection_generation_id,party_account_id,control_account_id,balance_side
            FROM reporting.party_aging_projection
            WHERE tenant_id=$1 AND company_id=$2 AND aging_report_id=$3
            """;
        Guid generationId;
        Guid partyAccountId;
        Guid controlAccountId;
        PartyBalanceSide balanceSide;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(scope.TenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(agingReportId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            generationId = reader.GetGuid(0);
            partyAccountId = reader.GetGuid(1);
            controlAccountId = reader.GetGuid(2);
            balanceSide = (PartyBalanceSide)reader.GetInt16(3);
        }

        LoadedProjectionGeneration manifest = await PostgresProjectionGenerationLoader.LoadAsync(
            connection, transaction, scope, companyId, generationId, cancellationToken)
            ?? throw new PartyAgingProjectionCorruptException(agingReportId);
        CalendarDayAgingPolicySnapshot policy = await PostgresAgingPolicyProjectionLoader.LoadAsync(
            connection, transaction, scope, companyId, generationId, cancellationToken)
            ?? throw new PartyAgingProjectionCorruptException(agingReportId);

        const string itemSql = """
            SELECT open_item_id,source_event_id,due_schedule_line_id,original_amount,remaining_amount,
                   due_date,is_disputed,is_blocked
            FROM reporting.party_aging_projection_item
            WHERE tenant_id=$1 AND company_id=$2 AND aging_report_id=$3 ORDER BY item_ordinal
            """;
        var items = new List<OpenItemAgingSnapshot>();
        await using (var command = new NpgsqlCommand(itemSql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(agingReportId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(OpenItemAgingSnapshot.Create(
                    reader.GetGuid(0), scope.TenantId, companyId, partyAccountId, controlAccountId,
                    reader.GetGuid(1), reader.GetGuid(2), manifest.Slice.Currency,
                    reader.GetDecimal(3), reader.GetDecimal(4), reader.GetFieldValue<DateOnly>(5),
                    manifest.Slice.EffectiveAsOf, manifest.Slice.DataCutoffAt,
                    reader.GetBoolean(6), reader.GetBoolean(7)));
            }
        }

        try
        {
            return ValidatedPartyAgingReport.Create(
                agingReportId, partyAccountId, controlAccountId, balanceSide,
                manifest.Slice, policy, items);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new PartyAgingProjectionCorruptException(agingReportId, exception);
        }
    }
}

public sealed class PartyAgingProjectionCorruptException : InvalidOperationException
{
    public PartyAgingProjectionCorruptException(Guid agingReportId, Exception? innerException = null)
        : base("Persisted party aging projection cannot be reconstructed safely.", innerException)
    {
        AgingReportId = agingReportId;
    }

    public string Code { get; } = "PARTY_AGING_PROJECTION_CORRUPT";
    public Guid AgingReportId { get; }
}
