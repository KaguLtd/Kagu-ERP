using System.Collections.ObjectModel;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;

namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed class ValidatedPartyStatement
{
    private ValidatedPartyStatement(
        Guid statementId,
        Guid partyAccountId,
        Guid controlAccountId,
        PartyBalanceSide balanceSide,
        decimal openingExposure,
        decimal closingExposure,
        FinancialReportSlice reportSlice,
        ReadOnlyCollection<PartyStatementLine> lines)
    {
        StatementId = statementId;
        PartyAccountId = partyAccountId;
        ControlAccountId = controlAccountId;
        BalanceSide = balanceSide;
        OpeningExposure = openingExposure;
        ClosingExposure = closingExposure;
        ReportSlice = reportSlice;
        Lines = lines;
    }

    public Guid StatementId { get; }

    public Guid PartyAccountId { get; }

    public Guid ControlAccountId { get; }

    public PartyBalanceSide BalanceSide { get; }

    public decimal OpeningExposure { get; }

    public decimal ClosingExposure { get; }

    public FinancialReportSlice ReportSlice { get; }

    public IReadOnlyList<PartyStatementLine> Lines { get; }

    public static ValidatedPartyStatement Create(
        Guid statementId,
        Guid partyAccountId,
        Guid controlAccountId,
        PartyBalanceSide balanceSide,
        decimal openingExposure,
        FinancialReportSlice? reportSlice,
        IEnumerable<PartyStatementEventSnapshot?>? events)
    {
        RequireId(statementId, "PARTY_STATEMENT_REQUIRED", "Party statement ID is required.");
        RequireId(partyAccountId, "PARTY_REPORT_ACCOUNT_REQUIRED", "Party-account ID is required.");
        RequireId(controlAccountId, "PARTY_REPORT_CONTROL_ACCOUNT_REQUIRED", "Party control-account ID is required.");
        ArgumentNullException.ThrowIfNull(reportSlice);

        if (!Enum.IsDefined(balanceSide))
        {
            throw new ReportingInvariantException("PARTY_REPORT_BALANCE_SIDE_INVALID", "Party balance side is invalid.");
        }

        if (openingExposure < decimal.Zero)
        {
            throw new ReportingInvariantException(
                "PARTY_STATEMENT_OPENING_INVALID",
                "This technical statement subset requires a non-negative opening exposure.");
        }

        if (events is null)
        {
            throw new ReportingInvariantException("PARTY_STATEMENT_EVENTS_REQUIRED", "Party statement event collection is required.");
        }

        var copiedEvents = events.ToArray();
        if (copiedEvents.Any(eventSnapshot => eventSnapshot is null))
        {
            throw new ReportingInvariantException(
                "PARTY_STATEMENT_EVENT_REQUIRED",
                "Party statement event collection cannot contain null values.");
        }

        var validatedEvents = copiedEvents.Cast<PartyStatementEventSnapshot>().ToArray();
        var eventIds = new HashSet<(Guid TenantId, Guid EventId)>();
        var sequenceKeys = new HashSet<(DateOnly EffectiveDate, long SequenceKey)>();
        foreach (var eventSnapshot in validatedEvents)
        {
            EnsureEventContext(eventSnapshot, partyAccountId, controlAccountId, reportSlice);
            if (!eventIds.Add((eventSnapshot.TenantId, eventSnapshot.EventId)))
            {
                throw new ReportingInvariantException(
                    "PARTY_STATEMENT_EVENT_DUPLICATE",
                    "A Party statement event ID can occur only once in a tenant.");
            }


            if (!sequenceKeys.Add((eventSnapshot.EffectiveDate, eventSnapshot.SequenceKey)))
            {
                throw new ReportingInvariantException(
                    "PARTY_STATEMENT_SEQUENCE_DUPLICATE",
                    "A Party statement sequence key can occur only once on an effective date.");
            }
        }

        var includedEvents = validatedEvents
            .Where(eventSnapshot =>
                eventSnapshot.EffectiveDate <= reportSlice.EffectiveAsOf &&
                eventSnapshot.RecordedAt <= reportSlice.DataCutoffAt)
            .OrderBy(eventSnapshot => eventSnapshot.EffectiveDate)
            .ThenBy(eventSnapshot => eventSnapshot.SequenceKey)
            .ThenBy(eventSnapshot => eventSnapshot.RecordedAt)
            .ThenBy(eventSnapshot => eventSnapshot.EventId)
            .ToArray();

        var lines = new PartyStatementLine[includedEvents.Length];
        var runningExposure = openingExposure;
        for (var index = 0; index < includedEvents.Length; index++)
        {
            try
            {
                runningExposure += includedEvents[index].ExposureEffect;
            }
            catch (OverflowException exception)
            {
                throw new ReportingInvariantException(
                    "PARTY_STATEMENT_BALANCE_OVERFLOW",
                    $"Party statement balance arithmetic overflowed: {exception.Message}");
            }

            if (runningExposure < decimal.Zero)
            {
                throw new ReportingInvariantException(
                    "PARTY_STATEMENT_NEGATIVE_EXPOSURE_UNSUPPORTED",
                    "This technical statement subset does not support credit or advance exposure.");
            }

            lines[index] = new PartyStatementLine(includedEvents[index], runningExposure);
        }

        return new ValidatedPartyStatement(
            statementId,
            partyAccountId,
            controlAccountId,
            balanceSide,
            openingExposure,
            runningExposure,
            reportSlice,
            Array.AsReadOnly(lines));
    }

    private static void EnsureEventContext(
        PartyStatementEventSnapshot eventSnapshot,
        Guid partyAccountId,
        Guid controlAccountId,
        FinancialReportSlice reportSlice)
    {
        if (eventSnapshot.TenantId != reportSlice.TenantId)
        {
            throw Mismatch("PARTY_STATEMENT_TENANT_MISMATCH", "tenant");
        }

        if (eventSnapshot.CompanyId != reportSlice.CompanyId)
        {
            throw Mismatch("PARTY_STATEMENT_COMPANY_MISMATCH", "company");
        }

        if (eventSnapshot.PartyAccountId != partyAccountId)
        {
            throw Mismatch("PARTY_STATEMENT_ACCOUNT_MISMATCH", "party account");
        }

        if (eventSnapshot.ControlAccountId != controlAccountId)
        {
            throw Mismatch("PARTY_STATEMENT_CONTROL_ACCOUNT_MISMATCH", "control account");
        }

        if (eventSnapshot.Currency != reportSlice.Currency)
        {
            throw Mismatch("PARTY_STATEMENT_CURRENCY_MISMATCH", "currency");
        }
    }

    private static ReportingInvariantException Mismatch(string code, string field) =>
        new(code, $"Party statement event must use the report {field}.");

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReportingInvariantException(code, message);
        }
    }
}
