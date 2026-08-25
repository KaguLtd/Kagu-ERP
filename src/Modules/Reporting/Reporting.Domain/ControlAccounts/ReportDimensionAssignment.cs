namespace KaguERP.Modules.Reporting.Domain.ControlAccounts;

public sealed record ReportDimensionAssignment
{
    private ReportDimensionAssignment(string dimensionCode, string valueCode)
    {
        DimensionCode = dimensionCode;
        ValueCode = valueCode;
    }

    public string DimensionCode { get; }

    public string ValueCode { get; }

    public static ReportDimensionAssignment Create(string dimensionCode, string valueCode) =>
        new(
            RequireCode(dimensionCode, "REPORT_DIMENSION_CODE_REQUIRED", "Report dimension code is required."),
            RequireCode(valueCode, "REPORT_DIMENSION_VALUE_REQUIRED", "Report dimension value is required."));

    private static string RequireCode(string value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReportingInvariantException(code, message);
        }

        return value.Trim();
    }
}
