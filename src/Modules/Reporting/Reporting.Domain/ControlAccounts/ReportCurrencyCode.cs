namespace KaguERP.Modules.Reporting.Domain.ControlAccounts;

public sealed record ReportCurrencyCode
{
    private ReportCurrencyCode(string value) => Value = value;

    public string Value { get; }

    public static ReportCurrencyCode Create(string? value)
    {
        if (value is null || value.Length != 3 || value.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ReportingInvariantException(
                "REPORT_CURRENCY_INVALID",
                "Report currency must contain exactly three uppercase ASCII letters.");
        }

        return new ReportCurrencyCode(value);
    }

    public override string ToString() => Value;
}
