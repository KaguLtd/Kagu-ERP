using KaguERP.Modules.Reporting.Domain.ControlAccounts;

namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed record PartyStatementAgingCrossFoot
{
    private PartyStatementAgingCrossFoot(
        Guid crossFootId,
        ValidatedPartyStatement statement,
        ValidatedPartyAgingReport aging)
    {
        CrossFootId = crossFootId;
        Statement = statement;
        Aging = aging;
    }

    public Guid CrossFootId { get; }
    public ValidatedPartyStatement Statement { get; }
    public ValidatedPartyAgingReport Aging { get; }

    public static PartyStatementAgingCrossFoot Create(
        Guid crossFootId,
        ValidatedPartyStatement? statement,
        ValidatedPartyAgingReport? aging)
    {
        if (crossFootId == Guid.Empty)
        {
            throw new ReportingInvariantException("PARTY_CROSS_FOOT_REQUIRED", "Party report cross-foot ID is required.");
        }

        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(aging);

        if (statement.PartyAccountId != aging.PartyAccountId ||
            statement.ControlAccountId != aging.ControlAccountId ||
            statement.BalanceSide != aging.BalanceSide)
        {
            throw new ReportingInvariantException(
                "PARTY_CROSS_FOOT_ACCOUNT_MISMATCH",
                "Party statement and aging account context must match.");
        }

        EnsureSameSlice(statement.ReportSlice, aging.ReportSlice);

        if (statement.ClosingExposure != aging.TotalRemaining)
        {
            throw new ReportingInvariantException(
                "PARTY_CROSS_FOOT_TOTAL_MISMATCH",
                "Party statement closing exposure must exactly equal aging remaining total.");
        }

        return new PartyStatementAgingCrossFoot(crossFootId, statement, aging);
    }

    private static void EnsureSameSlice(FinancialReportSlice left, FinancialReportSlice right)
    {
        var sameSlice = left.TenantId == right.TenantId &&
            left.CompanyId == right.CompanyId &&
            left.ReportCode == right.ReportCode &&
            left.ReportDefinitionVersion == right.ReportDefinitionVersion &&
            left.EffectiveAsOf == right.EffectiveAsOf &&
            left.DataCutoffAt == right.DataCutoffAt &&
            left.ProjectionGenerationId == right.ProjectionGenerationId &&
            left.Currency == right.Currency &&
            left.Dimensions.HasSameSelection(right.Dimensions);

        if (!sameSlice)
        {
            throw new ReportingInvariantException(
                "PARTY_CROSS_FOOT_SLICE_MISMATCH",
                "Party statement and aging must use the same report slice.");
        }
    }
}
