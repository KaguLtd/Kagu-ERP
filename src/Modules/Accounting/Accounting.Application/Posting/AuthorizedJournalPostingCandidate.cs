using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Accounts;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Domain.Dimensions;
using KaguERP.Modules.Accounting.Domain.Journals;
using KaguERP.Modules.Accounting.Domain.Periods;

namespace KaguERP.Modules.Accounting.Application.Posting;

public sealed class AuthorizedJournalPostingCandidate
{
    public const string RequiredPermission = "accounting.journal.post";

    private AuthorizedJournalPostingCandidate(
        Guid actorId,
        ValidatedJournalDraft journalDraft,
        ValidatedJournalAccountSet accounts,
        ValidatedJournalDimensions dimensions,
        ValidatedJournalCurrencySet currencies,
        ValidatedPeriodLockSet periodLocks)
    {
        ActorId = actorId;
        JournalDraft = journalDraft;
        Accounts = accounts;
        Dimensions = dimensions;
        Currencies = currencies;
        PeriodLocks = periodLocks;
    }

    public Guid ActorId { get; }
    public ValidatedJournalDraft JournalDraft { get; }
    public ValidatedJournalAccountSet Accounts { get; }
    public ValidatedJournalDimensions Dimensions { get; }
    public ValidatedJournalCurrencySet Currencies { get; }
    public ValidatedPeriodLockSet PeriodLocks { get; }

    public static AuthorizedJournalPostingCandidate Create(
        ExecutionScope scope,
        ValidatedJournalDraft journalDraft,
        ValidatedJournalAccountSet accounts,
        ValidatedJournalDimensions dimensions,
        ValidatedJournalCurrencySet currencies,
        ValidatedPeriodLockSet periodLocks)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(journalDraft);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(dimensions);
        ArgumentNullException.ThrowIfNull(currencies);
        ArgumentNullException.ThrowIfNull(periodLocks);

        scope.EnsureAllowed(journalDraft.TenantId, journalDraft.CompanyId);
        if (!scope.HasPermission(journalDraft.CompanyId, RequiredPermission))
        {
            throw new JournalPostingCandidateException(
                "JOURNAL_POST_PERMISSION_REQUIRED",
                "The active actor does not have permission to post journals for this company.");
        }

        EnsureSameDraft(journalDraft, accounts.JournalDraft, "accounts");
        EnsureSameDraft(journalDraft, dimensions.JournalDraft, "dimensions");
        EnsureSameDraft(journalDraft, currencies.JournalDraft, "currencies");

        if (periodLocks.TenantId != journalDraft.TenantId || periodLocks.CompanyId != journalDraft.CompanyId)
        {
            throw new JournalPostingCandidateException(
                "JOURNAL_PERIOD_SCOPE_MISMATCH",
                "Journal and period validation must belong to the same tenant and company.");
        }

        periodLocks.EnsureStandardPostingAllowed();
        return new AuthorizedJournalPostingCandidate(
            scope.ActorId,
            journalDraft,
            accounts,
            dimensions,
            currencies,
            periodLocks);
    }

    private static void EnsureSameDraft(
        ValidatedJournalDraft expected,
        ValidatedJournalDraft actual,
        string validationName)
    {
        if (!ReferenceEquals(expected, actual))
        {
            throw new JournalPostingCandidateException(
                "JOURNAL_VALIDATION_DRAFT_MISMATCH",
                $"The {validationName} validation does not belong to the supplied journal draft.");
        }
    }
}

public sealed class JournalPostingCandidateException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
