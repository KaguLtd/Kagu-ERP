using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace KaguERP.Migrator;

internal sealed class MigrationRunner(string connectionString)
{
    private static readonly MigrationDefinition[] Migrations =
    [
        new("0001_platform_and_organization_scope", "0001_platform_and_organization_scope.sql"),
        new("0002_identity_membership_and_permissions", "0002_identity_membership_and_permissions.sql"),
        new("0003_append_only_authorization_audit", "0003_append_only_authorization_audit.sql"),
        new("0004_transactional_outbox", "0004_transactional_outbox.sql"),
        new("0005_accounting_journal_source_reservation", "0005_accounting_journal_source_reservation.sql"),
        new("0006_validated_journal_draft", "0006_validated_journal_draft.sql"),
        new("0007_accounting_period_posting_gate", "0007_accounting_period_posting_gate.sql"),
        new("0008_api_idempotency_record", "0008_api_idempotency_record.sql"),
        new("0009_idempotency_completion_guard", "0009_idempotency_completion_guard.sql"),
        new("0010_account_posting_evidence", "0010_account_posting_evidence.sql"),
        new("0011_posting_dimension_evidence", "0011_posting_dimension_evidence.sql"),
        new("0012_currency_rounding_evidence", "0012_currency_rounding_evidence.sql"),
        new("0013_approval_completion_evidence", "0013_approval_completion_evidence.sql"),
        new("0014_posted_journal", "0014_posted_journal.sql"),
        new("0015_posted_journal_balance_guard", "0015_posted_journal_balance_guard.sql"),
        new("0016_posted_journal_reversal_link", "0016_posted_journal_reversal_link.sql"),
        new("0017_posted_journal_reversal_currency_guard", "0017_posted_journal_reversal_currency_guard.sql"),
        new("0018_party_account_due_schedule", "0018_party_account_due_schedule.sql"),
        new("0019_open_item_impact_event", "0019_open_item_impact_event.sql"),
        new("0020_open_item_capacity_guard", "0020_open_item_capacity_guard.sql"),
        new("0021_open_item_capacity_guard_privilege", "0021_open_item_capacity_guard_privilege.sql"),
        new("0022_payment_economic_event", "0022_payment_economic_event.sql"),
        new("0023_statement_line", "0023_statement_line.sql"),
        new("0024_reconciliation_proposal", "0024_reconciliation_proposal.sql"),
        new("0025_report_projection_generation", "0025_report_projection_generation.sql"),
        new("0026_party_statement_projection", "0026_party_statement_projection.sql"),
        new("0027_aging_policy_projection_snapshot", "0027_aging_policy_projection_snapshot.sql"),
        new("0028_party_aging_projection", "0028_party_aging_projection.sql"),
        new("0029_control_account_balance_projection", "0029_control_account_balance_projection.sql"),
        new("0030_party_account_balance_side_expand", "0030_party_account_balance_side_expand.sql"),
        new("0031_party_account_opening_event", "0031_party_account_opening_event.sql"),
        new("0032_party_due_source_posting_identity_expand", "0032_party_due_source_posting_identity_expand.sql"),
        new("0033_open_item_impact_source_identity_expand", "0033_open_item_impact_source_identity_expand.sql"),
        new("0034_open_item_restriction_event", "0034_open_item_restriction_event.sql"),
        new("0035_open_item_restriction_guard_privilege", "0035_open_item_restriction_guard_privilege.sql"),
        new("0036_authoritative_aging_policy", "0036_authoritative_aging_policy.sql"),
        new("0037_aging_policy_stream_guard_privilege", "0037_aging_policy_stream_guard_privilege.sql"),
        new("0038_service_identity_company_permission", "0038_service_identity_company_permission.sql"),
        new("0039_party_report_refresh_work_item", "0039_party_report_refresh_work_item.sql"),
        new("0040_party_statement_projection_event_scope", "0040_party_statement_projection_event_scope.sql"),
        new("0041_reconciliation_approval", "0041_reconciliation_approval.sql"),
        new("0042_payment_currency_conversion_snapshot", "0042_payment_currency_conversion_snapshot.sql"),
        new("0043_inventory_quantity_movement_foundation", "0043_inventory_quantity_movement_foundation.sql"),
        new("0044_sales_order_lifecycle_foundation", "0044_sales_order_lifecycle_foundation.sql"),
        new("0045_sales_order_line_commitment", "0045_sales_order_line_commitment.sql"),
    ];

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await ExecuteAsync(connection, "SET ROLE kagu_erp_schema_owner", cancellationToken);
        await ExecuteAsync(connection, "SELECT pg_advisory_lock(527614, 20260819)", cancellationToken);

        try
        {
            await EnsureHistoryTableAsync(connection, cancellationToken);
            Dictionary<string, string> applied = await ReadAppliedMigrationsAsync(connection, cancellationToken);
            EnsureDatabaseIsCompatible(applied);

            int appliedCount = 0;
            foreach (MigrationDefinition migration in Migrations)
            {
                string sql = ReadEmbeddedSql(migration.ResourceFileName);
                string checksum = ComputeChecksum(sql);

                if (applied.TryGetValue(migration.Id, out string? existingChecksum))
                {
                    if (!string.Equals(existingChecksum, checksum, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Applied migration {migration.Id} has a different checksum.");
                    }

                    continue;
                }

                await ApplyMigrationAsync(connection, migration.Id, checksum, sql, cancellationToken);
                appliedCount++;
            }

            return appliedCount;
        }
        finally
        {
            await ExecuteAsync(connection, "SELECT pg_advisory_unlock(527614, 20260819)", CancellationToken.None);
        }
    }

    private static async Task EnsureHistoryTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION kagu_erp_schema_owner;
            REVOKE ALL ON SCHEMA platform FROM PUBLIC;

            CREATE TABLE IF NOT EXISTS platform.schema_migration
            (
                migration_id varchar(160) PRIMARY KEY,
                checksum_sha256 varchar(64) NOT NULL,
                applied_at timestamptz NOT NULL DEFAULT clock_timestamp(),
                CONSTRAINT ck_schema_migration_checksum_sha256
                    CHECK (checksum_sha256 ~ '^[0-9a-f]{64}$')
            );

            ALTER TABLE platform.schema_migration OWNER TO kagu_erp_schema_owner;
            REVOKE ALL ON TABLE platform.schema_migration FROM PUBLIC;
            """;

        await ExecuteAsync(connection, sql, cancellationToken);
    }

    private static async Task<Dictionary<string, string>> ReadAppliedMigrationsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT migration_id, checksum_sha256 FROM platform.schema_migration ORDER BY migration_id";
        await using var command = new NpgsqlCommand(sql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0), reader.GetString(1));
        }

        return result;
    }

    private static void EnsureDatabaseIsCompatible(IReadOnlyDictionary<string, string> applied)
    {
        var knownIds = Migrations.Select(migration => migration.Id).ToHashSet(StringComparer.Ordinal);
        string? unknownMigration = applied.Keys.FirstOrDefault(id => !knownIds.Contains(id));
        if (unknownMigration is not null)
        {
            throw new InvalidOperationException($"Database contains migration {unknownMigration}, which this binary does not know.");
        }
    }

    private static async Task ApplyMigrationAsync(
        NpgsqlConnection connection,
        string migrationId,
        string checksum,
        string sql,
        CancellationToken cancellationToken)
    {
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var migrationCommand = new NpgsqlCommand(sql, connection, transaction))
        {
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string recordSql = """
            INSERT INTO platform.schema_migration (migration_id, checksum_sha256)
            VALUES ($1, $2)
            """;
        await using (var recordCommand = new NpgsqlCommand(recordSql, connection, transaction))
        {
            recordCommand.Parameters.AddWithValue(migrationId);
            recordCommand.Parameters.AddWithValue(checksum);
            await recordCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ReadEmbeddedSql(string fileName)
    {
        Assembly assembly = typeof(MigrationRunner).Assembly;
        string resourceName = assembly.GetManifestResourceNames().Single(
            name => name.EndsWith(fileName, StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration resource {fileName} was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string ComputeChecksum(string sql)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexStringLower(hash);
    }
}
