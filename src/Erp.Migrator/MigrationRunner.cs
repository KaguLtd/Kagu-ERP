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
