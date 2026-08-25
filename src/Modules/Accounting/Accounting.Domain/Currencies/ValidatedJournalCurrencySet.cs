using System.Collections.ObjectModel;
using KaguERP.Modules.Accounting.Domain.Journals;

namespace KaguERP.Modules.Accounting.Domain.Currencies;

public sealed class ValidatedJournalCurrencySet
{
    private ValidatedJournalCurrencySet(
        ValidatedJournalDraft journalDraft,
        ReadOnlyCollection<JournalCurrencyAmountSnapshot> lineAmounts)
    {
        JournalDraft = journalDraft;
        LineAmounts = lineAmounts;
    }

    public ValidatedJournalDraft JournalDraft { get; }

    public IReadOnlyList<JournalCurrencyAmountSnapshot> LineAmounts { get; }

    public static ValidatedJournalCurrencySet Create(ValidatedJournalDraft journalDraft)
    {
        ArgumentNullException.ThrowIfNull(journalDraft);

        var lineAmounts = new JournalCurrencyAmountSnapshot[journalDraft.Lines.Count];
        for (var index = 0; index < journalDraft.Lines.Count; index++)
        {
            var currencyAmount = journalDraft.Lines[index].CurrencyAmount;
            if (currencyAmount is null)
            {
                throw new CurrencyInvariantException(
                    "JOURNAL_CURRENCY_SNAPSHOT_REQUIRED",
                    "Every journal line requires an explicit currency-conversion snapshot.");
            }

            if (currencyAmount.ExchangeRate.TenantId != journalDraft.TenantId)
            {
                throw new CurrencyInvariantException(
                    "JOURNAL_CURRENCY_TENANT_MISMATCH",
                    "Journal and currency snapshots must have the same tenant.");
            }

            if (currencyAmount.ExchangeRate.CompanyId != journalDraft.CompanyId)
            {
                throw new CurrencyInvariantException(
                    "JOURNAL_CURRENCY_COMPANY_MISMATCH",
                    "Journal and currency snapshots must have the same company.");
            }

            if (currencyAmount.ExchangeRate.FunctionalCurrency != journalDraft.FunctionalCurrency)
            {
                throw new CurrencyInvariantException(
                    "JOURNAL_FUNCTIONAL_CURRENCY_MISMATCH",
                    "Journal and line snapshots must have the same functional currency.");
            }

            lineAmounts[index] = currencyAmount;
        }

        return new ValidatedJournalCurrencySet(journalDraft, Array.AsReadOnly(lineAmounts));
    }
}
