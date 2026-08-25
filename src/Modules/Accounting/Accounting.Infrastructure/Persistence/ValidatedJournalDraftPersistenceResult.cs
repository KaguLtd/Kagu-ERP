namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public sealed record ValidatedJournalDraftPersistenceResult(Guid JournalDraftId, bool Created, string DraftHash);

public sealed class ValidatedJournalDraftPersistenceConflictException : Exception
{
    public ValidatedJournalDraftPersistenceConflictException(Guid existingJournalDraftId)
        : base("The journal source reservation already has different validated draft content.")
    {
        ExistingJournalDraftId = existingJournalDraftId;
    }

    public Guid ExistingJournalDraftId { get; }
}
