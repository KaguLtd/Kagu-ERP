using KaguERP.Modules.Treasury.Domain.Payments;

namespace KaguERP.Modules.Treasury.Domain.Statements;

public sealed record ValidatedStatementLineDraft
{
    private ValidatedStatementLineDraft(
        Guid statementLineId,
        Guid statementImportId,
        StatementLineExternalIdentity externalIdentity,
        TreasuryCurrencyCode currency,
        decimal signedAmount,
        DateOnly bookingDate,
        DateOnly valueDate,
        DateTimeOffset recordedAt,
        string rawObjectSha256,
        long parserVersion)
    {
        StatementLineId = statementLineId;
        StatementImportId = statementImportId;
        ExternalIdentity = externalIdentity;
        Currency = currency;
        SignedAmount = signedAmount;
        BookingDate = bookingDate;
        ValueDate = valueDate;
        RecordedAt = recordedAt;
        RawObjectSha256 = rawObjectSha256;
        ParserVersion = parserVersion;
    }

    public Guid StatementLineId { get; }

    public Guid StatementImportId { get; }

    public Guid TenantId => ExternalIdentity.TenantId;

    public Guid CompanyId => ExternalIdentity.CompanyId;

    public Guid TreasuryAccountId => ExternalIdentity.TreasuryAccountId;

    public StatementLineExternalIdentity ExternalIdentity { get; }

    public TreasuryCurrencyCode Currency { get; }

    public decimal SignedAmount { get; }

    public decimal MatchCapacity => SignedAmount < decimal.Zero ? -SignedAmount : SignedAmount;

    public DateOnly BookingDate { get; }

    public DateOnly ValueDate { get; }

    public DateTimeOffset RecordedAt { get; }

    public string RawObjectSha256 { get; }

    public long ParserVersion { get; }

    public static ValidatedStatementLineDraft Create(
        Guid statementLineId,
        Guid statementImportId,
        StatementLineExternalIdentity? externalIdentity,
        TreasuryCurrencyCode? currency,
        decimal signedAmount,
        DateOnly bookingDate,
        DateOnly valueDate,
        DateTimeOffset recordedAt,
        string rawObjectSha256,
        long parserVersion)
    {
        RequireId(statementLineId, "STATEMENT_LINE_REQUIRED", "Statement-line ID is required.");
        RequireId(statementImportId, "STATEMENT_IMPORT_REQUIRED", "Statement import ID is required.");
        ArgumentNullException.ThrowIfNull(externalIdentity);
        ArgumentNullException.ThrowIfNull(currency);

        if (signedAmount == decimal.Zero || signedAmount == decimal.MinValue)
        {
            throw new StatementInvariantException(
                "STATEMENT_AMOUNT_INVALID",
                "Statement-line signed amount must be non-zero and safely representable as an absolute amount.");
        }

        if (bookingDate == default)
        {
            throw new StatementInvariantException("STATEMENT_BOOKING_DATE_REQUIRED", "Statement booking date is required.");
        }

        if (valueDate == default)
        {
            throw new StatementInvariantException("STATEMENT_VALUE_DATE_REQUIRED", "Statement value date is required.");
        }

        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new StatementInvariantException(
                "STATEMENT_RECORDED_AT_NOT_UTC",
                "Statement-line recorded timestamp must use the UTC offset.");
        }

        if (!IsLowercaseSha256(rawObjectSha256))
        {
            throw new StatementInvariantException(
                "STATEMENT_RAW_HASH_INVALID",
                "Statement raw-object hash must be a 64-character lowercase SHA-256 value.");
        }

        if (parserVersion <= 0)
        {
            throw new StatementInvariantException(
                "STATEMENT_PARSER_VERSION_INVALID",
                "Statement parser version must be positive.");
        }

        return new ValidatedStatementLineDraft(
            statementLineId,
            statementImportId,
            externalIdentity,
            currency,
            signedAmount,
            bookingDate,
            valueDate,
            recordedAt,
            rawObjectSha256,
            parserVersion);
    }

    private static bool IsLowercaseSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new StatementInvariantException(code, message);
        }
    }
}
