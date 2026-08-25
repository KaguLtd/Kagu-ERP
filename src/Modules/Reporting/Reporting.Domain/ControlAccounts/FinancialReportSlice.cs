namespace KaguERP.Modules.Reporting.Domain.ControlAccounts;

public sealed record FinancialReportSlice
{
    private FinancialReportSlice(
        Guid tenantId,
        Guid companyId,
        string reportCode,
        long reportDefinitionVersion,
        DateOnly effectiveAsOf,
        DateTimeOffset dataCutoffAt,
        DateTimeOffset generatedAt,
        Guid projectionGenerationId,
        ReportCurrencyCode currency,
        ReportDimensionSlice dimensions)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        ReportCode = reportCode;
        ReportDefinitionVersion = reportDefinitionVersion;
        EffectiveAsOf = effectiveAsOf;
        DataCutoffAt = dataCutoffAt;
        GeneratedAt = generatedAt;
        ProjectionGenerationId = projectionGenerationId;
        Currency = currency;
        Dimensions = dimensions;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public string ReportCode { get; }

    public long ReportDefinitionVersion { get; }

    public DateOnly EffectiveAsOf { get; }

    public DateTimeOffset DataCutoffAt { get; }

    public DateTimeOffset GeneratedAt { get; }

    public Guid ProjectionGenerationId { get; }

    public ReportCurrencyCode Currency { get; }

    public ReportDimensionSlice Dimensions { get; }

    public static FinancialReportSlice Create(
        Guid tenantId,
        Guid companyId,
        string reportCode,
        long reportDefinitionVersion,
        DateOnly effectiveAsOf,
        DateTimeOffset dataCutoffAt,
        DateTimeOffset generatedAt,
        Guid projectionGenerationId,
        ReportCurrencyCode? currency,
        ReportDimensionSlice? dimensions)
    {
        RequireId(tenantId, "REPORT_TENANT_REQUIRED", "Report tenant ID is required.");
        RequireId(companyId, "REPORT_COMPANY_REQUIRED", "Report company ID is required.");
        RequireId(
            projectionGenerationId,
            "REPORT_PROJECTION_GENERATION_REQUIRED",
            "Report projection generation ID is required.");
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(dimensions);

        if (string.IsNullOrWhiteSpace(reportCode))
        {
            throw new ReportingInvariantException("REPORT_CODE_REQUIRED", "Report code is required.");
        }

        if (reportDefinitionVersion <= 0)
        {
            throw new ReportingInvariantException(
                "REPORT_DEFINITION_VERSION_INVALID",
                "Report definition version must be positive.");
        }

        if (effectiveAsOf == default)
        {
            throw new ReportingInvariantException("REPORT_AS_OF_REQUIRED", "Report effective as-of date is required.");
        }

        if (dataCutoffAt.Offset != TimeSpan.Zero || generatedAt.Offset != TimeSpan.Zero)
        {
            throw new ReportingInvariantException(
                "REPORT_TIMESTAMP_NOT_UTC",
                "Report data-cutoff and generated timestamps must use the UTC offset.");
        }

        if (generatedAt < dataCutoffAt)
        {
            throw new ReportingInvariantException(
                "REPORT_GENERATED_BEFORE_CUTOFF",
                "Report generation timestamp cannot precede its data cutoff.");
        }

        return new FinancialReportSlice(
            tenantId,
            companyId,
            reportCode.Trim(),
            reportDefinitionVersion,
            effectiveAsOf,
            dataCutoffAt,
            generatedAt,
            projectionGenerationId,
            currency,
            dimensions);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReportingInvariantException(code, message);
        }
    }
}
