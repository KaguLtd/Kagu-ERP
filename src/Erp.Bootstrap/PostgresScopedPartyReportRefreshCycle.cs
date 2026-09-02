using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Infrastructure.Persistence;
using KaguERP.Modules.Parties.Contracts.Reports;
using KaguERP.Modules.Parties.Infrastructure.Reports;
using KaguERP.Modules.Reporting.Application.PartyReports;
using KaguERP.Modules.Reporting.Infrastructure.PartyReports;
using KaguERP.Modules.Reporting.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

internal sealed class PostgresScopedPartyReportRefreshCycle(
    NpgsqlDataSource dataSource,
    PartyReportRefreshWorkerSettings settings,
    PartyReportWorkerExecutionScopeProvider scopeProvider,
    TimeProvider timeProvider) : IPartyReportRefreshCycle
{
    public async ValueTask<PartyReportRefreshCycleResult> ProcessNextAsync(
        CancellationToken cancellationToken = default)
    {
        ExecutionScope scope = await scopeProvider.LoadAsync(cancellationToken);
        var queue = new PostgresPartyReportRefreshWorkStore(dataSource, scope);
        var source = new PostgresPartyReportSource(
            dataSource,
            scope,
            LoadPostingEvidenceAsync,
            LoadPostingLifecycleEvidenceAsync);
        var policySource = new PostgresPartyAgingPolicySource(dataSource, scope);
        var controlSource = new PostgresPartyControlAccountEvidenceSource(
            dataSource,
            scope,
            LoadGeneralLedgerEvidenceAsync);
        IPartyReportProjectionSink sink = new RevalidatingProjectionSink(
            new PostgresPartyReportProjectionSink(dataSource, scope),
            scopeProvider,
            scope);
        var projectionJob = new PartyReportProjectionJob(source, policySource, controlSource, sink);
        var processor = new PartyReportRefreshProcessor(
            queue,
            projectionJob,
            timeProvider,
            settings.LeaseDuration);
        return await processor.ProcessNextAsync(cancellationToken);
    }

    private static async ValueTask<PartySourcePostingEvidence?> LoadPostingEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        long sourceVersion,
        string postingPurpose,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        CancellationToken cancellationToken)
    {
        PostedSourceEvidence? evidence = await PostgresPostedSourceEvidenceLoader.LoadActiveAsync(
            connection,
            transaction,
            scope,
            companyId,
            sourceType,
            sourceEventId,
            sourceVersion,
            postingPurpose,
            effectiveAsOf,
            recordedCutoff,
            cancellationToken);
        return evidence is null
            ? null
            : new PartySourcePostingEvidence(
                evidence.JournalId,
                evidence.SourceType,
                evidence.SourceEventId,
                evidence.SourceVersion,
                evidence.PostingPurpose,
                evidence.EffectiveDate,
                evidence.RecordedAt,
                evidence.PostedAt);
    }

    private static async ValueTask<PartySourcePostingLifecycleEvidence> LoadPostingLifecycleEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        long sourceVersion,
        string postingPurpose,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        CancellationToken cancellationToken)
    {
        PostedSourceLifecycleEvidence lifecycle = await PostgresPostedSourceEvidenceLoader.LoadLifecycleAsync(
            connection,
            transaction,
            scope,
            companyId,
            sourceType,
            sourceEventId,
            sourceVersion,
            postingPurpose,
            effectiveAsOf,
            recordedCutoff,
            cancellationToken);
        PartySourcePostingEvidence? posting = lifecycle.Posting is null
            ? null
            : new PartySourcePostingEvidence(
                lifecycle.Posting.JournalId,
                lifecycle.Posting.SourceType,
                lifecycle.Posting.SourceEventId,
                lifecycle.Posting.SourceVersion,
                lifecycle.Posting.PostingPurpose,
                lifecycle.Posting.EffectiveDate,
                lifecycle.Posting.RecordedAt,
                lifecycle.Posting.PostedAt);
        PartySourcePostingReversalEvidence? reversal = lifecycle.Reversal is null
            ? null
            : new PartySourcePostingReversalEvidence(
                lifecycle.Reversal.OriginalJournalId,
                lifecycle.Reversal.ReversalJournalId,
                lifecycle.Reversal.EffectiveDate,
                lifecycle.Reversal.RecordedAt,
                lifecycle.Reversal.PostedAt,
                lifecycle.Reversal.LinkedAt);
        PartySourcePostingLifecycleState state = lifecycle.State switch
        {
            PostedSourceLifecycleState.NotPosted => PartySourcePostingLifecycleState.NotPosted,
            PostedSourceLifecycleState.Active => PartySourcePostingLifecycleState.Active,
            PostedSourceLifecycleState.Reversed => PartySourcePostingLifecycleState.Reversed,
            _ => throw new InvalidOperationException("Accounting returned an unknown posted-source lifecycle state."),
        };
        return new PartySourcePostingLifecycleEvidence(state, posting, reversal);
    }

    private static async ValueTask<PartyGeneralLedgerControlAccountEvidence> LoadGeneralLedgerEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        PartyReportSourceBatch source,
        CancellationToken cancellationToken)
    {
        PostedControlAccountLineageReference[] lineage = source.PostingLineage
            .Select(item => new PostedControlAccountLineageReference(
                item.JournalId,
                item.SourceType,
                item.SourceEventId,
                item.SourceVersion,
                item.PostingPurpose,
                item.EffectiveDate,
                item.RecordedAt,
                item.PostedAt))
            .ToArray();
        PostedControlAccountBalanceEvidence evidence =
            await PostgresPostedControlAccountBalanceEvidenceLoader.LoadAsync(
                connection,
                transaction,
                scope,
                source.CompanyId,
                source.ControlAccountId,
                source.Currency,
                source.EffectiveAsOf,
                source.RecordedCutoff,
                lineage,
                cancellationToken);
        return new PartyGeneralLedgerControlAccountEvidence(
            evidence.TenantId,
            evidence.CompanyId,
            evidence.ControlAccountId,
            evidence.Currency,
            evidence.EffectiveAsOf,
            evidence.RecordedCutoff,
            evidence.OpeningBalance,
            evidence.Debits,
            evidence.Credits,
            evidence.ClosingBalance,
            evidence.RowCount,
            evidence.SourceChecksumSha256);
    }

    private sealed class RevalidatingProjectionSink(
        IPartyReportProjectionSink inner,
        PartyReportWorkerExecutionScopeProvider scopeProvider,
        ExecutionScope claimedScope) : IPartyReportProjectionSink
    {
        public async ValueTask<PartyReportProjectionJobResult> PublishAsync(
            PartyReportProjectionPublication publication,
            CancellationToken cancellationToken = default)
        {
            ExecutionScope current = await scopeProvider.LoadAsync(cancellationToken);
            if (current.TenantId != claimedScope.TenantId || current.ActorId != claimedScope.ActorId ||
                !current.CompanyIds.SetEquals(claimedScope.CompanyIds) ||
                !current.HasPermission(
                    publication.Source.CompanyId,
                    PartyReportRefreshPermissions.Refresh))
            {
                throw new PartyReportWorkerIdentityException(
                    "PARTY_REPORT_WORKER_SCOPE_CHANGED",
                    "The service identity scope changed before projection publication.");
            }
            return await inner.PublishAsync(publication, cancellationToken);
        }
    }
}
