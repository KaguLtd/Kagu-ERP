using System.Collections.ObjectModel;
using KaguERP.Modules.Accounting.Domain.Journals;

namespace KaguERP.Modules.Accounting.Domain.Accounts;

public sealed class ValidatedJournalAccountSet
{
    private ValidatedJournalAccountSet(
        ValidatedJournalDraft journalDraft,
        Guid chartOfAccountsVersionId,
        ReadOnlyCollection<AccountPostingSnapshot> accounts)
    {
        JournalDraft = journalDraft;
        ChartOfAccountsVersionId = chartOfAccountsVersionId;
        Accounts = accounts;
    }

    public ValidatedJournalDraft JournalDraft { get; }
    public Guid ChartOfAccountsVersionId { get; }
    public IReadOnlyList<AccountPostingSnapshot> Accounts { get; }

    public static ValidatedJournalAccountSet Create(
        ValidatedJournalDraft? journalDraft,
        Guid chartOfAccountsVersionId,
        IEnumerable<AccountPostingSnapshot?>? accountSnapshots)
    {
        ArgumentNullException.ThrowIfNull(journalDraft);

        if (chartOfAccountsVersionId == Guid.Empty)
        {
            throw new AccountInvariantException(
                "ACCOUNT_CHART_VERSION_REQUIRED",
                "Chart-of-accounts version ID is required.");
        }

        if (accountSnapshots is null)
        {
            throw new AccountInvariantException(
                "ACCOUNT_SNAPSHOTS_REQUIRED",
                "Account posting snapshots are required.");
        }

        var copiedSnapshots = accountSnapshots.ToArray();
        if (copiedSnapshots.Length == 0)
        {
            throw new AccountInvariantException(
                "ACCOUNT_SNAPSHOTS_REQUIRED",
                "Account posting snapshots are required.");
        }

        if (copiedSnapshots.Any(snapshot => snapshot is null))
        {
            throw new AccountInvariantException(
                "ACCOUNT_SNAPSHOT_REQUIRED",
                "Account posting snapshots cannot contain null values.");
        }

        var snapshots = copiedSnapshots.Cast<AccountPostingSnapshot>().ToArray();
        var snapshotsByAccountId = new Dictionary<Guid, AccountPostingSnapshot>();
        foreach (var snapshot in snapshots)
        {
            RequireSameContext(journalDraft, chartOfAccountsVersionId, snapshot);
            if (!snapshotsByAccountId.TryAdd(snapshot.AccountId, snapshot))
            {
                throw new AccountInvariantException(
                    "ACCOUNT_SNAPSHOT_DUPLICATE",
                    "An account can occur only once in a journal validation snapshot.");
            }
        }

        var usedAccounts = new List<AccountPostingSnapshot>();
        var usedAccountIds = new HashSet<Guid>();
        foreach (var line in journalDraft.Lines)
        {
            if (!snapshotsByAccountId.TryGetValue(line.AccountId, out var account))
            {
                throw new AccountInvariantException(
                    "JOURNAL_ACCOUNT_SNAPSHOT_MISSING",
                    "Every journal account requires an explicit posting snapshot.");
            }

            if (!account.IsActive)
            {
                throw new AccountInvariantException(
                    "JOURNAL_ACCOUNT_INACTIVE",
                    "A journal line cannot use an inactive account.");
            }

            if (account.Kind != AccountKind.Posting)
            {
                throw new AccountInvariantException(
                    "JOURNAL_ACCOUNT_NOT_POSTABLE",
                    "A journal line cannot use a summary or non-posting account.");
            }

            if (usedAccountIds.Add(account.AccountId))
            {
                usedAccounts.Add(account);
            }
        }

        return new ValidatedJournalAccountSet(
            journalDraft,
            chartOfAccountsVersionId,
            Array.AsReadOnly(usedAccounts.ToArray()));
    }

    private static void RequireSameContext(
        ValidatedJournalDraft journalDraft,
        Guid chartOfAccountsVersionId,
        AccountPostingSnapshot snapshot)
    {
        if (snapshot.TenantId != journalDraft.TenantId)
        {
            throw new AccountInvariantException(
                "JOURNAL_ACCOUNT_TENANT_MISMATCH",
                "Journal and account snapshot must belong to the same tenant.");
        }

        if (snapshot.CompanyId != journalDraft.CompanyId)
        {
            throw new AccountInvariantException(
                "JOURNAL_ACCOUNT_COMPANY_MISMATCH",
                "Journal and account snapshot must belong to the same company.");
        }

        if (snapshot.ChartOfAccountsVersionId != chartOfAccountsVersionId)
        {
            throw new AccountInvariantException(
                "JOURNAL_ACCOUNT_CHART_VERSION_MISMATCH",
                "Every account snapshot must belong to the selected chart version.");
        }
    }
}
