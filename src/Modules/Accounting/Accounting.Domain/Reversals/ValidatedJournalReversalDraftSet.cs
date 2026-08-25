using System.Collections.ObjectModel;

namespace KaguERP.Modules.Accounting.Domain.Reversals;

public sealed class ValidatedJournalReversalDraftSet
{
    private ValidatedJournalReversalDraftSet(ReadOnlyCollection<JournalReversalDraft> reversals)
    {
        Reversals = reversals;
    }

    public IReadOnlyList<JournalReversalDraft> Reversals { get; }

    public static ValidatedJournalReversalDraftSet Create(IEnumerable<JournalReversalDraft> reversals)
    {
        ArgumentNullException.ThrowIfNull(reversals);

        var reversalArray = reversals.ToArray();
        if (reversalArray.Length == 0)
        {
            throw new ReversalInvariantException(
                "REVERSAL_DRAFT_SET_EMPTY",
                "A reversal draft set requires at least one reversal.");
        }

        if (reversalArray.Any(reversal => reversal is null))
        {
            throw new ReversalInvariantException(
                "REVERSAL_DRAFT_REQUIRED",
                "A reversal draft set cannot contain null values.");
        }

        var identities = new HashSet<(Guid TenantId, Guid CompanyId, Guid OriginalJournalId)>();
        foreach (var reversal in reversalArray)
        {
            var identity = (
                reversal.OriginalJournalDraft.TenantId,
                reversal.OriginalJournalDraft.CompanyId,
                reversal.OriginalJournalId);
            if (!identities.Add(identity))
            {
                throw new ReversalInvariantException(
                    "REVERSAL_ORIGINAL_DUPLICATE",
                    "An original journal can have at most one reversal intent in the same tenant and company.");
            }
        }

        Array.Sort(
            reversalArray,
            static (left, right) => CompareIdentity(left, right));
        return new ValidatedJournalReversalDraftSet(Array.AsReadOnly(reversalArray));
    }

    private static int CompareIdentity(JournalReversalDraft left, JournalReversalDraft right)
    {
        var tenantComparison = left.OriginalJournalDraft.TenantId.CompareTo(right.OriginalJournalDraft.TenantId);
        if (tenantComparison != 0)
        {
            return tenantComparison;
        }

        var companyComparison = left.OriginalJournalDraft.CompanyId.CompareTo(right.OriginalJournalDraft.CompanyId);
        return companyComparison != 0
            ? companyComparison
            : left.OriginalJournalId.CompareTo(right.OriginalJournalId);
    }
}
