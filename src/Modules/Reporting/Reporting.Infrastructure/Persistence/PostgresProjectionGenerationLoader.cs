using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record LoadedProjectionGeneration(
    FinancialReportSlice Slice,
    string GenerationReason,
    string SourceWatermarkFrom,
    string SourceWatermarkTo,
    string SourceChecksumSha256,
    Guid GeneratedBy);

public static class PostgresProjectionGenerationLoader
{
    public static async ValueTask<LoadedProjectionGeneration?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid projectionGenerationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (projectionGenerationId == Guid.Empty)
        {
            throw new ArgumentException("Projection generation ID is required.", nameof(projectionGenerationId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string headerSql = """
            SELECT report_code,report_definition_version,effective_as_of,data_cutoff_at,generated_at,
                   currency,generation_reason,source_watermark_from,source_watermark_to,
                   source_checksum_sha256,generated_by
            FROM reporting.projection_generation
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3
            """;
        string reportCode;
        long definitionVersion;
        DateOnly effectiveAsOf;
        DateTimeOffset dataCutoffAt;
        DateTimeOffset generatedAt;
        string currency;
        string reason;
        string watermarkFrom;
        string watermarkTo;
        string checksum;
        Guid generatedBy;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(scope.TenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(projectionGenerationId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            reportCode = reader.GetString(0);
            definitionVersion = reader.GetInt64(1);
            effectiveAsOf = reader.GetFieldValue<DateOnly>(2);
            dataCutoffAt = reader.GetFieldValue<DateTimeOffset>(3);
            generatedAt = reader.GetFieldValue<DateTimeOffset>(4);
            currency = reader.GetString(5);
            reason = reader.GetString(6);
            watermarkFrom = reader.GetString(7);
            watermarkTo = reader.GetString(8);
            checksum = reader.GetString(9);
            generatedBy = reader.GetGuid(10);
        }

        const string dimensionSql = """
            SELECT dimension_code,value_code
            FROM reporting.projection_generation_dimension
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3
            ORDER BY dimension_code
            """;
        var assignments = new List<ReportDimensionAssignment>();
        await using (var dimensions = new NpgsqlCommand(dimensionSql, connection, transaction))
        {
            dimensions.Parameters.AddWithValue(scope.TenantId);
            dimensions.Parameters.AddWithValue(companyId);
            dimensions.Parameters.AddWithValue(projectionGenerationId);
            await using NpgsqlDataReader reader = await dimensions.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                assignments.Add(ReportDimensionAssignment.Create(reader.GetString(0), reader.GetString(1)));
            }
        }

        FinancialReportSlice slice = FinancialReportSlice.Create(
            scope.TenantId, companyId, reportCode, definitionVersion, effectiveAsOf, dataCutoffAt,
            generatedAt, projectionGenerationId, ReportCurrencyCode.Create(currency),
            ReportDimensionSlice.Create(assignments));
        return new LoadedProjectionGeneration(
            slice, reason, watermarkFrom, watermarkTo, checksum, generatedBy);
    }
}
