using KaguERP.Modules.Parties.Contracts.Reports;
using KaguERP.Modules.Reporting.Application.PartyReports;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using KaguERP.Modules.Reporting.Domain.PartyReports;

internal static class PartySourceChecksumContractCheck
{
    internal static void Run()
    {
        Guid tenantId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        DateOnly asOf = new(2026, 8, 27);
        DateTimeOffset cutoff = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
        var impacts = new List<PartyReportImpactFact>
        {
            new(Guid.NewGuid(), PartyReportImpactKind.Allocation, Guid.NewGuid(), 10m,
                asOf.AddDays(-1), cutoff.AddMinutes(-1), null),
        };
        var item = new PartyOpenItemSourceFact(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "sales.invoice", 50m, 40m,
            asOf.AddDays(-10), asOf.AddDays(10), cutoff.AddMinutes(-2),
            PartyReportRestrictionEvidence.Clear, impacts);
        PartyReportSourceBatch first = PartyReportSourceBatch.Create(
            tenantId, companyId, Guid.NewGuid(), Guid.NewGuid(), PartyReportBalanceSide.Receivable,
            "GBP", asOf, cutoff, 0m, "event:1", "event:2", [item]);
        PartyReportSourceBatch replay = PartyReportSourceBatch.Create(
            first.TenantId, first.CompanyId, first.PartyAccountId, first.ControlAccountId,
            first.BalanceSide, first.Currency, first.EffectiveAsOf, first.RecordedCutoff,
            first.OpeningExposure, first.SourceWatermarkFrom, first.SourceWatermarkTo, [item]);
        if (first.SourceChecksumSha256 != replay.SourceChecksumSha256 || first.SourceChecksumSha256.Length != 64)
        {
            throw new InvalidOperationException("Equivalent Party source batches must have the same SHA-256 checksum.");
        }
        impacts.Clear();
        if (first.OpenItems[0].Impacts.Count != 1)
        {
            throw new InvalidOperationException("Party source batch retained a mutable impact collection.");
        }
        PartyReportSourceBatch changed = PartyReportSourceBatch.Create(
            first.TenantId, first.CompanyId, first.PartyAccountId, first.ControlAccountId,
            first.BalanceSide, first.Currency, first.EffectiveAsOf, first.RecordedCutoff,
            first.OpeningExposure, first.SourceWatermarkFrom, "event:3", [item with { Impacts = first.OpenItems[0].Impacts }]);
        if (first.SourceChecksumSha256 == changed.SourceChecksumSha256)
        {
            throw new InvalidOperationException("Changed Party source lineage reused its checksum.");
        }

        CalendarDayAgingPolicySnapshot policy = CalendarDayAgingPolicySnapshot.Create(
            tenantId, companyId, Guid.NewGuid(), 1,
            [CalendarDayAgingBucket.Create("all", int.MinValue, int.MaxValue)]);
        PartyReportProjectionBuilder.ProjectionPair pair = PartyReportProjectionBuilder.BuildPair(
            first, policy, "party.account.detail", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            1, Guid.NewGuid(), cutoff.AddMinutes(1));
        if (pair.Statement.ReportSlice.ReportCode != pair.Aging.ReportSlice.ReportCode ||
            pair.Statement.ReportSlice.ProjectionGenerationId != pair.Aging.ReportSlice.ProjectionGenerationId ||
            pair.Statement.ClosingExposure != pair.Aging.TotalRemaining)
        {
            throw new InvalidOperationException("Party statement and aging pair must share and cross-foot one report slice.");
        }

        var sink = new RecordingSink();
        var job = new PartyReportProjectionJob(
            new FixedPartySource(first), new FixedPolicySource(policy),
            new BalancedControlAccountSource(), sink);
        var command = new PartyReportProjectionJobCommand(
            new PartyReportSourceQuery(tenantId, companyId, first.PartyAccountId, asOf, cutoff),
            "party.account.detail", 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), cutoff.AddMinutes(1), "scheduled-refresh");
        PartyReportProjectionJobResult result = job.RunAsync(command).AsTask().GetAwaiter().GetResult();
        if (!result.Created || sink.PublishCount != 1)
        {
            throw new InvalidOperationException("Valid Party report job did not publish exactly once.");
        }

        var deniedSink = new RecordingSink();
        var deniedJob = new PartyReportProjectionJob(
            new FixedPartySource(first), new FixedPolicySource(policy),
            new BalancedControlAccountSource(), deniedSink);
        try
        {
            deniedJob.RunAsync(command with
            {
                SourceQuery = command.SourceQuery with { CompanyId = Guid.NewGuid() },
            }).AsTask().GetAwaiter().GetResult();
            throw new InvalidOperationException("Mismatched source scope was accepted.");
        }
        catch (PartyReportProjectionJobException exception) when (
            exception.Code == "PARTY_REPORT_SOURCE_SCOPE_MISMATCH")
        {
            if (deniedSink.PublishCount != 0)
            {
                throw new InvalidOperationException("Scope mismatch reached the projection sink.");
            }
        }

        var wrongBalanceSink = new RecordingSink();
        var wrongBalanceJob = new PartyReportProjectionJob(
            new FixedPartySource(first), new FixedPolicySource(policy),
            new BalancedControlAccountSource(Guid.NewGuid()), wrongBalanceSink);
        try
        {
            wrongBalanceJob.RunAsync(command).AsTask().GetAwaiter().GetResult();
            throw new InvalidOperationException("Wrong control-account evidence was accepted.");
        }
        catch (PartyReportProjectionJobException exception) when (
            exception.Code == "PARTY_CONTROL_ACCOUNT_EVIDENCE_MISMATCH")
        {
            if (wrongBalanceSink.PublishCount != 0)
            {
                throw new InvalidOperationException("Wrong control-account evidence reached the projection sink.");
            }
        }
    }

    private sealed class FixedPartySource(PartyReportSourceBatch source) : IPartyReportSource
    {
        public ValueTask<PartyReportSourceBatch?> LoadAsync(
            PartyReportSourceQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PartyReportSourceBatch?>(source);
    }

    private sealed class FixedPolicySource(CalendarDayAgingPolicySnapshot policy) : IPartyAgingPolicySource
    {
        public ValueTask<CalendarDayAgingPolicySnapshot?> LoadAsync(
            Guid tenantId, Guid companyId, DateOnly effectiveAsOf, DateTimeOffset recordedCutoff,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<CalendarDayAgingPolicySnapshot?>(policy);
    }

    private sealed class BalancedControlAccountSource(Guid? forcedControlAccountId = null)
        : IPartyControlAccountEvidenceSource
    {
        public ValueTask<PartyControlAccountEvidence?> LoadAsync(
            FinancialReportSlice reportSlice, Guid controlAccountId,
            CancellationToken cancellationToken = default)
        {
            Guid evidenceAccountId = forcedControlAccountId ?? controlAccountId;
            ControlAccountBalanceSnapshot subledger = ControlAccountBalanceSnapshot.Create(
                Guid.NewGuid(), evidenceAccountId, LedgerSide.Subledger, 0m, 50m, 10m, 40m, 2,
                new string('a', 64), reportSlice);
            ControlAccountBalanceSnapshot generalLedger = ControlAccountBalanceSnapshot.Create(
                Guid.NewGuid(), evidenceAccountId, LedgerSide.GeneralLedger, 0m, 50m, 10m, 40m, 2,
                new string('b', 64), reportSlice);
            return ValueTask.FromResult<PartyControlAccountEvidence?>(
                new PartyControlAccountEvidence(subledger, generalLedger));
        }
    }

    private sealed class RecordingSink : IPartyReportProjectionSink
    {
        public int PublishCount { get; private set; }

        public ValueTask<PartyReportProjectionJobResult> PublishAsync(
            PartyReportProjectionPublication publication,
            CancellationToken cancellationToken = default)
        {
            PublishCount++;
            return ValueTask.FromResult(new PartyReportProjectionJobResult(
                publication.Command.ProjectionGenerationId, publication.Command.StatementId,
                publication.Command.AgingReportId, true));
        }
    }
}
