using System.Collections.ObjectModel;

namespace KaguERP.Modules.Treasury.Domain.Payments;

public sealed class ValidatedPaymentEconomicEventDraftSet
{
    private ValidatedPaymentEconomicEventDraftSet(
        ReadOnlyCollection<ValidatedPaymentEconomicEventDraft> drafts)
    {
        Drafts = drafts;
    }

    public IReadOnlyList<ValidatedPaymentEconomicEventDraft> Drafts { get; }

    public static ValidatedPaymentEconomicEventDraftSet Create(
        IEnumerable<ValidatedPaymentEconomicEventDraft?>? drafts)
    {
        if (drafts is null)
        {
            throw new PaymentInvariantException("PAYMENT_DRAFTS_REQUIRED", "Payment draft collection is required.");
        }

        var copiedDrafts = drafts.ToArray();
        if (copiedDrafts.Length == 0)
        {
            throw new PaymentInvariantException("PAYMENT_DRAFTS_REQUIRED", "Payment draft collection is required.");
        }

        if (copiedDrafts.Any(draft => draft is null))
        {
            throw new PaymentInvariantException("PAYMENT_DRAFT_REQUIRED", "Payment drafts cannot contain null values.");
        }

        var validatedDrafts = copiedDrafts.Cast<ValidatedPaymentEconomicEventDraft>().ToArray();
        var paymentIds = new HashSet<(Guid TenantId, Guid PaymentId)>();
        var sourceIdentities = new HashSet<PaymentSourceIdentity>();
        foreach (var draft in validatedDrafts)
        {
            if (!paymentIds.Add((draft.TenantId, draft.PaymentId)))
            {
                throw new PaymentInvariantException(
                    "PAYMENT_ID_DUPLICATE",
                    "A payment ID can occur only once in a tenant.");
            }

            if (!sourceIdentities.Add(draft.SourceIdentity))
            {
                throw new PaymentInvariantException(
                    "PAYMENT_SOURCE_DUPLICATE",
                    "A canonical source identity can produce at most one payment economic-event intent.");
            }
        }

        Array.Sort(validatedDrafts, CompareDrafts);
        return new ValidatedPaymentEconomicEventDraftSet(Array.AsReadOnly(validatedDrafts));
    }

    private static int CompareDrafts(
        ValidatedPaymentEconomicEventDraft left,
        ValidatedPaymentEconomicEventDraft right)
    {
        var tenantComparison = left.TenantId.CompareTo(right.TenantId);
        if (tenantComparison != 0)
        {
            return tenantComparison;
        }

        var companyComparison = left.CompanyId.CompareTo(right.CompanyId);
        return companyComparison != 0
            ? companyComparison
            : left.PaymentId.CompareTo(right.PaymentId);
    }
}
