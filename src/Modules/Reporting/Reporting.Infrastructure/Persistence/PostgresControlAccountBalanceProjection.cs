using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record ControlAccountBalanceProjectionPersistenceResult(Guid SnapshotId, bool Created);

public static class PostgresControlAccountBalanceProjectionWriter
{
    public static async ValueTask<ControlAccountBalanceProjectionPersistenceResult> PersistAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ExecutionScope scope,
        ControlAccountBalanceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        scope.EnsureAllowed(snapshot.ReportSlice.TenantId, snapshot.ReportSlice.CompanyId);
        const string sql = """
            INSERT INTO reporting.control_account_balance_projection
             (tenant_id,company_id,projection_generation_id,snapshot_id,control_account_id,ledger_side,
              opening_balance,debits,credits,closing_balance,row_count,source_checksum_sha256)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            ON CONFLICT (tenant_id,company_id,snapshot_id) DO NOTHING RETURNING snapshot_id
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            object[] values = [snapshot.ReportSlice.TenantId, snapshot.ReportSlice.CompanyId,
                snapshot.ReportSlice.ProjectionGenerationId, snapshot.SnapshotId, snapshot.ControlAccountId,
                (short)snapshot.LedgerSide, snapshot.OpeningBalance, snapshot.Debits, snapshot.Credits,
                snapshot.ClosingBalance, snapshot.RowCount, snapshot.SourceChecksumSha256];
            foreach (object value in values) command.Parameters.AddWithValue(value);
            if (await command.ExecuteScalarAsync(cancellationToken) is Guid insertedId)
                return new ControlAccountBalanceProjectionPersistenceResult(insertedId, true);
        }
        ControlAccountBalanceSnapshot? existing = await PostgresControlAccountBalanceProjectionLoader.LoadAsync(
            connection, transaction, scope, snapshot.ReportSlice.CompanyId, snapshot.SnapshotId, cancellationToken);
        if (existing is null || !Matches(existing, snapshot))
            throw new ControlAccountBalanceProjectionPersistenceConflictException(snapshot.SnapshotId);
        return new ControlAccountBalanceProjectionPersistenceResult(snapshot.SnapshotId, false);
    }

    private static bool Matches(ControlAccountBalanceSnapshot left, ControlAccountBalanceSnapshot right) =>
        left.SnapshotId == right.SnapshotId && left.ControlAccountId == right.ControlAccountId &&
        left.LedgerSide == right.LedgerSide && left.OpeningBalance == right.OpeningBalance &&
        left.Debits == right.Debits && left.Credits == right.Credits && left.ClosingBalance == right.ClosingBalance &&
        left.RowCount == right.RowCount && left.SourceChecksumSha256 == right.SourceChecksumSha256 &&
        left.ReportSlice.ProjectionGenerationId == right.ReportSlice.ProjectionGenerationId;
}

public static class PostgresControlAccountBalanceProjectionLoader
{
    public static async ValueTask<ControlAccountBalanceSnapshot?> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ExecutionScope scope,
        Guid companyId, Guid snapshotId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        if (snapshotId == Guid.Empty) throw new ArgumentException("Snapshot ID is required.", nameof(snapshotId));
        scope.EnsureAllowed(scope.TenantId, companyId);
        const string sql = """
            SELECT projection_generation_id,control_account_id,ledger_side,opening_balance,debits,credits,
                   closing_balance,row_count,source_checksum_sha256
            FROM reporting.control_account_balance_projection
            WHERE tenant_id=$1 AND company_id=$2 AND snapshot_id=$3
            """;
        Guid generationId; Guid controlAccountId; LedgerSide side; decimal opening; decimal debits;
        decimal credits; decimal closing; long rowCount; string checksum;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId); command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(snapshotId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            generationId = reader.GetGuid(0); controlAccountId = reader.GetGuid(1); side = (LedgerSide)reader.GetInt16(2);
            opening = reader.GetDecimal(3); debits = reader.GetDecimal(4); credits = reader.GetDecimal(5);
            closing = reader.GetDecimal(6); rowCount = reader.GetInt64(7); checksum = reader.GetString(8);
        }
        LoadedProjectionGeneration manifest = await PostgresProjectionGenerationLoader.LoadAsync(
            connection, transaction, scope, companyId, generationId, cancellationToken)
            ?? throw new ControlAccountBalanceProjectionCorruptException(snapshotId);
        try
        {
            return ControlAccountBalanceSnapshot.Create(snapshotId, controlAccountId, side, opening, debits,
                credits, closing, rowCount, checksum, manifest.Slice);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ControlAccountBalanceProjectionCorruptException(snapshotId, exception);
        }
    }
}

public static class PostgresControlAccountReconciliationLoader
{
    public static async ValueTask<ControlAccountReconciliationResult?> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId,
        Guid reconciliationId, Guid subledgerSnapshotId, Guid generalLedgerSnapshotId,
        CancellationToken cancellationToken = default)
    {
        ControlAccountBalanceSnapshot? subledger = await PostgresControlAccountBalanceProjectionLoader.LoadAsync(
            connection, transaction, scope, companyId, subledgerSnapshotId, cancellationToken);
        if (subledger is null) return null;
        ControlAccountBalanceSnapshot? generalLedger = await PostgresControlAccountBalanceProjectionLoader.LoadAsync(
            connection, transaction, scope, companyId, generalLedgerSnapshotId, cancellationToken);
        return generalLedger is null ? null :
            ControlAccountReconciliationResult.Create(reconciliationId, subledger, generalLedger);
    }
}

public sealed class ControlAccountBalanceProjectionPersistenceConflictException(Guid snapshotId)
    : InvalidOperationException("The balance snapshot ID already has different immutable projection content.")
{
    public string Code { get; } = "CONTROL_ACCOUNT_BALANCE_PROJECTION_CONFLICT";
    public Guid SnapshotId { get; } = snapshotId;
}

public sealed class ControlAccountBalanceProjectionCorruptException : InvalidOperationException
{
    public ControlAccountBalanceProjectionCorruptException(Guid snapshotId, Exception? innerException = null)
        : base("Persisted control-account balance projection cannot be reconstructed safely.", innerException) => SnapshotId = snapshotId;
    public string Code { get; } = "CONTROL_ACCOUNT_BALANCE_PROJECTION_CORRUPT";
    public Guid SnapshotId { get; }
}
