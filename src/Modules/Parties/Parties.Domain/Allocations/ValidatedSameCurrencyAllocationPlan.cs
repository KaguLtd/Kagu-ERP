using System.Collections.ObjectModel;

namespace KaguERP.Modules.Parties.Domain.Allocations;

public sealed class ValidatedSameCurrencyAllocationPlan
{
    private ValidatedSameCurrencyAllocationPlan(
        PaymentAllocationCapacity payment,
        ReadOnlyCollection<AllocationPlanLine> lines,
        decimal totalAllocated)
    {
        Payment = payment;
        Lines = lines;
        TotalAllocated = totalAllocated;
    }

    public PaymentAllocationCapacity Payment { get; }
    public IReadOnlyList<AllocationPlanLine> Lines { get; }
    public decimal TotalAllocated { get; }
    public decimal PaymentRemainingAfter => Payment.UsableAmount - TotalAllocated;

    public static ValidatedSameCurrencyAllocationPlan Create(
        PaymentAllocationCapacity? payment,
        IEnumerable<AllocationPlanLine?>? lines)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (lines is null)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_LINES_REQUIRED",
                "At least one allocation line is required.");
        }

        var copiedLines = lines.ToArray();
        if (copiedLines.Length == 0)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_LINES_REQUIRED",
                "At least one allocation line is required.");
        }

        if (copiedLines.Any(line => line is null))
        {
            throw new AllocationInvariantException(
                "ALLOCATION_LINE_REQUIRED",
                "Allocation lines cannot contain null values.");
        }

        var validatedLines = copiedLines.Cast<AllocationPlanLine>().ToArray();
        var openItemIds = new HashSet<Guid>();
        var totalAllocated = 0m;

        foreach (var line in validatedLines)
        {
            RequireSameScope(payment, line.OpenItem);

            if (payment.Currency != line.OpenItem.Currency)
            {
                throw new AllocationInvariantException(
                    "ALLOCATION_CROSS_CURRENCY_REQUIRES_RATE_SNAPSHOT",
                    "Cross-currency allocation requires an approved rate and rounding snapshot.");
            }

            if (!openItemIds.Add(line.OpenItem.OpenItemId))
            {
                throw new AllocationInvariantException(
                    "ALLOCATION_OPEN_ITEM_DUPLICATE",
                    "An open item can occur only once in an allocation plan.");
            }

            try
            {
                totalAllocated = checked(totalAllocated + line.Amount);
            }
            catch (OverflowException exception)
            {
                throw new AllocationInvariantException(
                    "ALLOCATION_PAYMENT_EXCEEDED",
                    "Total allocation cannot exceed the payment usable amount.",
                    exception);
            }
        }

        if (totalAllocated > payment.UsableAmount)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_PAYMENT_EXCEEDED",
                "Total allocation cannot exceed the payment usable amount.");
        }

        return new ValidatedSameCurrencyAllocationPlan(
            payment,
            Array.AsReadOnly(validatedLines),
            totalAllocated);
    }

    private static void RequireSameScope(
        PaymentAllocationCapacity payment,
        OpenItemAllocationCapacity openItem)
    {
        if (payment.TenantId != openItem.TenantId)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_TENANT_MISMATCH",
                "Payment and open item must belong to the same tenant.");
        }

        if (payment.CompanyId != openItem.CompanyId)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_COMPANY_MISMATCH",
                "Payment and open item must belong to the same company.");
        }

        if (payment.PartyAccountId != openItem.PartyAccountId)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_PARTY_ACCOUNT_MISMATCH",
                "Payment and open item must belong to the same party account.");
        }
    }
}
