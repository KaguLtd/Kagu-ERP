using System.Collections.ObjectModel;

namespace KaguERP.Modules.Accounting.Domain.Journals;

public sealed class ValidatedJournalDraftSet
{
    private ValidatedJournalDraftSet(ReadOnlyCollection<ValidatedJournalDraft> drafts)
    {
        Drafts = drafts;
    }

    public IReadOnlyList<ValidatedJournalDraft> Drafts { get; }

    public static ValidatedJournalDraftSet Create(IEnumerable<ValidatedJournalDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        var draftArray = drafts.ToArray();
        if (draftArray.Length == 0)
        {
            throw new JournalInvariantException(
                "JOURNAL_DRAFT_SET_EMPTY",
                "A validated journal draft set requires at least one draft.");
        }

        if (draftArray.Any(draft => draft is null))
        {
            throw new JournalInvariantException(
                "JOURNAL_DRAFT_REQUIRED",
                "A validated journal draft set cannot contain null values.");
        }

        var identities = new HashSet<JournalPostingIdentity>();
        foreach (var draft in draftArray)
        {
            if (!identities.Add(draft.PostingIdentity))
            {
                throw new JournalInvariantException(
                    "JOURNAL_SOURCE_DUPLICATE",
                    "A source event can produce at most one journal intent for the same company and posting purpose.");
            }
        }

        return new ValidatedJournalDraftSet(Array.AsReadOnly(draftArray));
    }
}
