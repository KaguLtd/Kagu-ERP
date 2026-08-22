namespace KaguERP.Modules.Parties.Domain.Allocations;

public sealed record OpenItemAllocationCapacity
{
    private OpenItemAllocationCapacity(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid openItemId,
        AllocationCurrencyCode currency,
        decimal remainingAmount)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        OpenItemId = openItemId;
        Currency = currency;
        RemainingAmount = remainingAmount;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PartyAccountId { get; }
    public Guid OpenItemId { get; }
    public AllocationCurrencyCode Currency { get; }
    public decimal RemainingAmount { get; }

    public static OpenItemAllocationCapacity Create(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid openItemId,
        AllocationCurrencyCode? currency,
        decimal remainingAmount)
    {
        RequireId(tenantId, "ALLOCATION_TENANT_REQUIRED", "Tenant is required.");
        RequireId(companyId, "ALLOCATION_COMPANY_REQUIRED", "Company is required.");
        RequireId(partyAccountId, "ALLOCATION_PARTY_ACCOUNT_REQUIRED", "Party account is required.");
        RequireId(openItemId, "ALLOCATION_OPEN_ITEM_REQUIRED", "Open item is required.");
        ArgumentNullException.ThrowIfNull(currency);

        if (remainingAmount <= 0m)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_OPEN_ITEM_CAPACITY_INVALID",
                "Open-item remaining amount must be greater than zero.");
        }

        return new OpenItemAllocationCapacity(
            tenantId,
            companyId,
            partyAccountId,
            openItemId,
            currency,
            remainingAmount);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new AllocationInvariantException(code, message);
        }
    }
}
