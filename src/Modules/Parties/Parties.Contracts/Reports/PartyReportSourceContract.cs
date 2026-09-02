using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KaguERP.Modules.Parties.Contracts.Reports;

public enum PartyReportBalanceSide { Receivable = 1, Payable = 2 }
public enum PartyReportImpactKind { Allocation = 1, Unallocation = 2, WriteOff = 3, WriteOffReversal = 4 }
public enum PartyReportRestrictionEvidence { Unavailable = 0, Clear = 1, Disputed = 2, Blocked = 3, DisputedAndBlocked = 4 }

public sealed record PartyReportSourceQuery(
    Guid TenantId,
    Guid CompanyId,
    Guid PartyAccountId,
    DateOnly EffectiveAsOf,
    DateTimeOffset RecordedCutoff);

public sealed record PartyReportImpactFact(
    Guid EventId,
    PartyReportImpactKind Kind,
    Guid? PaymentId,
    decimal Amount,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    Guid? ReversesEventId);

public sealed record PartyReportPostingLineageFact(
    Guid JournalId,
    string SourceType,
    Guid SourceEventId,
    long SourceVersion,
    string PostingPurpose,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    DateTimeOffset PostedAt);

public sealed record PartyOpenItemSourceFact(
    Guid OpenItemId,
    Guid SourceEventId,
    Guid DueScheduleLineId,
    string SourceType,
    decimal OriginalAmount,
    decimal RemainingAmount,
    DateOnly EffectiveDate,
    DateOnly DueDate,
    DateTimeOffset RecordedAt,
    PartyReportRestrictionEvidence RestrictionEvidence,
    IReadOnlyList<PartyReportImpactFact> Impacts);

public sealed class PartyReportSourceBatch
{
    private PartyReportSourceBatch(
        Guid tenantId, Guid companyId, Guid partyAccountId, Guid controlAccountId,
        PartyReportBalanceSide balanceSide, string currency, DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff, decimal openingExposure, string sourceWatermarkFrom,
        string sourceWatermarkTo,
        ReadOnlyCollection<PartyOpenItemSourceFact> openItems,
        ReadOnlyCollection<PartyReportPostingLineageFact> postingLineage)
    {
        TenantId = tenantId; CompanyId = companyId; PartyAccountId = partyAccountId;
        ControlAccountId = controlAccountId; BalanceSide = balanceSide; Currency = currency;
        EffectiveAsOf = effectiveAsOf; RecordedCutoff = recordedCutoff; OpeningExposure = openingExposure;
        SourceWatermarkFrom = sourceWatermarkFrom; SourceWatermarkTo = sourceWatermarkTo;
        SourceChecksumSha256 = ComputeChecksum(
            tenantId, companyId, partyAccountId, controlAccountId, balanceSide, currency,
            effectiveAsOf, recordedCutoff, openingExposure, sourceWatermarkFrom,
            sourceWatermarkTo, openItems, postingLineage);
        OpenItems = openItems;
        PostingLineage = postingLineage;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PartyAccountId { get; }
    public Guid ControlAccountId { get; }
    public PartyReportBalanceSide BalanceSide { get; }
    public string Currency { get; }
    public DateOnly EffectiveAsOf { get; }
    public DateTimeOffset RecordedCutoff { get; }
    public decimal OpeningExposure { get; }
    public string SourceWatermarkFrom { get; }
    public string SourceWatermarkTo { get; }
    public string SourceChecksumSha256 { get; }
    public IReadOnlyList<PartyOpenItemSourceFact> OpenItems { get; }
    public IReadOnlyList<PartyReportPostingLineageFact> PostingLineage { get; }

    public static PartyReportSourceBatch Create(
        Guid tenantId, Guid companyId, Guid partyAccountId, Guid controlAccountId,
        PartyReportBalanceSide balanceSide, string currency, DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff, decimal openingExposure, string sourceWatermarkFrom,
        string sourceWatermarkTo,
        IEnumerable<PartyOpenItemSourceFact?>? openItems,
        IEnumerable<PartyReportPostingLineageFact?>? postingLineage)
    {
        RequireId(tenantId, nameof(tenantId)); RequireId(companyId, nameof(companyId));
        RequireId(partyAccountId, nameof(partyAccountId)); RequireId(controlAccountId, nameof(controlAccountId));
        if (!Enum.IsDefined(balanceSide)) throw new ArgumentOutOfRangeException(nameof(balanceSide));
        if (currency is null || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency must contain three uppercase ASCII letters.", nameof(currency));
        if (effectiveAsOf == default) throw new ArgumentException("Effective as-of date is required.", nameof(effectiveAsOf));
        if (recordedCutoff.Offset != TimeSpan.Zero) throw new ArgumentException("Recorded cutoff must be UTC.", nameof(recordedCutoff));
        ArgumentOutOfRangeException.ThrowIfNegative(openingExposure);
        string from = RequireText(sourceWatermarkFrom, nameof(sourceWatermarkFrom));
        string to = RequireText(sourceWatermarkTo, nameof(sourceWatermarkTo));
        ArgumentNullException.ThrowIfNull(openItems);
        PartyOpenItemSourceFact?[] copied = openItems.ToArray();
        if (copied.Any(item => item is null)) throw new ArgumentException("Open-item facts cannot contain null.", nameof(openItems));
        PartyOpenItemSourceFact[] supplied = copied.Cast<PartyOpenItemSourceFact>().ToArray();
        if (supplied.Select(item => item.OpenItemId).Distinct().Count() != supplied.Length)
            throw new ArgumentException("Open-item IDs must be unique.", nameof(openItems));
        foreach (PartyOpenItemSourceFact item in supplied) ValidateItem(item, effectiveAsOf, recordedCutoff);
        PartyOpenItemSourceFact[] validated = supplied
            .Select(item => item with { Impacts = Array.AsReadOnly(item.Impacts.ToArray()) })
            .ToArray();
        ArgumentNullException.ThrowIfNull(postingLineage);
        PartyReportPostingLineageFact?[] lineageCopy = postingLineage.ToArray();
        if (lineageCopy.Any(item => item is null))
            throw new ArgumentException("Posting lineage cannot contain null.", nameof(postingLineage));
        PartyReportPostingLineageFact[] validatedLineage = lineageCopy
            .Cast<PartyReportPostingLineageFact>()
            .ToArray();
        if (validatedLineage.Select(item => item.JournalId).Distinct().Count() != validatedLineage.Length)
            throw new ArgumentException("Posting journal IDs must be unique.", nameof(postingLineage));
        if (validatedLineage
            .Select(item => (item.SourceType, item.SourceEventId, item.SourceVersion, item.PostingPurpose))
            .Distinct()
            .Count() != validatedLineage.Length)
            throw new ArgumentException("Posting source identities must be unique.", nameof(postingLineage));
        foreach (PartyReportPostingLineageFact lineage in validatedLineage)
            ValidateLineage(lineage, effectiveAsOf, recordedCutoff);
        return new PartyReportSourceBatch(tenantId, companyId, partyAccountId, controlAccountId,
            balanceSide, currency, effectiveAsOf, recordedCutoff, openingExposure, from, to,
            Array.AsReadOnly(validated), Array.AsReadOnly(validatedLineage));
    }

    private static void ValidateLineage(
        PartyReportPostingLineageFact lineage,
        DateOnly effectiveAsOf,
        DateTimeOffset cutoff)
    {
        RequireId(lineage.JournalId, nameof(lineage.JournalId));
        RequireId(lineage.SourceEventId, nameof(lineage.SourceEventId));
        RequireText(lineage.SourceType, nameof(lineage.SourceType));
        RequireText(lineage.PostingPurpose, nameof(lineage.PostingPurpose));
        if (lineage.SourceVersion <= 0 || lineage.EffectiveDate == default ||
            lineage.EffectiveDate > effectiveAsOf || lineage.RecordedAt.Offset != TimeSpan.Zero ||
            lineage.PostedAt.Offset != TimeSpan.Zero || lineage.RecordedAt > cutoff ||
            lineage.PostedAt > cutoff)
        {
            throw new ArgumentException(
                "Posting lineage is outside the requested cut or invalid.",
                nameof(lineage));
        }
    }

    private static void ValidateItem(PartyOpenItemSourceFact item, DateOnly effectiveAsOf, DateTimeOffset cutoff)
    {
        RequireId(item.OpenItemId, nameof(item.OpenItemId)); RequireId(item.SourceEventId, nameof(item.SourceEventId));
        RequireId(item.DueScheduleLineId, nameof(item.DueScheduleLineId));
        RequireText(item.SourceType, nameof(item.SourceType));
        if (item.OriginalAmount <= 0 || item.RemainingAmount < 0 || item.RemainingAmount > item.OriginalAmount)
            throw new ArgumentException("Open-item amounts are invalid.", nameof(item));
        if (item.EffectiveDate == default || item.EffectiveDate > effectiveAsOf || item.DueDate == default ||
            item.RecordedAt.Offset != TimeSpan.Zero || item.RecordedAt > cutoff ||
            !Enum.IsDefined(item.RestrictionEvidence))
            throw new ArgumentException("Open-item date or restriction evidence is invalid.", nameof(item));
        if (item.Impacts is null || item.Impacts.Any(impact => impact is null))
            throw new ArgumentException("Impact facts are required.", nameof(item));
        if (item.Impacts.Select(impact => impact.EventId).Distinct().Count() != item.Impacts.Count)
            throw new ArgumentException("Impact event IDs must be unique per open item.", nameof(item));
        foreach (PartyReportImpactFact impact in item.Impacts)
        {
            RequireId(impact.EventId, nameof(impact.EventId));
            if (!Enum.IsDefined(impact.Kind) || impact.Amount <= 0 || impact.EffectiveDate > effectiveAsOf ||
                impact.RecordedAt.Offset != TimeSpan.Zero || impact.RecordedAt > cutoff)
                throw new ArgumentException("Impact fact is outside the requested cut or invalid.", nameof(item));
            bool paymentImpact = impact.Kind is PartyReportImpactKind.Allocation or PartyReportImpactKind.Unallocation;
            if (paymentImpact != (impact.PaymentId is { } paymentId && paymentId != Guid.Empty))
                throw new ArgumentException("Impact payment evidence does not match its kind.", nameof(item));
        }
    }

    private static void RequireId(Guid value, string parameterName)
    {
        if (value == Guid.Empty) throw new ArgumentException("ID is required.", parameterName);
    }
    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Lineage text is required.", parameterName);
        return value.Trim();
    }
    private static string ComputeChecksum(
        Guid tenantId, Guid companyId, Guid partyAccountId, Guid controlAccountId,
        PartyReportBalanceSide balanceSide, string currency, DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff, decimal openingExposure, string watermarkFrom,
        string watermarkTo, IEnumerable<PartyOpenItemSourceFact> openItems,
        IEnumerable<PartyReportPostingLineageFact> postingLineage)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            hash.AppendData(Encoding.ASCII.GetBytes(bytes.Length.ToString(CultureInfo.InvariantCulture)));
            hash.AppendData(":"u8);
            hash.AppendData(bytes);
        }
        Add("party-report-source-v2"); Add(tenantId.ToString("N")); Add(companyId.ToString("N"));
        Add(partyAccountId.ToString("N")); Add(controlAccountId.ToString("N"));
        Add(((int)balanceSide).ToString(CultureInfo.InvariantCulture)); Add(currency);
        Add(effectiveAsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(recordedCutoff.ToString("O", CultureInfo.InvariantCulture));
        Add(openingExposure.ToString("G29", CultureInfo.InvariantCulture)); Add(watermarkFrom); Add(watermarkTo);
        foreach (PartyOpenItemSourceFact item in openItems.OrderBy(item => item.OpenItemId))
        {
            Add(item.OpenItemId.ToString("N")); Add(item.SourceEventId.ToString("N"));
            Add(item.DueScheduleLineId.ToString("N")); Add(item.SourceType);
            Add(item.OriginalAmount.ToString("G29", CultureInfo.InvariantCulture));
            Add(item.RemainingAmount.ToString("G29", CultureInfo.InvariantCulture));
            Add(item.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Add(item.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Add(item.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
            Add(((int)item.RestrictionEvidence).ToString(CultureInfo.InvariantCulture));
            foreach (PartyReportImpactFact impact in item.Impacts.OrderBy(impact => impact.EventId))
            {
                Add(impact.EventId.ToString("N")); Add(((int)impact.Kind).ToString(CultureInfo.InvariantCulture));
                Add(impact.PaymentId?.ToString("N") ?? string.Empty);
                Add(impact.Amount.ToString("G29", CultureInfo.InvariantCulture));
                Add(impact.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                Add(impact.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
                Add(impact.ReversesEventId?.ToString("N") ?? string.Empty);
            }
        }
        foreach (PartyReportPostingLineageFact lineage in postingLineage
                     .OrderBy(item => item.SourceType, StringComparer.Ordinal)
                     .ThenBy(item => item.SourceEventId)
                     .ThenBy(item => item.SourceVersion)
                     .ThenBy(item => item.PostingPurpose, StringComparer.Ordinal)
                     .ThenBy(item => item.JournalId))
        {
            Add(lineage.JournalId.ToString("N")); Add(lineage.SourceType);
            Add(lineage.SourceEventId.ToString("N"));
            Add(lineage.SourceVersion.ToString(CultureInfo.InvariantCulture));
            Add(lineage.PostingPurpose);
            Add(lineage.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Add(lineage.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
            Add(lineage.PostedAt.ToString("O", CultureInfo.InvariantCulture));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

public interface IPartyReportSource
{
    ValueTask<PartyReportSourceBatch?> LoadAsync(
        PartyReportSourceQuery query,
        CancellationToken cancellationToken = default);
}
