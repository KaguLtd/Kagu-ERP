using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record ProjectionGenerationPersistenceCommand(
    ExecutionScope Scope,
    FinancialReportSlice Slice,
    string GenerationReason,
    string SourceWatermarkFrom,
    string SourceWatermarkTo,
    string SourceChecksumSha256);

public sealed record ProjectionGenerationPersistenceResult(Guid ProjectionGenerationId, bool Created);

public static class PostgresProjectionGenerationWriter
{
    public static async ValueTask<ProjectionGenerationPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProjectionGenerationPersistenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(command);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        FinancialReportSlice slice = command.Slice;
        command.Scope.EnsureAllowed(slice.TenantId, slice.CompanyId);
        string reason = RequireText(command.GenerationReason, 160, nameof(command.GenerationReason));
        string watermarkFrom = RequireText(command.SourceWatermarkFrom, 200, nameof(command.SourceWatermarkFrom));
        string watermarkTo = RequireText(command.SourceWatermarkTo, 200, nameof(command.SourceWatermarkTo));
        if (command.SourceChecksumSha256.Length != 64 ||
            command.SourceChecksumSha256.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("Source checksum must be lowercase SHA-256.", nameof(command));
        }

        const string sql = """
            INSERT INTO reporting.projection_generation
                (tenant_id,company_id,projection_generation_id,report_code,report_definition_version,
                 effective_as_of,data_cutoff_at,generated_at,currency,generation_reason,
                 source_watermark_from,source_watermark_to,source_checksum_sha256,dimension_count,generated_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
            ON CONFLICT (tenant_id,company_id,projection_generation_id) DO NOTHING
            RETURNING projection_generation_id
            """;
        await using (var header = new NpgsqlCommand(sql, connection, transaction))
        {
            object[] values =
            [
                slice.TenantId, slice.CompanyId, slice.ProjectionGenerationId, slice.ReportCode,
                slice.ReportDefinitionVersion, slice.EffectiveAsOf, slice.DataCutoffAt, slice.GeneratedAt,
                slice.Currency.Value, reason, watermarkFrom, watermarkTo, command.SourceChecksumSha256,
                slice.Dimensions.Assignments.Count, command.Scope.ActorId,
            ];
            foreach (object value in values) header.Parameters.AddWithValue(value);
            if (await header.ExecuteScalarAsync(cancellationToken) is Guid insertedId)
            {
                await InsertDimensionsAsync(connection, transaction, slice, cancellationToken);
                return new ProjectionGenerationPersistenceResult(insertedId, true);
            }
        }
        await ValidateExistingAsync(
            connection, transaction, command, reason, watermarkFrom, watermarkTo, cancellationToken);
        return new ProjectionGenerationPersistenceResult(slice.ProjectionGenerationId, false);
    }

    private static async ValueTask InsertDimensionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        FinancialReportSlice slice,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO reporting.projection_generation_dimension
                (tenant_id,company_id,projection_generation_id,dimension_code,value_code)
            VALUES ($1,$2,$3,$4,$5)
            """;
        foreach (ReportDimensionAssignment assignment in slice.Dimensions.Assignments)
        {
            await using var dbCommand = new NpgsqlCommand(sql, connection, transaction);
            dbCommand.Parameters.AddWithValue(slice.TenantId);
            dbCommand.Parameters.AddWithValue(slice.CompanyId);
            dbCommand.Parameters.AddWithValue(slice.ProjectionGenerationId);
            dbCommand.Parameters.AddWithValue(assignment.DimensionCode);
            dbCommand.Parameters.AddWithValue(assignment.ValueCode);
            await dbCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async ValueTask ValidateExistingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProjectionGenerationPersistenceCommand command,
        string reason,
        string watermarkFrom,
        string watermarkTo,
        CancellationToken cancellationToken)
    {
        FinancialReportSlice slice = command.Slice;
        const string sql = """
            SELECT report_code,report_definition_version,effective_as_of,data_cutoff_at,generated_at,
                   currency,generation_reason,source_watermark_from,source_watermark_to,
                   source_checksum_sha256,dimension_count
            FROM reporting.projection_generation
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3
            """;
        await using (var existing = new NpgsqlCommand(sql, connection, transaction))
        {
            existing.Parameters.AddWithValue(slice.TenantId);
            existing.Parameters.AddWithValue(slice.CompanyId);
            existing.Parameters.AddWithValue(slice.ProjectionGenerationId);
            await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetString(0) != slice.ReportCode ||
                reader.GetInt64(1) != slice.ReportDefinitionVersion ||
                reader.GetFieldValue<DateOnly>(2) != slice.EffectiveAsOf ||
                reader.GetFieldValue<DateTimeOffset>(3) != slice.DataCutoffAt ||
                reader.GetFieldValue<DateTimeOffset>(4) != slice.GeneratedAt ||
                reader.GetString(5) != slice.Currency.Value || reader.GetString(6) != reason ||
                reader.GetString(7) != watermarkFrom || reader.GetString(8) != watermarkTo ||
                reader.GetString(9) != command.SourceChecksumSha256 ||
                reader.GetInt32(10) != slice.Dimensions.Assignments.Count)
            {
                throw new ProjectionGenerationPersistenceConflictException(slice.ProjectionGenerationId);
            }
        }

        const string dimensionSql = """
            SELECT dimension_code,value_code FROM reporting.projection_generation_dimension
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3 ORDER BY dimension_code
            """;
        await using var dimensions = new NpgsqlCommand(dimensionSql, connection, transaction);
        dimensions.Parameters.AddWithValue(slice.TenantId);
        dimensions.Parameters.AddWithValue(slice.CompanyId);
        dimensions.Parameters.AddWithValue(slice.ProjectionGenerationId);
        await using NpgsqlDataReader dimensionReader = await dimensions.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await dimensionReader.ReadAsync(cancellationToken))
        {
            if (index >= slice.Dimensions.Assignments.Count ||
                dimensionReader.GetString(0) != slice.Dimensions.Assignments[index].DimensionCode ||
                dimensionReader.GetString(1) != slice.Dimensions.Assignments[index].ValueCode)
            {
                throw new ProjectionGenerationPersistenceConflictException(slice.ProjectionGenerationId);
            }
            index++;
        }
        if (index != slice.Dimensions.Assignments.Count)
            throw new ProjectionGenerationPersistenceConflictException(slice.ProjectionGenerationId);
    }

    private static string RequireText(string value, int maximumLength, string parameterName)
    {
        string canonical = value.Trim();
        if (canonical.Length == 0 || canonical.Length > maximumLength)
            throw new ArgumentException("Projection lineage text is required and exceeds no storage limit.", parameterName);
        return canonical;
    }
}

public sealed class ProjectionGenerationPersistenceConflictException(Guid projectionGenerationId)
    : InvalidOperationException("The projection generation ID already has different immutable lineage.")
{
    public string Code { get; } = "PROJECTION_GENERATION_CONFLICT";
    public Guid ProjectionGenerationId { get; } = projectionGenerationId;
}
