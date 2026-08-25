namespace KaguERP.Modules.Treasury.Domain.Payments;

public sealed record PaymentSourceIdentity
{
    private PaymentSourceIdentity(
        Guid tenantId,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        string postingPurpose)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        SourceType = sourceType;
        SourceEventId = sourceEventId;
        PostingPurpose = postingPurpose;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public string SourceType { get; }

    public Guid SourceEventId { get; }

    public string PostingPurpose { get; }

    public static PaymentSourceIdentity Create(
        Guid tenantId,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        string postingPurpose)
    {
        RequireId(tenantId, "PAYMENT_TENANT_REQUIRED", "Payment tenant ID is required.");
        RequireId(companyId, "PAYMENT_COMPANY_REQUIRED", "Payment company ID is required.");
        RequireId(sourceEventId, "PAYMENT_SOURCE_REQUIRED", "Payment source-event ID is required.");

        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new PaymentInvariantException("PAYMENT_SOURCE_TYPE_REQUIRED", "Payment source type is required.");
        }

        if (string.IsNullOrWhiteSpace(postingPurpose))
        {
            throw new PaymentInvariantException("PAYMENT_PURPOSE_REQUIRED", "Payment posting purpose is required.");
        }

        return new PaymentSourceIdentity(
            tenantId,
            companyId,
            sourceType.Trim(),
            sourceEventId,
            postingPurpose.Trim());
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PaymentInvariantException(code, message);
        }
    }
}
