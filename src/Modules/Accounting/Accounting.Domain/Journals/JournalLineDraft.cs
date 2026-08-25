using System.Collections.ObjectModel;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Domain.Dimensions;

namespace KaguERP.Modules.Accounting.Domain.Journals;

public sealed record JournalLineDraft
{
    private JournalLineDraft(
        Guid accountId,
        Guid? sourceLineId,
        JournalAmount amount,
        ReadOnlyCollection<DimensionAssignment> dimensions,
        JournalCurrencyAmountSnapshot? currencyAmount)
    {
        AccountId = accountId;
        SourceLineId = sourceLineId;
        Amount = amount;
        Dimensions = dimensions;
        CurrencyAmount = currencyAmount;
    }

    public Guid AccountId { get; }

    public Guid? SourceLineId { get; }

    public JournalAmount Amount { get; }

    public IReadOnlyList<DimensionAssignment> Dimensions { get; }

    public JournalCurrencyAmountSnapshot? CurrencyAmount { get; }

    public static JournalLineDraft Create(Guid accountId, Guid? sourceLineId, JournalAmount amount) =>
        CreateCore(accountId, sourceLineId, amount, [], null);

    public static JournalLineDraft Create(
        Guid accountId,
        Guid? sourceLineId,
        JournalAmount amount,
        IEnumerable<DimensionAssignment?>? dimensions)
        => CreateCore(accountId, sourceLineId, amount, dimensions, null);

    public static JournalLineDraft Create(
        Guid accountId,
        Guid? sourceLineId,
        JournalAmount amount,
        IEnumerable<DimensionAssignment?>? dimensions,
        JournalCurrencyAmountSnapshot currencyAmount)
    {
        ArgumentNullException.ThrowIfNull(currencyAmount);

        if (currencyAmount.FunctionalAmount != amount)
        {
            throw new JournalInvariantException(
                "JOURNAL_CURRENCY_AMOUNT_MISMATCH",
                "Journal-line functional amount must match its currency snapshot.");
        }

        return CreateCore(accountId, sourceLineId, amount, dimensions, currencyAmount);
    }

    private static JournalLineDraft CreateCore(
        Guid accountId,
        Guid? sourceLineId,
        JournalAmount amount,
        IEnumerable<DimensionAssignment?>? dimensions,
        JournalCurrencyAmountSnapshot? currencyAmount)
    {
        if (accountId == Guid.Empty)
        {
            throw new JournalInvariantException("JOURNAL_ACCOUNT_REQUIRED", "Journal account ID is required.");
        }

        if (sourceLineId == Guid.Empty)
        {
            throw new JournalInvariantException(
                "JOURNAL_SOURCE_LINE_INVALID",
                "Source line ID must be null or a non-empty UUID.");
        }

        if (!amount.IsValid)
        {
            throw new JournalInvariantException("JOURNAL_AMOUNT_INVALID", "Journal amount is invalid.");
        }

        if (dimensions is null)
        {
            throw new JournalInvariantException(
                "JOURNAL_DIMENSIONS_REQUIRED",
                "Dimension assignments collection is required; use an empty collection when none are assigned.");
        }

        var copiedDimensions = dimensions.ToArray();
        if (copiedDimensions.Any(dimension => dimension is null))
        {
            throw new JournalInvariantException(
                "JOURNAL_DIMENSION_REQUIRED",
                "Dimension assignments cannot contain null values.");
        }

        var validatedDimensions = copiedDimensions.Cast<DimensionAssignment>().ToArray();
        if (validatedDimensions.Select(dimension => dimension.DimensionId).Distinct().Count() != validatedDimensions.Length)
        {
            throw new JournalInvariantException(
                "JOURNAL_DIMENSION_DUPLICATE",
                "A dimension can occur only once on a journal line.");
        }

        Array.Sort(validatedDimensions, static (left, right) => left.DimensionId.CompareTo(right.DimensionId));
        return new JournalLineDraft(
            accountId,
            sourceLineId,
            amount,
            Array.AsReadOnly(validatedDimensions),
            currencyAmount);
    }
}
