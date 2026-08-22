namespace KaguERP.Modules.Parties.Domain.Allocations;

public sealed record PaymentAllocationCapacity
{
    private PaymentAllocationCapacity(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid paymentId,
        AllocationCurrencyCode currency,
        decimal usableAmount)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        PaymentId = paymentId;
        Currency = currency;
        UsableAmount = usableAmount;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PartyAccountId { get; }
    public Guid PaymentId { get; }
    public AllocationCurrencyCode Currency { get; }
    public decimal UsableAmount { get; }

    public static PaymentAllocationCapacity Create(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid paymentId,
        AllocationCurrencyCode? currency,
        decimal usableAmount)
    {
        RequireId(tenantId, "ALLOCATION_TENANT_REQUIRED", "Tenant is required.");
        RequireId(companyId, "ALLOCATION_COMPANY_REQUIRED", "Company is required.");
        RequireId(partyAccountId, "ALLOCATION_PARTY_ACCOUNT_REQUIRED", "Party account is required.");
        RequireId(paymentId, "ALLOCATION_PAYMENT_REQUIRED", "Payment is required.");
        ArgumentNullException.ThrowIfNull(currency);

        if (usableAmount <= 0m)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_PAYMENT_CAPACITY_INVALID",
                "Payment usable amount must be greater than zero.");
        }

        return new PaymentAllocationCapacity(
            tenantId,
            companyId,
            partyAccountId,
            paymentId,
            currency,
            usableAmount);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new AllocationInvariantException(code, message);
        }
    }
}
