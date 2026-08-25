namespace KaguERP.Modules.Reporting.Domain.ControlAccounts;

public sealed record ControlAccountBalanceSnapshot
{
    private ControlAccountBalanceSnapshot(
        Guid snapshotId,
        Guid controlAccountId,
        LedgerSide ledgerSide,
        decimal openingBalance,
        decimal debits,
        decimal credits,
        decimal closingBalance,
        long rowCount,
        string sourceChecksumSha256,
        FinancialReportSlice reportSlice)
    {
        SnapshotId = snapshotId;
        ControlAccountId = controlAccountId;
        LedgerSide = ledgerSide;
        OpeningBalance = openingBalance;
        Debits = debits;
        Credits = credits;
        ClosingBalance = closingBalance;
        RowCount = rowCount;
        SourceChecksumSha256 = sourceChecksumSha256;
        ReportSlice = reportSlice;
    }

    public Guid SnapshotId { get; }

    public Guid ControlAccountId { get; }

    public LedgerSide LedgerSide { get; }

    public decimal OpeningBalance { get; }

    public decimal Debits { get; }

    public decimal Credits { get; }

    public decimal ClosingBalance { get; }

    public long RowCount { get; }

    public string SourceChecksumSha256 { get; }

    public FinancialReportSlice ReportSlice { get; }

    public static ControlAccountBalanceSnapshot Create(
        Guid snapshotId,
        Guid controlAccountId,
        LedgerSide ledgerSide,
        decimal openingBalance,
        decimal debits,
        decimal credits,
        decimal closingBalance,
        long rowCount,
        string sourceChecksumSha256,
        FinancialReportSlice? reportSlice)
    {
        RequireId(snapshotId, "REPORT_BALANCE_SNAPSHOT_REQUIRED", "Balance snapshot ID is required.");
        RequireId(controlAccountId, "REPORT_CONTROL_ACCOUNT_REQUIRED", "Control-account ID is required.");
        ArgumentNullException.ThrowIfNull(reportSlice);

        if (!Enum.IsDefined(ledgerSide))
        {
            throw new ReportingInvariantException("REPORT_LEDGER_SIDE_INVALID", "Balance snapshot ledger side is invalid.");
        }

        if (debits < decimal.Zero || credits < decimal.Zero)
        {
            throw new ReportingInvariantException(
                "REPORT_BALANCE_MOVEMENT_INVALID",
                "Balance snapshot debit and credit totals cannot be negative.");
        }

        if (rowCount < 0)
        {
            throw new ReportingInvariantException("REPORT_ROW_COUNT_INVALID", "Balance snapshot row count cannot be negative.");
        }

        if (!IsLowercaseSha256(sourceChecksumSha256))
        {
            throw new ReportingInvariantException(
                "REPORT_SOURCE_CHECKSUM_INVALID",
                "Balance source checksum must be a 64-character lowercase SHA-256 value.");
        }

        decimal calculatedClosing;
        try
        {
            calculatedClosing = openingBalance + debits - credits;
        }
        catch (OverflowException exception)
        {
            throw new ReportingInvariantException(
                "REPORT_BALANCE_OVERFLOW",
                $"Balance snapshot arithmetic overflowed: {exception.Message}");
        }

        if (calculatedClosing != closingBalance)
        {
            throw new ReportingInvariantException(
                "REPORT_BALANCE_CROSS_FOOT_MISMATCH",
                "Balance snapshot must satisfy opening plus debits minus credits equals closing.");
        }

        return new ControlAccountBalanceSnapshot(
            snapshotId,
            controlAccountId,
            ledgerSide,
            openingBalance,
            debits,
            credits,
            closingBalance,
            rowCount,
            sourceChecksumSha256,
            reportSlice);
    }

    private static bool IsLowercaseSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReportingInvariantException(code, message);
        }
    }
}
