using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record PartyAgingProjectionPersistenceResult(Guid AgingReportId, bool Created);

public static class PostgresPartyAgingProjectionWriter
{
    public static async ValueTask<PartyAgingProjectionPersistenceResult> PersistAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ExecutionScope scope,
        ValidatedPartyAgingReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(report);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        scope.EnsureAllowed(report.ReportSlice.TenantId, report.ReportSlice.CompanyId);

        const string sql = """
            INSERT INTO reporting.party_aging_projection
             (tenant_id,company_id,projection_generation_id,aging_report_id,party_account_id,
              control_account_id,balance_side,total_remaining,item_count)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
            ON CONFLICT (tenant_id,company_id,aging_report_id) DO NOTHING RETURNING aging_report_id
            """;
        await using (var header = new NpgsqlCommand(sql, connection, transaction))
        {
            object[] values = [report.ReportSlice.TenantId, report.ReportSlice.CompanyId,
                report.ReportSlice.ProjectionGenerationId, report.AgingReportId, report.PartyAccountId,
                report.ControlAccountId, (short)report.BalanceSide, report.TotalRemaining, report.Items.Count];
            foreach (object value in values) header.Parameters.AddWithValue(value);
            if (await header.ExecuteScalarAsync(cancellationToken) is Guid insertedId)
            {
                await InsertItemsAsync(connection, transaction, report, cancellationToken);
                return new PartyAgingProjectionPersistenceResult(insertedId, true);
            }
        }
        await ValidateExistingAsync(connection, transaction, report, cancellationToken);
        return new PartyAgingProjectionPersistenceResult(report.AgingReportId, false);
    }

    private static async ValueTask InsertItemsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ValidatedPartyAgingReport report, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO reporting.party_aging_projection_item
             (tenant_id,company_id,aging_report_id,item_ordinal,open_item_id,source_event_id,
              due_schedule_line_id,original_amount,remaining_amount,due_date,is_disputed,is_blocked)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            """;
        for (var index = 0; index < report.Items.Count; index++)
        {
            OpenItemAgingSnapshot item = report.Items[index];
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            object[] values = [item.TenantId, item.CompanyId, report.AgingReportId, index + 1,
                item.OpenItemId, item.SourceEventId, item.DueScheduleLineId, item.OriginalAmount,
                item.RemainingAmount, item.DueDate, item.IsDisputed, item.IsBlocked];
            foreach (object value in values) command.Parameters.AddWithValue(value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async ValueTask ValidateExistingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ValidatedPartyAgingReport report, CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT projection_generation_id,party_account_id,control_account_id,balance_side,total_remaining,item_count
            FROM reporting.party_aging_projection WHERE tenant_id=$1 AND company_id=$2 AND aging_report_id=$3
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(report.ReportSlice.TenantId); header.Parameters.AddWithValue(report.ReportSlice.CompanyId);
            header.Parameters.AddWithValue(report.AgingReportId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetGuid(0) != report.ReportSlice.ProjectionGenerationId ||
                reader.GetGuid(1) != report.PartyAccountId || reader.GetGuid(2) != report.ControlAccountId ||
                reader.GetInt16(3) != (short)report.BalanceSide || reader.GetDecimal(4) != report.TotalRemaining ||
                reader.GetInt32(5) != report.Items.Count) throw new PartyAgingProjectionPersistenceConflictException(report.AgingReportId);
        }
        const string itemSql = """
            SELECT open_item_id,source_event_id,due_schedule_line_id,original_amount,remaining_amount,due_date,is_disputed,is_blocked
            FROM reporting.party_aging_projection_item WHERE tenant_id=$1 AND company_id=$2 AND aging_report_id=$3 ORDER BY item_ordinal
            """;
        await using var items = new NpgsqlCommand(itemSql, connection, transaction);
        items.Parameters.AddWithValue(report.ReportSlice.TenantId); items.Parameters.AddWithValue(report.ReportSlice.CompanyId);
        items.Parameters.AddWithValue(report.AgingReportId);
        await using NpgsqlDataReader itemReader = await items.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await itemReader.ReadAsync(cancellationToken))
        {
            if (index >= report.Items.Count || !Matches(itemReader, report.Items[index]))
                throw new PartyAgingProjectionPersistenceConflictException(report.AgingReportId);
            index++;
        }
        if (index != report.Items.Count) throw new PartyAgingProjectionPersistenceConflictException(report.AgingReportId);
    }

    private static bool Matches(NpgsqlDataReader reader, OpenItemAgingSnapshot item) =>
        reader.GetGuid(0) == item.OpenItemId && reader.GetGuid(1) == item.SourceEventId &&
        reader.GetGuid(2) == item.DueScheduleLineId && reader.GetDecimal(3) == item.OriginalAmount &&
        reader.GetDecimal(4) == item.RemainingAmount && reader.GetFieldValue<DateOnly>(5) == item.DueDate &&
        reader.GetBoolean(6) == item.IsDisputed && reader.GetBoolean(7) == item.IsBlocked;
}

public sealed class PartyAgingProjectionPersistenceConflictException(Guid agingReportId)
    : InvalidOperationException("The aging report ID already has different immutable projection content.")
{
    public string Code { get; } = "PARTY_AGING_PROJECTION_CONFLICT";
    public Guid AgingReportId { get; } = agingReportId;
}
