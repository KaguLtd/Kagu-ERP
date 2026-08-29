using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Journals;
using AccountingAccounts = KaguERP.Modules.Accounting.Domain.Accounts;
using AccountingApplication = KaguERP.Modules.Accounting.Application.Posting;
using AccountingCurrencies = KaguERP.Modules.Accounting.Domain.Currencies;
using AccountingDimensions = KaguERP.Modules.Accounting.Domain.Dimensions;
using AccountingPeriods = KaguERP.Modules.Accounting.Domain.Periods;
using AccountingReversals = KaguERP.Modules.Accounting.Domain.Reversals;
using ApprovalApplication = KaguERP.BuildingBlocks.Application.Approvals;
using PartyAllocations = KaguERP.Modules.Parties.Domain.Allocations;
using PartyDueSchedules = KaguERP.Modules.Parties.Domain.DueSchedules;
using PartyOpeningApplication = KaguERP.Modules.Parties.Application.Openings;
using PartyOpenings = KaguERP.Modules.Parties.Domain.Openings;
using PartyOpenItems = KaguERP.Modules.Parties.Domain.OpenItems;
using PartyReportContracts = KaguERP.Modules.Parties.Contracts.Reports;
using PartyRestrictionApplication = KaguERP.Modules.Parties.Application.OpenItems;
using ReportingApplication = KaguERP.Modules.Reporting.Application.PartyReports;
using ReportingControlAccounts = KaguERP.Modules.Reporting.Domain.ControlAccounts;
using ReportingParty = KaguERP.Modules.Reporting.Domain.PartyReports;
using TreasuryPayments = KaguERP.Modules.Treasury.Domain.Payments;
using TreasuryReconciliation = KaguERP.Modules.Treasury.Domain.Reconciliation;
using TreasuryStatements = KaguERP.Modules.Treasury.Domain.Statements;

var checks = new (string Name, Action Run)[]
{
    ("ACC-INV-001 balanced decimal journal", BalancedJournalIsAccepted),
    ("ACC-INV-001 exact imbalance rejection", ImbalancedJournalIsRejected),
    ("Journal line amount boundaries", InvalidJournalAmountsAreRejected),
    ("Journal scope and source requirements", MissingContextIsRejected),
    ("Journal UTC timestamp requirement", NonUtcRecordedTimeIsRejected),
    ("Journal currency format requirement", InvalidCurrencyIsRejected),
    ("Validated journal immutability", ValidatedJournalCopiesAndProtectsLines),
    ("ACC-INV-001 deterministic decimal distributions", DecimalDistributionsRemainBalanced),
    ("ACC-INV-005 canonical posting identity", PostingIdentityIsCanonicalAndComparable),
    ("ACC-INV-005 duplicate source rejection", DuplicateJournalSourceIsRejected),
    ("ACC-INV-005 scope separation and set immutability", PostingIdentityScopeAndDraftSetAreProtected),
    ("PARTY-INV-001 allocation amount and capacity boundaries", AllocationAmountBoundariesAreEnforced),
    ("PARTY-INV-002 allocation scope and currency boundaries", AllocationScopeAndCurrencyAreEnforced),
    ("PARTY-INV-003 multi-item allocation capacity", MultiItemAllocationCapacityIsEnforced),
    ("PARTY allocation order and immutability", AllocationOrderAndImmutabilityAreProtected),
    ("ACC-PER-001 close progression", PeriodCloseProgressionIsEnforced),
    ("ACC-PER-002 scoped lock isolation", PeriodLockScopesAreIsolated),
    ("ACC-PER-003 fail-closed standard posting", StandardPostingPeriodGateFailsClosed),
    ("ACC-INV-006 account snapshot boundaries", AccountSnapshotBoundariesAreEnforced),
    ("ACC-INV-006 journal account scope and chart version", JournalAccountScopeAndVersionAreEnforced),
    ("ACC-INV-006 journal account completeness and postability", JournalAccountsMustBeCompleteAndPostable),
    ("ACC-INV-007 dimension snapshot boundaries", DimensionSnapshotBoundariesAreEnforced),
    ("ACC-INV-007 journal line dimension immutability", JournalLineDimensionsAreImmutable),
    ("ACC-INV-007 required dimension completeness", RequiredJournalDimensionsAreEnforced),
    ("ACC-INV-008 rate and rounding snapshot boundaries", CurrencySnapshotBoundariesAreEnforced),
    ("ACC-INV-008 deterministic functional amount reproduction", CurrencyConversionIsReproducible),
    ("ACC-INV-008 journal currency scope and completeness", JournalCurrencyContextIsEnforced),
    ("ACC-INV-003 exact linked journal reversal", JournalReversalIsExactAndLinked),
    ("ACC-INV-003 reversal context boundaries", JournalReversalContextIsEnforced),
    ("ACC-INV-003 duplicate reversal intent and immutability", DuplicateJournalReversalIsRejected),
    ("PARTY-DUE-001 due-line boundaries", DueScheduleLineBoundariesAreEnforced),
    ("PARTY-DUE-002 exact schedule total and immutability", DueScheduleTotalAndImmutabilityAreEnforced),
    ("PARTY-DUE-002 due-schedule scope isolation", DueScheduleScopeIsEnforced),
    ("PARTY-OI-001 bitemporal remaining derivation", OpenItemRemainingIsDerivedAsOf),
    ("PARTY-OI-002 append-only counter-event boundaries", OpenItemCounterEventsAreEnforced),
    ("PARTY-OI-003 bitemporal dispute and block evidence", OpenItemRestrictionEvidenceIsDerivedAsOf),
    ("PARTY-ACC-002 opening source boundaries", PartyAccountOpeningBoundariesAreEnforced),
    ("PARTY-ACC-002 opening preparation permission", PartyAccountOpeningPermissionAndScopeFailClosed),
    ("PARTY-INV-004 payment allocation bitemporal derivation", PaymentAllocationIsDerivedAsOf),
    ("PARTY-INV-004 payment allocation scope and capacity", PaymentAllocationScopeAndCapacityAreEnforced),
    ("PARTY-INV-004 exact unallocation linkage", PaymentUnallocationLinkageIsEnforced),
    ("BNK-INV-002 same-currency payment rate boundaries", PaymentRateBoundariesAreEnforced),
    ("BNK-PAY-001 scoped payment economic event", PaymentEconomicEventBoundariesAreEnforced),
    ("BNK-PAY-002 canonical payment source uniqueness", PaymentSourceUniquenessAndImmutabilityAreEnforced),
    ("BNK-STMT-001 normalized statement-line boundaries", StatementLineBoundariesAreEnforced),
    ("BNK-INV-003 statement-line deduplication", StatementLineUniquenessAndImmutabilityAreEnforced),
    ("BNK-REC-001/002 many-to-many reconciliation proposal", ReconciliationProposalBoundariesAreEnforced),
    ("RPT-INV-001 versioned report slice boundaries", FinancialReportSliceBoundariesAreEnforced),
    ("RPT-CTRL-002 exact balance cross-foot", ControlAccountBalanceCrossFootIsEnforced),
    ("RPT-CTRL-001 exact reconciliation context", ControlAccountReconciliationContextIsEnforced),
    ("RPT-PARTY-001 normalized statement event boundaries", PartyStatementEventBoundariesAreEnforced),
    ("RPT-PARTY-001 bitemporal statement running balance", PartyStatementIsDerivedBitemporally),
    ("RPT-PARTY-002 explicit aging policy and totals", PartyAgingPolicyAndTotalsAreEnforced),
    ("RPT-PARTY-002 statement-aging exact cross-foot", PartyStatementAgingCrossFootIsEnforced),
    ("RPT-PARTY source contract bitemporal boundaries", PartyReportSourceContractBoundariesAreEnforced),
    ("RPT-PARTY source projection exact cross-foot", PartyReportSourceProjectionCrossFootIsEnforced),
    ("MP-03 golden receivable-to-report exact cross-foot", GoldenPartyCollectionCycleCrossFootIsExact),
    ("API-003 authorized journal posting candidate", AuthorizedPostingCandidateRequiresCompleteEvidence),
    ("API-003 journal posting permission and scope", PostingCandidatePermissionAndScopeFailClosed),
    ("ACC-PER-003 journal posting candidate period gate", PostingCandidatePeriodAndDraftEvidenceFailClosed),
    ("WFL-INV-002 approval exact subject version", ApprovalEvidenceBindsExactSubjectVersion),
    ("WFL-INV-003 approval maker-checker", ApprovalEvidenceRejectsMakerCheckerConflict),
    ("Workflow distinct-person quorum", ApprovalEvidenceRequiresDistinctQuorum),
};

static void OpenItemRestrictionEvidenceIsDerivedAsOf()
{
    Guid tenantId = Guid.NewGuid();
    Guid companyId = Guid.NewGuid();
    Guid partyAccountId = Guid.NewGuid();
    Guid dueLineId = Guid.NewGuid();
    DateTimeOffset recordedAt = new(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);
    PartyOpenItems.OpenItemRestrictionEvent dispute = PartyOpenItems.OpenItemRestrictionEvent.Create(
        Guid.NewGuid(), tenantId, companyId, partyAccountId, dueLineId,
        PartyOpenItems.OpenItemRestrictionKind.Dispute,
        PartyOpenItems.OpenItemRestrictionAction.Applied,
        "invoice-under-review", new DateOnly(2026, 8, 1), recordedAt);
    PartyOpenItems.OpenItemRestrictionEvent disputeRelease = PartyOpenItems.OpenItemRestrictionEvent.Create(
        Guid.NewGuid(), tenantId, companyId, partyAccountId, dueLineId,
        PartyOpenItems.OpenItemRestrictionKind.Dispute,
        PartyOpenItems.OpenItemRestrictionAction.Released,
        "review-resolved", new DateOnly(2026, 8, 3), recordedAt.AddMinutes(2), dispute.EventId);
    PartyOpenItems.OpenItemRestrictionEvent collectionBlock = PartyOpenItems.OpenItemRestrictionEvent.Create(
        Guid.NewGuid(), tenantId, companyId, partyAccountId, dueLineId,
        PartyOpenItems.OpenItemRestrictionKind.CollectionBlock,
        PartyOpenItems.OpenItemRestrictionAction.Applied,
        "legal-hold", new DateOnly(2026, 8, 2), recordedAt.AddMinutes(3));

    PartyOpenItems.DerivedOpenItemRestrictionSnapshot historical =
        PartyOpenItems.DerivedOpenItemRestrictionSnapshot.Create(
            tenantId, companyId, partyAccountId, dueLineId, new DateOnly(2026, 8, 31),
            recordedAt.AddMinutes(1), [dispute, disputeRelease, collectionBlock]);
    Equal(true, historical.IsDisputed && !historical.IsCollectionBlocked && historical.ConsideredEvents.Count == 1,
        "Late-recorded restriction changes leaked into the historical cutoff.");
    PartyOpenItems.DerivedOpenItemRestrictionSnapshot current =
        PartyOpenItems.DerivedOpenItemRestrictionSnapshot.Create(
            tenantId, companyId, partyAccountId, dueLineId, new DateOnly(2026, 8, 31),
            recordedAt.AddMinutes(4), [dispute, disputeRelease, collectionBlock]);
    Equal(true, !current.IsDisputed && current.IsCollectionBlocked && current.ConsideredEvents.Count == 3,
        "Restriction release and independent collection block were not derived exactly.");

    PartyOpenItems.OpenItemRestrictionEvent wrongKindRelease = PartyOpenItems.OpenItemRestrictionEvent.Create(
        Guid.NewGuid(), tenantId, companyId, partyAccountId, dueLineId,
        PartyOpenItems.OpenItemRestrictionKind.CollectionBlock,
        PartyOpenItems.OpenItemRestrictionAction.Released,
        "invalid-release", new DateOnly(2026, 8, 4), recordedAt.AddMinutes(4), dispute.EventId);
    PartyDueSchedules.PartyOpenItemInvariantException releaseConflict =
        Throws<PartyDueSchedules.PartyOpenItemInvariantException>(() =>
            PartyOpenItems.DerivedOpenItemRestrictionSnapshot.Create(
                tenantId, companyId, partyAccountId, dueLineId, new DateOnly(2026, 8, 31),
                recordedAt.AddMinutes(5), [dispute, wrongKindRelease]));
    Equal("OPEN_ITEM_RESTRICTION_RELEASE_CONFLICT", releaseConflict.Code,
        "A release was allowed to target a different restriction kind.");

    var allowedScope = new ExecutionScope(
        tenantId,
        Guid.NewGuid(),
        [new CompanyAccess(
            companyId,
            [PartyRestrictionApplication.AuthorizedOpenItemRestrictionChange.RequiredPermission])]);
    PartyRestrictionApplication.AuthorizedOpenItemRestrictionChange authorized =
        PartyRestrictionApplication.AuthorizedOpenItemRestrictionChange.Create(allowedScope, dispute);
    Equal(dispute.EventId, authorized.RestrictionEvent.EventId,
        "Authorized restriction command changed the immutable event.");
    Throws<PartyRestrictionApplication.OpenItemRestrictionAuthorizationException>(() =>
        PartyRestrictionApplication.AuthorizedOpenItemRestrictionChange.Create(
            new ExecutionScope(tenantId, Guid.NewGuid(), [companyId]), dispute));
}

static void PartyAccountOpeningBoundariesAreEnforced()
{
    Guid tenantId = Guid.NewGuid();
    Guid companyId = Guid.NewGuid();
    Guid openingEventId = Guid.NewGuid();
    Guid partyAccountId = Guid.NewGuid();
    DateOnly effectiveDate = new(2026, 1, 1);
    DateTimeOffset recordedAt = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

    PartyOpenings.PartyAccountOpeningDraft valid = PartyOpenings.PartyAccountOpeningDraft.Create(
        tenantId,
        companyId,
        openingEventId,
        partyAccountId,
        PartyOpenings.PartyAccountOpeningEntrySide.Debit,
        1000.1234m,
        effectiveDate,
        recordedAt);
    Equal(PartyOpenings.PartyAccountOpeningDraft.SourceType, "party.account-opening",
        "Opening source type is not canonical.");
    Equal(PartyOpenings.PartyAccountOpeningDraft.PostingPurpose, "party.account-opening.post",
        "Opening posting purpose is not canonical.");
    Equal(1L, valid.SourceVersion, "Opening source must begin at immutable version one.");
    Equal(1000.1234m, valid.OriginalAmount, "Opening amount changed during validation.");

    ExpectPartyOpeningInvariant(
        "PARTY_OPENING_AMOUNT_INVALID",
        () => PartyOpenings.PartyAccountOpeningDraft.Create(
            tenantId, companyId, openingEventId, partyAccountId,
            PartyOpenings.PartyAccountOpeningEntrySide.Debit, 0m, effectiveDate, recordedAt));
    ExpectPartyOpeningInvariant(
        "PARTY_OPENING_AMOUNT_SCALE_INVALID",
        () => PartyOpenings.PartyAccountOpeningDraft.Create(
            tenantId, companyId, openingEventId, partyAccountId,
            PartyOpenings.PartyAccountOpeningEntrySide.Debit, 1.00001m, effectiveDate, recordedAt));
    ExpectPartyOpeningInvariant(
        "PARTY_OPENING_ENTRY_SIDE_INVALID",
        () => PartyOpenings.PartyAccountOpeningDraft.Create(
            tenantId, companyId, openingEventId, partyAccountId,
            (PartyOpenings.PartyAccountOpeningEntrySide)3, 1m, effectiveDate, recordedAt));
    ExpectPartyOpeningInvariant(
        "PARTY_OPENING_EFFECTIVE_DATE_REQUIRED",
        () => PartyOpenings.PartyAccountOpeningDraft.Create(
            tenantId, companyId, openingEventId, partyAccountId,
            PartyOpenings.PartyAccountOpeningEntrySide.Credit, 1m, default, recordedAt));
    ExpectPartyOpeningInvariant(
        "PARTY_OPENING_RECORDED_AT_NOT_UTC",
        () => PartyOpenings.PartyAccountOpeningDraft.Create(
            tenantId, companyId, openingEventId, partyAccountId,
            PartyOpenings.PartyAccountOpeningEntrySide.Credit, 1m, effectiveDate,
            recordedAt.ToOffset(TimeSpan.FromHours(3))));
}

static void PartyAccountOpeningPermissionAndScopeFailClosed()
{
    Guid tenantId = Guid.NewGuid();
    Guid companyId = Guid.NewGuid();
    Guid actorId = Guid.NewGuid();
    PartyOpenings.PartyAccountOpeningDraft draft = PartyOpenings.PartyAccountOpeningDraft.Create(
        tenantId,
        companyId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        PartyOpenings.PartyAccountOpeningEntrySide.Debit,
        25m,
        new DateOnly(2026, 1, 1),
        new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.Zero));

    var permittedScope = new ExecutionScope(
        tenantId,
        actorId,
        [new CompanyAccess(companyId, [PartyOpeningApplication.AuthorizedPartyAccountOpeningPreparation.RequiredPermission])]);
    PartyOpeningApplication.AuthorizedPartyAccountOpeningPreparation authorized =
        PartyOpeningApplication.AuthorizedPartyAccountOpeningPreparation.Create(permittedScope, draft);
    Equal(actorId, authorized.ActorId, "Opening preparation did not retain the authorized actor.");
    Equal(draft, authorized.Draft, "Opening preparation did not retain the validated source.");

    var missingPermission = new ExecutionScope(tenantId, actorId, [companyId]);
    PartyOpeningApplication.PartyAccountOpeningAuthorizationException permissionException =
        Throws<PartyOpeningApplication.PartyAccountOpeningAuthorizationException>(() =>
            PartyOpeningApplication.AuthorizedPartyAccountOpeningPreparation.Create(missingPermission, draft));
    Equal("PARTY_OPENING_PERMISSION_REQUIRED", permissionException.Code,
        "Opening preparation did not fail with the expected permission code.");

    var wrongCompany = new ExecutionScope(tenantId, actorId, [Guid.NewGuid()]);
    Throws<ExecutionScopeDeniedException>(() =>
        PartyOpeningApplication.AuthorizedPartyAccountOpeningPreparation.Create(wrongCompany, draft));
}

static void ExpectPartyOpeningInvariant(string expectedCode, Action action)
{
    PartyOpenings.PartyAccountOpeningInvariantException exception =
        Throws<PartyOpenings.PartyAccountOpeningInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected PartyAccount opening invariant code.");
}

static void PartyReportSourceProjectionCrossFootIsEnforced()
{
    Guid tenantId = Guid.NewGuid();
    Guid companyId = Guid.NewGuid();
    Guid partyAccountId = Guid.NewGuid();
    Guid controlAccountId = Guid.NewGuid();
    DateOnly asOf = new(2026, 8, 27);
    DateTimeOffset cutoff = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
    var impact = new PartyReportContracts.PartyReportImpactFact(
        Guid.NewGuid(), PartyReportContracts.PartyReportImpactKind.Allocation, Guid.NewGuid(),
        10m, asOf.AddDays(-1), cutoff.AddMinutes(-1), null);
    var item = new PartyReportContracts.PartyOpenItemSourceFact(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "sales.invoice", 50m, 40m,
        asOf.AddDays(-10), asOf.AddDays(10), cutoff.AddMinutes(-2),
        PartyReportContracts.PartyReportRestrictionEvidence.Unavailable, [impact]);
    PartyReportContracts.PartyReportSourceBatch source = PartyReportContracts.PartyReportSourceBatch.Create(
        tenantId, companyId, partyAccountId, controlAccountId,
        PartyReportContracts.PartyReportBalanceSide.Receivable, "GBP", asOf, cutoff, 0m,
        "event:1", "event:2", [item]);

    ReportingParty.ValidatedPartyStatement statement = ReportingApplication.PartyReportProjectionBuilder.BuildStatement(
        source, "party.account.detail", Guid.NewGuid(), 1, Guid.NewGuid(), cutoff.AddMinutes(1));
    Equal(40m, statement.ClosingExposure, "Source facts did not produce the exact statement closing exposure.");
    Equal(2, statement.Lines.Count, "Open item and impact were not normalized into statement events.");

    ReportingParty.CalendarDayAgingPolicySnapshot policy = ReportingParty.CalendarDayAgingPolicySnapshot.Create(
        tenantId, companyId, Guid.NewGuid(), 1,
        [ReportingParty.CalendarDayAgingBucket.Create("all", int.MinValue, int.MaxValue)]);
    ReportingControlAccounts.ReportingInvariantException unavailable = Throws<ReportingControlAccounts.ReportingInvariantException>(() =>
        ReportingApplication.PartyReportProjectionBuilder.BuildAging(
            source, policy, "party.account.detail", Guid.NewGuid(), 1, Guid.NewGuid(), cutoff.AddMinutes(1)));
    Equal("PARTY_RESTRICTION_EVIDENCE_UNAVAILABLE", unavailable.Code,
        "Unavailable restriction evidence was silently treated as clear.");
    PartyReportContracts.PartyReportSourceBatch clearSource = PartyReportContracts.PartyReportSourceBatch.Create(
        tenantId, companyId, partyAccountId, controlAccountId,
        PartyReportContracts.PartyReportBalanceSide.Receivable, "GBP", asOf, cutoff, 0m,
        "event:1", "event:2",
        [item with { RestrictionEvidence = PartyReportContracts.PartyReportRestrictionEvidence.Clear }]);
    ReportingParty.ValidatedPartyAgingReport aging = ReportingApplication.PartyReportProjectionBuilder.BuildAging(
        clearSource, policy, "party.account.detail", Guid.NewGuid(), 1, Guid.NewGuid(), cutoff.AddMinutes(1));
    Equal(40m, aging.TotalRemaining, "Aging total did not preserve the source remaining amount.");
    ReportingApplication.PartyReportProjectionBuilder.ProjectionPair pair =
        ReportingApplication.PartyReportProjectionBuilder.BuildPair(
            clearSource, policy, "party.account.detail", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            1, Guid.NewGuid(), cutoff.AddMinutes(1));
    Equal(pair.Statement.ReportSlice.ReportCode, pair.Aging.ReportSlice.ReportCode,
        "Statement and aging did not use the same report definition.");
    Equal(pair.Statement.ReportSlice.ProjectionGenerationId, pair.Aging.ReportSlice.ProjectionGenerationId,
        "Statement and aging did not use the same projection generation.");
    Equal(40m, pair.CrossFoot.Statement.ClosingExposure,
        "Projection pair did not cross-foot at construction time.");

    PartyReportContracts.PartyReportSourceBatch inconsistent = PartyReportContracts.PartyReportSourceBatch.Create(
        tenantId, companyId, partyAccountId, controlAccountId,
        PartyReportContracts.PartyReportBalanceSide.Receivable, "GBP", asOf, cutoff, 0m,
        "event:1", "event:2", [item with { RemainingAmount = 39m }]);
    ReportingControlAccounts.ReportingInvariantException mismatch = Throws<ReportingControlAccounts.ReportingInvariantException>(() =>
        ReportingApplication.PartyReportProjectionBuilder.BuildStatement(
            inconsistent, "party.account.detail", Guid.NewGuid(), 1, Guid.NewGuid(), cutoff.AddMinutes(1)));
    Equal("PARTY_SOURCE_CROSS_FOOT_MISMATCH", mismatch.Code,
        "Inconsistent source remaining amount passed statement cross-foot.");
}

static void PartyReportSourceContractBoundariesAreEnforced()
{
    Guid tenantId = Guid.NewGuid();
    Guid companyId = Guid.NewGuid();
    DateOnly asOf = new(2026, 8, 27);
    DateTimeOffset cutoff = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
    var impact = new PartyReportContracts.PartyReportImpactFact(
        Guid.NewGuid(), PartyReportContracts.PartyReportImpactKind.Allocation, Guid.NewGuid(),
        10m, asOf, cutoff.AddMinutes(-1), null);
    var item = new PartyReportContracts.PartyOpenItemSourceFact(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "sales.invoice", 50m, 40m, asOf.AddDays(-5), asOf,
        cutoff.AddMinutes(-2),
        PartyReportContracts.PartyReportRestrictionEvidence.Unavailable, [impact]);
    PartyReportContracts.PartyReportSourceBatch batch = PartyReportContracts.PartyReportSourceBatch.Create(
        tenantId, companyId, Guid.NewGuid(), Guid.NewGuid(),
        PartyReportContracts.PartyReportBalanceSide.Receivable, "GBP", asOf, cutoff, 0m,
        "event:1", "event:2", [item]);
    Equal(PartyReportContracts.PartyReportRestrictionEvidence.Unavailable,
        batch.OpenItems[0].RestrictionEvidence,
        "Unavailable dispute/block evidence was silently converted to clear.");
    PartyReportContracts.PartyReportSourceBatch replay = PartyReportContracts.PartyReportSourceBatch.Create(
        batch.TenantId, batch.CompanyId, batch.PartyAccountId, batch.ControlAccountId,
        batch.BalanceSide, batch.Currency, batch.EffectiveAsOf, batch.RecordedCutoff,
        batch.OpeningExposure, batch.SourceWatermarkFrom, batch.SourceWatermarkTo, [item]);
    Equal(batch.SourceChecksumSha256, replay.SourceChecksumSha256,
        "Equivalent source facts did not produce a deterministic checksum.");
    Equal(64, batch.SourceChecksumSha256.Length, "Source checksum is not SHA-256 length.");
    PartyReportContracts.PartyReportSourceBatch changedWatermark = PartyReportContracts.PartyReportSourceBatch.Create(
        batch.TenantId, batch.CompanyId, batch.PartyAccountId, batch.ControlAccountId,
        batch.BalanceSide, batch.Currency, batch.EffectiveAsOf, batch.RecordedCutoff,
        batch.OpeningExposure, batch.SourceWatermarkFrom, "event:3", [item]);
    Equal(false, batch.SourceChecksumSha256 == changedWatermark.SourceChecksumSha256,
        "Changed source lineage reused the same checksum.");

    Throws<ArgumentException>(() => PartyReportContracts.PartyReportSourceBatch.Create(
        tenantId, companyId, Guid.NewGuid(), Guid.NewGuid(),
        PartyReportContracts.PartyReportBalanceSide.Receivable, "GBP", asOf,
        cutoff.ToOffset(TimeSpan.FromHours(3)), 0m, "event:1", "event:2", [item]));
    var lateImpact = impact with { EventId = Guid.NewGuid(), RecordedAt = cutoff.AddSeconds(1) };
    var lateItem = item with { OpenItemId = Guid.NewGuid(), Impacts = [lateImpact] };
    Throws<ArgumentException>(() => PartyReportContracts.PartyReportSourceBatch.Create(
        tenantId, companyId, Guid.NewGuid(), Guid.NewGuid(),
        PartyReportContracts.PartyReportBalanceSide.Receivable, "GBP", asOf, cutoff, 0m,
        "event:1", "event:2", [lateItem]));
    var lateOrigin = item with { OpenItemId = Guid.NewGuid(), RecordedAt = cutoff.AddSeconds(1) };
    Throws<ArgumentException>(() => PartyReportContracts.PartyReportSourceBatch.Create(
        tenantId, companyId, Guid.NewGuid(), Guid.NewGuid(),
        PartyReportContracts.PartyReportBalanceSide.Receivable, "GBP", asOf, cutoff, 0m,
        "event:1", "event:2", [lateOrigin]));
}

static void ApprovalEvidenceBindsExactSubjectVersion()
{
    Guid tenantId = Guid.NewGuid();
    Guid companyId = Guid.NewGuid();
    Guid subjectId = Guid.NewGuid();
    ApprovalApplication.ApprovalCompletionEvidence evidence = CreateApprovalEvidence(
        tenantId, companyId, subjectId, 3, Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], 2);
    evidence.EnsureSubject(tenantId, companyId, "accounting.journal", subjectId, 3);
    ApprovalApplication.ApprovalEvidenceException mismatch = Throws<ApprovalApplication.ApprovalEvidenceException>(() =>
        evidence.EnsureSubject(tenantId, companyId, "accounting.journal", subjectId, 4));
    Equal("APPROVAL_SUBJECT_MISMATCH", mismatch.Code, "Changed subject version reused approval evidence.");
}

static void ApprovalEvidenceRejectsMakerCheckerConflict()
{
    Guid makerId = Guid.NewGuid();
    ApprovalApplication.ApprovalEvidenceException conflict = Throws<ApprovalApplication.ApprovalEvidenceException>(() =>
        CreateApprovalEvidence(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, makerId, [makerId], 1));
    Equal("APPROVAL_MAKER_CHECKER_CONFLICT", conflict.Code, "Maker approved the same critical subject.");
}

static void ApprovalEvidenceRequiresDistinctQuorum()
{
    Guid approverId = Guid.NewGuid();
    ApprovalApplication.ApprovalEvidenceException duplicate = Throws<ApprovalApplication.ApprovalEvidenceException>(() =>
        CreateApprovalEvidence(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), [approverId, approverId], 2));
    Equal("APPROVAL_APPROVER_NOT_DISTINCT", duplicate.Code, "One person filled multiple quorum votes.");

    ApprovalApplication.ApprovalEvidenceException insufficient = Throws<ApprovalApplication.ApprovalEvidenceException>(() =>
        CreateApprovalEvidence(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), [Guid.NewGuid()], 2));
    Equal("APPROVAL_QUORUM_NOT_MET", insufficient.Code, "Insufficient distinct approvals met quorum.");
}

static ApprovalApplication.ApprovalCompletionEvidence CreateApprovalEvidence(
    Guid tenantId,
    Guid companyId,
    Guid subjectId,
    long subjectVersion,
    Guid makerId,
    IEnumerable<Guid> approverIds,
    int requiredQuorum)
{
    DateTimeOffset decidedAt = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    ApprovalApplication.ApprovalDecisionEvidence[] decisions = approverIds
        .Select((approverId, index) => ApprovalApplication.ApprovalDecisionEvidence.Create(
            Guid.NewGuid(), approverId, decidedAt.AddMinutes(index)))
        .ToArray();
    return ApprovalApplication.ApprovalCompletionEvidence.Create(
        tenantId, companyId, Guid.NewGuid(), Guid.NewGuid(), "accounting.journal", subjectId,
        subjectVersion, makerId, requiredQuorum, decisions);
}

static void AuthorizedPostingCandidateRequiresCompleteEvidence()
{
    PostingCandidateFixture fixture = CreatePostingCandidateFixture();
    var scope = new ExecutionScope(
        fixture.Draft.TenantId,
        fixture.ActorId,
        [new CompanyAccess(fixture.Draft.CompanyId, [AccountingApplication.AuthorizedJournalPostingCandidate.RequiredPermission])]);

    AccountingApplication.AuthorizedJournalPostingCandidate candidate =
        AccountingApplication.AuthorizedJournalPostingCandidate.Create(
            scope,
            fixture.Draft,
            fixture.Accounts,
            fixture.Dimensions,
            fixture.Currencies,
            fixture.PeriodLocks);

    Equal(fixture.ActorId, candidate.ActorId, "Posting candidate lost the authorized actor identity.");
    if (!ReferenceEquals(fixture.Draft, candidate.JournalDraft))
    {
        throw new InvalidOperationException("Posting candidate replaced its validated journal draft.");
    }
}

static void PostingCandidatePermissionAndScopeFailClosed()
{
    PostingCandidateFixture fixture = CreatePostingCandidateFixture();
    var missingPermission = new ExecutionScope(
        fixture.Draft.TenantId,
        fixture.ActorId,
        [new CompanyAccess(fixture.Draft.CompanyId, ["accounting.journal.read"])]);
    AccountingApplication.JournalPostingCandidateException permissionException =
        Throws<AccountingApplication.JournalPostingCandidateException>(() =>
            AccountingApplication.AuthorizedJournalPostingCandidate.Create(
                missingPermission,
                fixture.Draft,
                fixture.Accounts,
                fixture.Dimensions,
                fixture.Currencies,
                fixture.PeriodLocks));
    Equal("JOURNAL_POST_PERMISSION_REQUIRED", permissionException.Code, "Unexpected permission failure code.");

    var wrongCompany = new ExecutionScope(
        fixture.Draft.TenantId,
        fixture.ActorId,
        [new CompanyAccess(Guid.NewGuid(), [AccountingApplication.AuthorizedJournalPostingCandidate.RequiredPermission])]);
    Throws<ExecutionScopeDeniedException>(() => AccountingApplication.AuthorizedJournalPostingCandidate.Create(
        wrongCompany,
        fixture.Draft,
        fixture.Accounts,
        fixture.Dimensions,
        fixture.Currencies,
        fixture.PeriodLocks));
}

static void PostingCandidatePeriodAndDraftEvidenceFailClosed()
{
    PostingCandidateFixture fixture = CreatePostingCandidateFixture();
    var scope = new ExecutionScope(
        fixture.Draft.TenantId,
        fixture.ActorId,
        [new CompanyAccess(fixture.Draft.CompanyId, [AccountingApplication.AuthorizedJournalPostingCandidate.RequiredPermission])]);
    PostingCandidateFixture other = CreatePostingCandidateFixture(
        tenantId: fixture.Draft.TenantId,
        companyId: fixture.Draft.CompanyId);
    AccountingApplication.JournalPostingCandidateException mismatch =
        Throws<AccountingApplication.JournalPostingCandidateException>(() =>
            AccountingApplication.AuthorizedJournalPostingCandidate.Create(
                scope,
                fixture.Draft,
                other.Accounts,
                fixture.Dimensions,
                fixture.Currencies,
                fixture.PeriodLocks));
    Equal("JOURNAL_VALIDATION_DRAFT_MISMATCH", mismatch.Code, "Unexpected mixed-evidence failure code.");

    Guid closedPeriodId = Guid.NewGuid();
    AccountingPeriods.ValidatedPeriodLockSet closedPeriod = AccountingPeriods.ValidatedPeriodLockSet.Create(
        fixture.Draft.TenantId,
        fixture.Draft.CompanyId,
        closedPeriodId,
        [
            AccountingPeriods.PeriodLockSnapshot.Create(
                fixture.Draft.TenantId,
                fixture.Draft.CompanyId,
                closedPeriodId,
                AccountingPeriods.PeriodLockScope.GeneralLedger,
                AccountingPeriods.PeriodCloseStage.SoftClose,
                1),
            AccountingPeriods.PeriodLockSnapshot.Create(
                fixture.Draft.TenantId,
                fixture.Draft.CompanyId,
                closedPeriodId,
                AccountingPeriods.PeriodLockScope.HardLegal,
                AccountingPeriods.PeriodCloseStage.Open,
                1),
        ]);
    AccountingPeriods.PeriodInvariantException periodException = Throws<AccountingPeriods.PeriodInvariantException>(() =>
        AccountingApplication.AuthorizedJournalPostingCandidate.Create(
            scope,
            fixture.Draft,
            fixture.Accounts,
            fixture.Dimensions,
            fixture.Currencies,
            closedPeriod));
    Equal("PERIOD_GL_LOCK_BLOCKS_POSTING", periodException.Code, "Unexpected closed-period failure code.");
}

var failures = new List<string>();
foreach (var check in checks)
{
    try
    {
        check.Run();
    }
    catch (Exception exception)
    {
        failures.Add($"{check.Name}: {exception.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("Domain unit checks failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine($"Domain unit checks passed: {checks.Length} checks.");
return 0;

static void BalancedJournalIsAccepted()
{
    var draft = CreateDefaultDraft(
        JournalLineDraft.Create(Guid.NewGuid(), Guid.NewGuid(), JournalAmount.Create(125.4321m, 0m)),
        JournalLineDraft.Create(Guid.NewGuid(), Guid.NewGuid(), JournalAmount.Create(0m, 125.4321m)));

    Equal(125.4321m, draft.TotalDebit, "Unexpected total debit.");
    Equal(125.4321m, draft.TotalCredit, "Unexpected total credit.");
    Equal(new DateOnly(2026, 8, 21), draft.EffectiveDate, "Effective date changed.");
    Equal(TimeSpan.Zero, draft.RecordedAt.Offset, "Recorded timestamp is not UTC.");
}

static void ImbalancedJournalIsRejected()
{
    ExpectInvariant(
        "JOURNAL_NOT_BALANCED",
        () => CreateDefaultDraft(
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(10.0001m, 0m)),
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 10.0000m))));
}

static void InvalidJournalAmountsAreRejected()
{
    ExpectInvariant("JOURNAL_AMOUNT_NEGATIVE", () => JournalAmount.Create(-0.0001m, 0m));
    ExpectInvariant("JOURNAL_AMOUNT_SIDE_INVALID", () => JournalAmount.Create(0m, 0m));
    ExpectInvariant("JOURNAL_AMOUNT_SIDE_INVALID", () => JournalAmount.Create(1m, 1m));
    ExpectInvariant(
        "JOURNAL_ACCOUNT_REQUIRED",
        () => JournalLineDraft.Create(Guid.Empty, null, JournalAmount.Create(1m, 0m)));
    ExpectInvariant(
        "JOURNAL_SOURCE_LINE_INVALID",
        () => JournalLineDraft.Create(Guid.NewGuid(), Guid.Empty, JournalAmount.Create(1m, 0m)));
}

static void MissingContextIsRejected()
{
    var validLines = new[]
    {
        JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(1m, 0m)),
        JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 1m)),
    };

    ExpectInvariant(
        "JOURNAL_TENANT_REQUIRED",
        () => CreateConfiguredDraft(validLines, tenantId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_COMPANY_REQUIRED",
        () => CreateConfiguredDraft(validLines, companyId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_SOURCE_REQUIRED",
        () => CreateConfiguredDraft(validLines, sourceEventId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_RULE_VERSION_REQUIRED",
        () => CreateConfiguredDraft(validLines, postingRuleVersionId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_SOURCE_TYPE_REQUIRED",
        () => CreateConfiguredDraft(validLines, sourceType: " "));
    ExpectInvariant(
        "JOURNAL_PURPOSE_REQUIRED",
        () => CreateConfiguredDraft(validLines, postingPurpose: string.Empty));
    ExpectInvariant("JOURNAL_LINES_INSUFFICIENT", () => CreateDefaultDraft(validLines[..1]));
}

static void NonUtcRecordedTimeIsRejected()
{
    var localOffsetTime = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.FromHours(3));
    ExpectInvariant(
        "JOURNAL_RECORDED_AT_NOT_UTC",
        () => CreateConfiguredDraft(
            [
                JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(1m, 0m)),
                JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 1m)),
            ],
            recordedAt: localOffsetTime));
}

static void InvalidCurrencyIsRejected()
{
    ExpectInvariant("JOURNAL_CURRENCY_INVALID", () => CurrencyCode.Create("try"));
    ExpectInvariant("JOURNAL_CURRENCY_INVALID", () => CurrencyCode.Create("EURO"));
    Equal("TRY", CurrencyCode.Create("TRY").Value, "Currency code changed.");
}

static void ValidatedJournalCopiesAndProtectsLines()
{
    var originalLines = new[]
    {
        JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(50m, 0m)),
        JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 50m)),
    };
    var firstAccountId = originalLines[0].AccountId;
    var draft = CreateDefaultDraft(originalLines);

    originalLines[0] = JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(999m, 0m));
    Equal(firstAccountId, draft.Lines[0].AccountId, "Validated journal retained a mutable input collection.");

    if (draft.Lines is IList<JournalLineDraft> list)
    {
        Throws<NotSupportedException>(() => list[0] = originalLines[0]);
    }
}

static void DecimalDistributionsRemainBalanced()
{
    for (var index = 1; index <= 100; index++)
    {
        var total = index * 0.0001m;
        var firstCredit = total / 4m;
        var secondCredit = total - firstCredit;
        var draft = CreateDefaultDraft(
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(total, 0m)),
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, firstCredit)),
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, secondCredit)));

        Equal(draft.TotalDebit, draft.TotalCredit, $"Distribution {index} is not balanced.");
    }
}

static void PostingIdentityIsCanonicalAndComparable()
{
    var tenantId = Guid.NewGuid();
    var companyId = Guid.NewGuid();
    var sourceEventId = Guid.NewGuid();
    var first = JournalPostingIdentity.Create(
        tenantId,
        companyId,
        " sales.invoice ",
        sourceEventId,
        " revenue ");
    var second = JournalPostingIdentity.Create(
        tenantId,
        companyId,
        "sales.invoice",
        sourceEventId,
        "revenue");

    Equal(first, second, "Canonical posting identities differ.");
    Equal("sales.invoice", first.SourceType, "Source type was not canonicalized.");
    Equal("revenue", first.PostingPurpose, "Posting purpose was not canonicalized.");
    ExpectInvariant(
        "JOURNAL_SOURCE_TYPE_REQUIRED",
        () => JournalPostingIdentity.Create(tenantId, companyId, " ", sourceEventId, "revenue"));
    ExpectInvariant(
        "JOURNAL_PURPOSE_REQUIRED",
        () => JournalPostingIdentity.Create(tenantId, companyId, "sales.invoice", sourceEventId, " "));
}

static void DuplicateJournalSourceIsRejected()
{
    var tenantId = Guid.NewGuid();
    var companyId = Guid.NewGuid();
    var sourceEventId = Guid.NewGuid();
    var first = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: companyId,
        sourceEventId: sourceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "revenue");
    var second = CreateConfiguredDraft(
        CreateBalancedLines(25m),
        tenantId: tenantId,
        companyId: companyId,
        sourceEventId: sourceEventId,
        postingRuleVersionId: Guid.NewGuid(),
        sourceType: " sales.invoice ",
        postingPurpose: " revenue ");

    ExpectInvariant(
        "JOURNAL_SOURCE_DUPLICATE",
        () => ValidatedJournalDraftSet.Create([first, second]));
}

static void PostingIdentityScopeAndDraftSetAreProtected()
{
    var tenantId = Guid.NewGuid();
    var sourceEventId = Guid.NewGuid();
    var firstCompanyId = Guid.NewGuid();
    var first = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: firstCompanyId,
        sourceEventId: sourceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "revenue");
    var second = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: Guid.NewGuid(),
        sourceEventId: sourceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "revenue");
    var third = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: firstCompanyId,
        sourceEventId: sourceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "receivable");
    var fourth = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: firstCompanyId,
        sourceEventId: Guid.NewGuid(),
        sourceType: "sales.invoice",
        postingPurpose: "revenue");
    var input = new[] { first, second, third, fourth };
    var draftSet = ValidatedJournalDraftSet.Create(input);

    input[0] = CreateDefaultDraft(CreateBalancedLines(99m));
    if (!ReferenceEquals(first, draftSet.Drafts[0]))
    {
        throw new InvalidOperationException("Validated draft set retained a mutable input collection.");
    }

    if (draftSet.Drafts is IList<ValidatedJournalDraft> list)
    {
        Throws<NotSupportedException>(() => list[0] = second);
    }

    ExpectInvariant("JOURNAL_DRAFT_SET_EMPTY", () => ValidatedJournalDraftSet.Create([]));
}

static void AllocationAmountBoundariesAreEnforced()
{
    var context = CreateAllocationTestContext();
    var openItem = CreateOpenItem(context, 10m);

    ExpectAllocationInvariant(
        "ALLOCATION_AMOUNT_INVALID",
        () => PartyAllocations.AllocationPlanLine.Create(openItem, 0m));
    ExpectAllocationInvariant(
        "ALLOCATION_OPEN_ITEM_EXCEEDED",
        () => PartyAllocations.AllocationPlanLine.Create(openItem, 10.0001m));
    ExpectAllocationInvariant(
        "ALLOCATION_PAYMENT_CAPACITY_INVALID",
        () => CreatePayment(context, 0m));
    ExpectAllocationInvariant(
        "ALLOCATION_OPEN_ITEM_CAPACITY_INVALID",
        () => CreateOpenItem(context, -0.0001m));
    ExpectAllocationInvariant(
        "ALLOCATION_CURRENCY_INVALID",
        () => PartyAllocations.AllocationCurrencyCode.Create("gbp"));

    var exactLine = PartyAllocations.AllocationPlanLine.Create(openItem, 10m);
    Equal(0m, exactLine.OpenItemRemainingAfter, "Exact allocation left an unexpected remainder.");
}

static void AllocationScopeAndCurrencyAreEnforced()
{
    var context = CreateAllocationTestContext();
    var payment = CreatePayment(context, 100m);

    ExpectAllocationInvariant(
        "ALLOCATION_TENANT_MISMATCH",
        () => CreateAllocationPlan(payment, CreateOpenItem(context with { TenantId = Guid.NewGuid() }, 10m), 10m));
    ExpectAllocationInvariant(
        "ALLOCATION_COMPANY_MISMATCH",
        () => CreateAllocationPlan(payment, CreateOpenItem(context with { CompanyId = Guid.NewGuid() }, 10m), 10m));
    ExpectAllocationInvariant(
        "ALLOCATION_PARTY_ACCOUNT_MISMATCH",
        () => CreateAllocationPlan(payment, CreateOpenItem(context with { PartyAccountId = Guid.NewGuid() }, 10m), 10m));
    ExpectAllocationInvariant(
        "ALLOCATION_CROSS_CURRENCY_REQUIRES_RATE_SNAPSHOT",
        () => CreateAllocationPlan(
            payment,
            CreateOpenItem(context with { Currency = PartyAllocations.AllocationCurrencyCode.Create("EUR") }, 10m),
            10m));
}

static void MultiItemAllocationCapacityIsEnforced()
{
    var context = CreateAllocationTestContext();
    var payment = CreatePayment(context, 100m);
    var firstItem = CreateOpenItem(context, 70m);
    var secondItem = CreateOpenItem(context, 80m);
    var plan = PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
        payment,
        [
            PartyAllocations.AllocationPlanLine.Create(firstItem, 60m),
            PartyAllocations.AllocationPlanLine.Create(secondItem, 40m),
        ]);

    Equal(100m, plan.TotalAllocated, "Unexpected total allocation.");
    Equal(0m, plan.PaymentRemainingAfter, "Unexpected payment remainder.");
    Equal(10m, plan.Lines[0].OpenItemRemainingAfter, "Unexpected first open-item remainder.");
    Equal(40m, plan.Lines[1].OpenItemRemainingAfter, "Unexpected second open-item remainder.");

    ExpectAllocationInvariant(
        "ALLOCATION_PAYMENT_EXCEEDED",
        () => PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
            payment,
            [
                PartyAllocations.AllocationPlanLine.Create(firstItem, 70m),
                PartyAllocations.AllocationPlanLine.Create(secondItem, 30.0001m),
            ]));
    ExpectAllocationInvariant(
        "ALLOCATION_OPEN_ITEM_DUPLICATE",
        () => PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
            payment,
            [
                PartyAllocations.AllocationPlanLine.Create(firstItem, 30m),
                PartyAllocations.AllocationPlanLine.Create(firstItem, 20m),
            ]));
    ExpectAllocationInvariant(
        "ALLOCATION_PAYMENT_EXCEEDED",
        () => PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
            CreatePayment(context, decimal.MaxValue),
            [
                PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, decimal.MaxValue), decimal.MaxValue),
                PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, 1m), 1m),
            ]));
}

static void AllocationOrderAndImmutabilityAreProtected()
{
    var context = CreateAllocationTestContext();
    var payment = CreatePayment(context, 100m);
    var firstLine = PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, 70m), 60m);
    var secondLine = PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, 80m), 30m);
    var input = new[] { firstLine, secondLine };
    var firstPlan = PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(payment, input);
    var reversedPlan = PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(payment, input.Reverse());

    Equal(firstPlan.TotalAllocated, reversedPlan.TotalAllocated, "Line order changed the total allocation.");
    Equal(firstPlan.PaymentRemainingAfter, reversedPlan.PaymentRemainingAfter, "Line order changed the payment remainder.");

    input[0] = PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, 1m), 1m);
    if (!ReferenceEquals(firstLine, firstPlan.Lines[0]))
    {
        throw new InvalidOperationException("Validated allocation retained a mutable input collection.");
    }

    if (firstPlan.Lines is IList<PartyAllocations.AllocationPlanLine> list)
    {
        Throws<NotSupportedException>(() => list[0] = input[0]);
    }
}

static void PeriodCloseProgressionIsEnforced()
{
    var softClose = AccountingPeriods.PeriodCloseTransition.Create(
        AccountingPeriods.PeriodCloseStage.Open,
        AccountingPeriods.PeriodCloseStage.SoftClose);
    var review = AccountingPeriods.PeriodCloseTransition.Create(
        AccountingPeriods.PeriodCloseStage.SoftClose,
        AccountingPeriods.PeriodCloseStage.Review);
    var hardClose = AccountingPeriods.PeriodCloseTransition.Create(
        AccountingPeriods.PeriodCloseStage.Review,
        AccountingPeriods.PeriodCloseStage.HardClose);

    Equal(AccountingPeriods.PeriodCloseStage.SoftClose, softClose.To, "Unexpected soft-close target.");
    Equal(AccountingPeriods.PeriodCloseStage.Review, review.To, "Unexpected review target.");
    Equal(AccountingPeriods.PeriodCloseStage.HardClose, hardClose.To, "Unexpected hard-close target.");

    ExpectPeriodInvariant(
        "PERIOD_TRANSITION_NO_CHANGE",
        () => AccountingPeriods.PeriodCloseTransition.Create(
            AccountingPeriods.PeriodCloseStage.Open,
            AccountingPeriods.PeriodCloseStage.Open));
    ExpectPeriodInvariant(
        "PERIOD_TRANSITION_INVALID",
        () => AccountingPeriods.PeriodCloseTransition.Create(
            AccountingPeriods.PeriodCloseStage.Open,
            AccountingPeriods.PeriodCloseStage.Review));
    ExpectPeriodInvariant(
        "PERIOD_REOPEN_REQUIRES_APPROVED_WORKFLOW",
        () => AccountingPeriods.PeriodCloseTransition.Create(
            AccountingPeriods.PeriodCloseStage.HardClose,
            AccountingPeriods.PeriodCloseStage.Review));

    var context = CreatePeriodTestContext();
    ExpectPeriodInvariant(
        "PERIOD_CLOSE_STAGE_INVALID",
        () => CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.GeneralLedger, (AccountingPeriods.PeriodCloseStage)99));
    ExpectPeriodInvariant(
        "PERIOD_LOCK_VERSION_INVALID",
        () => CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.GeneralLedger, version: 0));
    ExpectPeriodInvariant(
        "PERIOD_LOCK_SCOPE_INVALID",
        () => CreatePeriodLock(context, (AccountingPeriods.PeriodLockScope)99));
    ExpectPeriodInvariant(
        "PERIOD_TENANT_REQUIRED",
        () => CreatePeriodLock(context with { TenantId = Guid.Empty }, AccountingPeriods.PeriodLockScope.GeneralLedger));
    ExpectPeriodInvariant(
        "PERIOD_COMPANY_REQUIRED",
        () => CreatePeriodLock(context with { CompanyId = Guid.Empty }, AccountingPeriods.PeriodLockScope.GeneralLedger));
    ExpectPeriodInvariant(
        "PERIOD_ID_REQUIRED",
        () => CreatePeriodLock(context with { PeriodId = Guid.Empty }, AccountingPeriods.PeriodLockScope.GeneralLedger));
}

static void PeriodLockScopesAreIsolated()
{
    var context = CreatePeriodTestContext();
    var glLock = CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.GeneralLedger);
    var hardLegalLock = CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.HardLegal);
    var taxLock = CreatePeriodLock(
        context,
        AccountingPeriods.PeriodLockScope.Tax,
        AccountingPeriods.PeriodCloseStage.HardClose);
    var input = new[] { glLock, hardLegalLock, taxLock };
    var lockSet = CreatePeriodLockSet(context, input);

    input[0] = CreatePeriodLock(
        context,
        AccountingPeriods.PeriodLockScope.GeneralLedger,
        AccountingPeriods.PeriodCloseStage.HardClose,
        2);
    if (!ReferenceEquals(glLock, lockSet.Locks[0]))
    {
        throw new InvalidOperationException("Validated period lock set retained a mutable input collection.");
    }

    if (lockSet.Locks is IList<AccountingPeriods.PeriodLockSnapshot> list)
    {
        Throws<NotSupportedException>(() => list[0] = input[0]);
    }

    Equal(
        AccountingPeriods.PeriodCloseStage.HardClose,
        lockSet.GetRequired(AccountingPeriods.PeriodLockScope.Tax).Stage,
        "Tax scope changed unexpectedly.");
    Equal(
        AccountingPeriods.PeriodCloseStage.Open,
        lockSet.GetRequired(AccountingPeriods.PeriodLockScope.GeneralLedger).Stage,
        "GL scope changed with an unrelated scope.");

    ExpectPeriodInvariant(
        "PERIOD_LOCK_SCOPE_DUPLICATE",
        () => CreatePeriodLockSet(context, [glLock, CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.GeneralLedger)]));
    ExpectPeriodInvariant(
        "PERIOD_LOCKS_REQUIRED",
        () => CreatePeriodLockSet(context, []));
    ExpectPeriodInvariant(
        "PERIOD_TENANT_MISMATCH",
        () => CreatePeriodLockSet(
            context,
            [CreatePeriodLock(context with { TenantId = Guid.NewGuid() }, AccountingPeriods.PeriodLockScope.GeneralLedger)]));
    ExpectPeriodInvariant(
        "PERIOD_COMPANY_MISMATCH",
        () => CreatePeriodLockSet(
            context,
            [CreatePeriodLock(context with { CompanyId = Guid.NewGuid() }, AccountingPeriods.PeriodLockScope.GeneralLedger)]));
    ExpectPeriodInvariant(
        "PERIOD_ID_MISMATCH",
        () => CreatePeriodLockSet(
            context,
            [CreatePeriodLock(context with { PeriodId = Guid.NewGuid() }, AccountingPeriods.PeriodLockScope.GeneralLedger)]));
}

static void StandardPostingPeriodGateFailsClosed()
{
    var context = CreatePeriodTestContext();
    var openGlLock = CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.GeneralLedger);
    var openHardLegalLock = CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.HardLegal);
    var closedTaxLock = CreatePeriodLock(
        context,
        AccountingPeriods.PeriodLockScope.Tax,
        AccountingPeriods.PeriodCloseStage.HardClose);
    CreatePeriodLockSet(context, [openGlLock, openHardLegalLock, closedTaxLock]).EnsureStandardPostingAllowed();

    ExpectPeriodInvariant(
        "PERIOD_GL_LOCK_BLOCKS_POSTING",
        () => CreatePeriodLockSet(
            context,
            [
                CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.GeneralLedger, AccountingPeriods.PeriodCloseStage.SoftClose),
                openHardLegalLock,
            ]).EnsureStandardPostingAllowed());
    ExpectPeriodInvariant(
        "PERIOD_HARD_LOCK_BLOCKS_POSTING",
        () => CreatePeriodLockSet(
            context,
            [
                openGlLock,
                CreatePeriodLock(context, AccountingPeriods.PeriodLockScope.HardLegal, AccountingPeriods.PeriodCloseStage.HardClose),
            ]).EnsureStandardPostingAllowed());
    ExpectPeriodInvariant(
        "PERIOD_LOCK_SCOPE_MISSING",
        () => CreatePeriodLockSet(context, [openGlLock]).EnsureStandardPostingAllowed());
    ExpectPeriodInvariant(
        "PERIOD_LOCK_SCOPE_MISSING",
        () => CreatePeriodLockSet(context, [openHardLegalLock]).EnsureStandardPostingAllowed());
}

static void AccountSnapshotBoundariesAreEnforced()
{
    var context = CreateAccountTestContext();

    ExpectAccountInvariant(
        "ACCOUNT_TENANT_REQUIRED",
        () => CreateAccountSnapshot(context with { TenantId = Guid.Empty }, context.DebitAccountId));
    ExpectAccountInvariant(
        "ACCOUNT_COMPANY_REQUIRED",
        () => CreateAccountSnapshot(context with { CompanyId = Guid.Empty }, context.DebitAccountId));
    ExpectAccountInvariant(
        "ACCOUNT_ID_REQUIRED",
        () => CreateAccountSnapshot(context, Guid.Empty));
    ExpectAccountInvariant(
        "ACCOUNT_CHART_VERSION_REQUIRED",
        () => CreateAccountSnapshot(context with { ChartVersionId = Guid.Empty }, context.DebitAccountId));
    ExpectAccountInvariant(
        "ACCOUNT_KIND_INVALID",
        () => CreateAccountSnapshot(context, context.DebitAccountId, (AccountingAccounts.AccountKind)99));
    ExpectAccountInvariant(
        "ACCOUNT_SNAPSHOT_VERSION_INVALID",
        () => CreateAccountSnapshot(context, context.DebitAccountId, version: 0));
}

static void JournalAccountScopeAndVersionAreEnforced()
{
    var context = CreateAccountTestContext();
    var journal = CreateJournalForAccounts(context);
    var debitAccount = CreateAccountSnapshot(context, context.DebitAccountId);
    var creditAccount = CreateAccountSnapshot(context, context.CreditAccountId);

    ExpectAccountInvariant(
        "JOURNAL_ACCOUNT_TENANT_MISMATCH",
        () => ValidateJournalAccounts(
            journal,
            context,
            [CreateAccountSnapshot(context with { TenantId = Guid.NewGuid() }, context.DebitAccountId), creditAccount]));
    ExpectAccountInvariant(
        "JOURNAL_ACCOUNT_COMPANY_MISMATCH",
        () => ValidateJournalAccounts(
            journal,
            context,
            [CreateAccountSnapshot(context with { CompanyId = Guid.NewGuid() }, context.DebitAccountId), creditAccount]));
    ExpectAccountInvariant(
        "JOURNAL_ACCOUNT_CHART_VERSION_MISMATCH",
        () => ValidateJournalAccounts(
            journal,
            context,
            [CreateAccountSnapshot(context with { ChartVersionId = Guid.NewGuid() }, context.DebitAccountId), creditAccount]));
    ExpectAccountInvariant(
        "ACCOUNT_SNAPSHOT_DUPLICATE",
        () => ValidateJournalAccounts(journal, context, [debitAccount, debitAccount, creditAccount]));
    ExpectAccountInvariant(
        "ACCOUNT_CHART_VERSION_REQUIRED",
        () => AccountingAccounts.ValidatedJournalAccountSet.Create(journal, Guid.Empty, [debitAccount, creditAccount]));
}

static void JournalAccountsMustBeCompleteAndPostable()
{
    var context = CreateAccountTestContext();
    var journal = CreateJournalForAccounts(context);
    var debitAccount = CreateAccountSnapshot(context, context.DebitAccountId);
    var creditAccount = CreateAccountSnapshot(context, context.CreditAccountId);
    var input = new[] { creditAccount, debitAccount };
    var validated = ValidateJournalAccounts(journal, context, input);

    Equal(2, validated.Accounts.Count, "Unexpected validated account count.");
    Equal(context.DebitAccountId, validated.Accounts[0].AccountId, "Journal account order was not deterministic.");
    Equal(context.CreditAccountId, validated.Accounts[1].AccountId, "Journal account order was not deterministic.");

    input[0] = CreateAccountSnapshot(context, Guid.NewGuid());
    if (!ReferenceEquals(creditAccount, validated.Accounts[1]))
    {
        throw new InvalidOperationException("Validated journal account set retained a mutable input collection.");
    }

    if (validated.Accounts is IList<AccountingAccounts.AccountPostingSnapshot> list)
    {
        Throws<NotSupportedException>(() => list[0] = input[0]);
    }

    ExpectAccountInvariant(
        "ACCOUNT_SNAPSHOTS_REQUIRED",
        () => ValidateJournalAccounts(journal, context, []));
    ExpectAccountInvariant(
        "JOURNAL_ACCOUNT_SNAPSHOT_MISSING",
        () => ValidateJournalAccounts(journal, context, [debitAccount]));
    ExpectAccountInvariant(
        "JOURNAL_ACCOUNT_INACTIVE",
        () => ValidateJournalAccounts(
            journal,
            context,
            [CreateAccountSnapshot(context, context.DebitAccountId, isActive: false), creditAccount]));
    ExpectAccountInvariant(
        "JOURNAL_ACCOUNT_NOT_POSTABLE",
        () => ValidateJournalAccounts(
            journal,
            context,
            [
                CreateAccountSnapshot(context, context.DebitAccountId, AccountingAccounts.AccountKind.Summary),
                creditAccount,
            ]));
}

static void PaymentRateBoundariesAreEnforced()
{
    var context = CreateTreasuryPaymentTestContext();

    ExpectPaymentInvariant("PAYMENT_CURRENCY_INVALID", () => TreasuryPayments.TreasuryCurrencyCode.Create("try"));
    ExpectPaymentInvariant("PAYMENT_RATE_TENANT_REQUIRED", () => CreatePaymentRate(context with { TenantId = Guid.Empty }));
    ExpectPaymentInvariant("PAYMENT_RATE_COMPANY_REQUIRED", () => CreatePaymentRate(context with { CompanyId = Guid.Empty }));
    ExpectPaymentInvariant(
        "PAYMENT_RATE_SNAPSHOT_REQUIRED",
        () => CreatePaymentRate(context with { RateSnapshotId = Guid.Empty }));
    ExpectPaymentInvariant("PAYMENT_RATE_VERSION_INVALID", () => CreatePaymentRate(context, version: 0));
    ExpectPaymentInvariant("PAYMENT_RATE_TYPE_REQUIRED", () => CreatePaymentRate(context, rateType: " "));
    ExpectPaymentInvariant("PAYMENT_RATE_SOURCE_REQUIRED", () => CreatePaymentRate(context, source: string.Empty));
    ExpectPaymentInvariant("PAYMENT_RATE_NUMERATOR_INVALID", () => CreatePaymentRate(context, numerator: 0m));
    ExpectPaymentInvariant("PAYMENT_RATE_DENOMINATOR_INVALID", () => CreatePaymentRate(context, denominator: -1m));
    ExpectPaymentInvariant(
        "PAYMENT_CROSS_CURRENCY_NOT_SUPPORTED",
        () => CreatePaymentRate(context, functionalCurrency: TreasuryPayments.TreasuryCurrencyCode.Create("EUR")));
    ExpectPaymentInvariant(
        "PAYMENT_CROSS_CURRENCY_NOT_SUPPORTED",
        () => CreatePaymentRate(context, numerator: 2m, denominator: 1m));

    var rate = CreatePaymentRate(context, rateType: " identity ", source: " technical-fixture ", numerator: 100m, denominator: 100m);
    Equal("identity", rate.RateType, "Payment-rate type was not canonicalized.");
    Equal("technical-fixture", rate.Source, "Payment-rate source was not canonicalized.");
    Equal(context.Currency, rate.TransactionCurrency, "Payment transaction currency changed.");
    Equal(context.Currency, rate.FunctionalCurrency, "Payment functional currency changed.");
}

static void PaymentEconomicEventBoundariesAreEnforced()
{
    var context = CreateTreasuryPaymentTestContext();

    ExpectPaymentInvariant("PAYMENT_ID_REQUIRED", () => CreatePaymentDraft(context, paymentId: Guid.Empty));
    ExpectPaymentInvariant(
        "PAYMENT_PARTY_ACCOUNT_REQUIRED",
        () => CreatePaymentDraft(context with { PartyAccountId = Guid.Empty }));
    ExpectPaymentInvariant(
        "PAYMENT_TREASURY_ACCOUNT_REQUIRED",
        () => CreatePaymentDraft(context with { TreasuryAccountId = Guid.Empty }));
    ExpectPaymentInvariant(
        "PAYMENT_DIRECTION_INVALID",
        () => CreatePaymentDraft(context, direction: (TreasuryPayments.PaymentDirection)99));
    ExpectPaymentInvariant("PAYMENT_AMOUNT_INVALID", () => CreatePaymentDraft(context, transactionAmount: 0m, functionalAmount: 0m));
    ExpectPaymentInvariant(
        "PAYMENT_FUNCTIONAL_AMOUNT_MISMATCH",
        () => CreatePaymentDraft(context, transactionAmount: 10m, functionalAmount: 9.9999m));
    ExpectPaymentInvariant(
        "PAYMENT_RECORDED_AT_NOT_UTC",
        () => CreatePaymentDraft(
            context,
            recordedAt: new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(3))));
    ExpectPaymentInvariant(
        "PAYMENT_TENANT_REQUIRED",
        () => CreatePaymentDraft(
            context with { TenantId = Guid.Empty },
            rateSnapshot: CreatePaymentRate(context)));
    ExpectPaymentInvariant(
        "PAYMENT_COMPANY_REQUIRED",
        () => CreatePaymentDraft(
            context with { CompanyId = Guid.Empty },
            rateSnapshot: CreatePaymentRate(context)));
    ExpectPaymentInvariant("PAYMENT_SOURCE_REQUIRED", () => CreatePaymentDraft(context, sourceEventId: Guid.Empty));
    ExpectPaymentInvariant("PAYMENT_SOURCE_TYPE_REQUIRED", () => CreatePaymentDraft(context, sourceType: " "));
    ExpectPaymentInvariant("PAYMENT_PURPOSE_REQUIRED", () => CreatePaymentDraft(context, postingPurpose: string.Empty));
    ExpectPaymentInvariant(
        "PAYMENT_RATE_TENANT_MISMATCH",
        () => CreatePaymentDraft(
            context,
            rateSnapshot: CreatePaymentRate(context with { TenantId = Guid.NewGuid() })));
    ExpectPaymentInvariant(
        "PAYMENT_RATE_COMPANY_MISMATCH",
        () => CreatePaymentDraft(
            context,
            rateSnapshot: CreatePaymentRate(context with { CompanyId = Guid.NewGuid() })));

    var payment = CreatePaymentDraft(
        context,
        direction: TreasuryPayments.PaymentDirection.Incoming,
        transactionAmount: 125.4321m,
        functionalAmount: 125.4321m);
    Equal(context.TenantId, payment.TenantId, "Payment tenant changed.");
    Equal(context.CompanyId, payment.CompanyId, "Payment company changed.");
    Equal(context.PartyAccountId, payment.PartyAccountId, "Payment party account changed.");
    Equal(context.TreasuryAccountId, payment.TreasuryAccountId, "Payment treasury account changed.");
    Equal(125.4321m, payment.TransactionAmount, "Payment transaction amount changed.");
    Equal(125.4321m, payment.FunctionalAmount, "Payment functional amount changed.");
    Equal(TimeSpan.Zero, payment.RecordedAt.Offset, "Payment timestamp is not UTC.");
}

static void PaymentSourceUniquenessAndImmutabilityAreEnforced()
{
    var context = CreateTreasuryPaymentTestContext();
    var sourceEventId = Guid.NewGuid();
    var first = CreatePaymentDraft(
        context,
        sourceEventId: sourceEventId,
        sourceType: " sales.receipt ",
        postingPurpose: " party-collection ");
    var duplicateSource = CreatePaymentDraft(
        context,
        sourceEventId: sourceEventId,
        sourceType: "sales.receipt",
        postingPurpose: "party-collection");

    Equal("sales.receipt", first.SourceIdentity.SourceType, "Payment source type was not canonicalized.");
    Equal("party-collection", first.SourceIdentity.PostingPurpose, "Payment purpose was not canonicalized.");
    ExpectPaymentInvariant(
        "PAYMENT_SOURCE_DUPLICATE",
        () => TreasuryPayments.ValidatedPaymentEconomicEventDraftSet.Create([first, duplicateSource]));

    var duplicatePaymentId = CreatePaymentDraft(
        context with { CompanyId = Guid.NewGuid() },
        paymentId: first.PaymentId);
    ExpectPaymentInvariant(
        "PAYMENT_ID_DUPLICATE",
        () => TreasuryPayments.ValidatedPaymentEconomicEventDraftSet.Create([first, duplicatePaymentId]));
    ExpectPaymentInvariant(
        "PAYMENT_DRAFTS_REQUIRED",
        () => TreasuryPayments.ValidatedPaymentEconomicEventDraftSet.Create([]));
    ExpectPaymentInvariant(
        "PAYMENT_DRAFTS_REQUIRED",
        () => TreasuryPayments.ValidatedPaymentEconomicEventDraftSet.Create(null));
    ExpectPaymentInvariant(
        "PAYMENT_DRAFT_REQUIRED",
        () => TreasuryPayments.ValidatedPaymentEconomicEventDraftSet.Create([first, null!]));

    var otherTenant = CreateTreasuryPaymentTestContext();
    var samePaymentIdOtherTenant = CreatePaymentDraft(otherTenant, paymentId: first.PaymentId);
    var third = CreatePaymentDraft(context);
    var input = new[] { samePaymentIdOtherTenant, third, first };
    var set = TreasuryPayments.ValidatedPaymentEconomicEventDraftSet.Create(input);

    Equal(3, set.Drafts.Count, "Tenant-scoped payment IDs collided.");
    var expectedFirstTenant = input.Select(item => item.TenantId).Order().First();
    Equal(expectedFirstTenant, set.Drafts[0].TenantId, "Payment draft ordering is not deterministic.");
    input[0] = duplicateSource;
    if (!set.Drafts.Contains(samePaymentIdOtherTenant))
    {
        throw new InvalidOperationException("Validated payment set retained a mutable input collection.");
    }

    if (set.Drafts is IList<TreasuryPayments.ValidatedPaymentEconomicEventDraft> list)
    {
        Throws<NotSupportedException>(() => list[0] = duplicateSource);
    }
}

static void StatementLineBoundariesAreEnforced()
{
    var context = CreateStatementTestContext();

    ExpectStatementInvariant(
        "STATEMENT_TENANT_REQUIRED",
        () => CreateStatementIdentity(context with { TenantId = Guid.Empty }));
    ExpectStatementInvariant(
        "STATEMENT_COMPANY_REQUIRED",
        () => CreateStatementIdentity(context with { CompanyId = Guid.Empty }));
    ExpectStatementInvariant(
        "STATEMENT_TREASURY_ACCOUNT_REQUIRED",
        () => CreateStatementIdentity(context with { TreasuryAccountId = Guid.Empty }));
    ExpectStatementInvariant(
        "STATEMENT_SOURCE_SYSTEM_REQUIRED",
        () => CreateStatementIdentity(context, sourceSystem: " "));
    ExpectStatementInvariant(
        "STATEMENT_IDENTITY_KIND_REQUIRED",
        () => CreateStatementIdentity(context, identityKind: string.Empty));
    ExpectStatementInvariant(
        "STATEMENT_EXTERNAL_KEY_REQUIRED",
        () => CreateStatementIdentity(context, externalKey: " "));
    ExpectStatementInvariant("STATEMENT_LINE_REQUIRED", () => CreateStatementLine(context, statementLineId: Guid.Empty));
    ExpectStatementInvariant("STATEMENT_IMPORT_REQUIRED", () => CreateStatementLine(context with { StatementImportId = Guid.Empty }));
    ExpectStatementInvariant("STATEMENT_AMOUNT_INVALID", () => CreateStatementLine(context, signedAmount: 0m));
    ExpectStatementInvariant("STATEMENT_AMOUNT_INVALID", () => CreateStatementLine(context, signedAmount: decimal.MinValue));
    ExpectStatementInvariant("STATEMENT_BOOKING_DATE_REQUIRED", () => CreateStatementLine(context, bookingDate: default(DateOnly)));
    ExpectStatementInvariant("STATEMENT_VALUE_DATE_REQUIRED", () => CreateStatementLine(context, valueDate: default(DateOnly)));
    ExpectStatementInvariant(
        "STATEMENT_RECORDED_AT_NOT_UTC",
        () => CreateStatementLine(
            context,
            recordedAt: new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(3))));
    ExpectStatementInvariant("STATEMENT_RAW_HASH_INVALID", () => CreateStatementLine(context, rawObjectSha256: new string('A', 64)));
    ExpectStatementInvariant("STATEMENT_PARSER_VERSION_INVALID", () => CreateStatementLine(context, parserVersion: 0));

    var incoming = CreateStatementLine(context, signedAmount: 125.4321m);
    var outgoing = CreateStatementLine(context, signedAmount: -75.1234m);
    Equal(125.4321m, incoming.MatchCapacity, "Incoming statement match capacity changed.");
    Equal(75.1234m, outgoing.MatchCapacity, "Outgoing statement match capacity is not absolute.");
    Equal(TimeSpan.Zero, outgoing.RecordedAt.Offset, "Statement recorded timestamp is not UTC.");
}

static void StatementLineUniquenessAndImmutabilityAreEnforced()
{
    var context = CreateStatementTestContext();
    var first = CreateStatementLine(
        context,
        externalIdentity: CreateStatementIdentity(
            context,
            sourceSystem: " bank-profile-v1 ",
            identityKind: " bank-reference ",
            externalKey: " REF-001 "));
    var duplicateExternalIdentity = CreateStatementLine(
        context with { StatementImportId = Guid.NewGuid() },
        externalIdentity: CreateStatementIdentity(
            context,
            sourceSystem: "bank-profile-v1",
            identityKind: "bank-reference",
            externalKey: "REF-001"));

    Equal("bank-profile-v1", first.ExternalIdentity.SourceSystem, "Statement source system was not canonicalized.");
    Equal("bank-reference", first.ExternalIdentity.IdentityKind, "Statement identity kind was not canonicalized.");
    Equal("REF-001", first.ExternalIdentity.ExternalKey, "Statement external key was not canonicalized.");
    ExpectStatementInvariant(
        "STATEMENT_EXTERNAL_IDENTITY_DUPLICATE",
        () => TreasuryStatements.ValidatedStatementLineDraftSet.Create([first, duplicateExternalIdentity]));

    var duplicateLineId = CreateStatementLine(
        context with { CompanyId = Guid.NewGuid() },
        statementLineId: first.StatementLineId);
    ExpectStatementInvariant(
        "STATEMENT_LINE_DUPLICATE",
        () => TreasuryStatements.ValidatedStatementLineDraftSet.Create([first, duplicateLineId]));
    ExpectStatementInvariant("STATEMENT_LINES_REQUIRED", () => TreasuryStatements.ValidatedStatementLineDraftSet.Create([]));
    ExpectStatementInvariant("STATEMENT_LINES_REQUIRED", () => TreasuryStatements.ValidatedStatementLineDraftSet.Create(null));
    ExpectStatementInvariant(
        "STATEMENT_LINE_REQUIRED",
        () => TreasuryStatements.ValidatedStatementLineDraftSet.Create([first, null!]));

    var otherTenantContext = CreateStatementTestContext();
    var sameLineIdOtherTenant = CreateStatementLine(otherTenantContext, statementLineId: first.StatementLineId);
    var third = CreateStatementLine(context);
    var input = new[] { sameLineIdOtherTenant, third, first };
    var set = TreasuryStatements.ValidatedStatementLineDraftSet.Create(input);
    Equal(3, set.Lines.Count, "Tenant-scoped statement-line IDs collided.");
    Equal(input.Select(line => line.TenantId).Order().First(), set.Lines[0].TenantId, "Statement-line ordering is not deterministic.");
    input[0] = duplicateExternalIdentity;
    if (!set.Lines.Contains(sameLineIdOtherTenant))
    {
        throw new InvalidOperationException("Validated statement-line set retained a mutable input collection.");
    }

    if (set.Lines is IList<TreasuryStatements.ValidatedStatementLineDraft> list)
    {
        Throws<NotSupportedException>(() => list[0] = duplicateExternalIdentity);
    }
}

static void ReconciliationProposalBoundariesAreEnforced()
{
    var context = CreateStatementTestContext();
    var statementA = CreateStatementLine(context, signedAmount: -100m);
    var statementB = CreateStatementLine(context, signedAmount: -50m);
    var movementA = CreateMovementCapacity(context, usableAmount: 120m);
    var movementB = CreateMovementCapacity(context, usableAmount: 30m);

    ExpectReconciliationInvariant(
        "RECONCILIATION_TENANT_REQUIRED",
        () => CreateMovementCapacity(context with { TenantId = Guid.Empty }, 10m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_COMPANY_REQUIRED",
        () => CreateMovementCapacity(context with { CompanyId = Guid.Empty }, 10m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_TREASURY_ACCOUNT_REQUIRED",
        () => CreateMovementCapacity(context with { TreasuryAccountId = Guid.Empty }, 10m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MOVEMENT_REQUIRED",
        () => CreateMovementCapacity(context, 10m, movementId: Guid.Empty));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MOVEMENT_VERSION_INVALID",
        () => CreateMovementCapacity(context, 10m, version: 0));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MOVEMENT_DIRECTION_INVALID",
        () => CreateMovementCapacity(context, 10m, direction: (TreasuryPayments.PaymentDirection)99));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MOVEMENT_CAPACITY_INVALID",
        () => CreateMovementCapacity(context, 0m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCH_AMOUNT_INVALID",
        () => CreateReconciliationMatch(statementA, movementA, 0m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCH_TENANT_MISMATCH",
        () => CreateReconciliationMatch(statementA, CreateMovementCapacity(context with { TenantId = Guid.NewGuid() }, 10m), 10m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCH_COMPANY_MISMATCH",
        () => CreateReconciliationMatch(statementA, CreateMovementCapacity(context with { CompanyId = Guid.NewGuid() }, 10m), 10m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCH_ACCOUNT_MISMATCH",
        () => CreateReconciliationMatch(statementA, CreateMovementCapacity(context with { TreasuryAccountId = Guid.NewGuid() }, 10m), 10m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCH_CURRENCY_MISMATCH",
        () => CreateReconciliationMatch(
            statementA,
            CreateMovementCapacity(context, 10m, currency: TreasuryPayments.TreasuryCurrencyCode.Create("EUR")),
            10m));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCH_DIRECTION_MISMATCH",
        () => CreateReconciliationMatch(
            statementA,
            CreateMovementCapacity(context, 10m, direction: TreasuryPayments.PaymentDirection.Incoming),
            10m));

    var first = CreateReconciliationMatch(statementA, movementA, 70m);
    var second = CreateReconciliationMatch(statementA, movementB, 30m);
    var third = CreateReconciliationMatch(statementB, movementA, 50m);
    var input = new[] { third, first, second };
    var proposal = TreasuryReconciliation.ValidatedReconciliationProposal.Create(
        Guid.NewGuid(),
        context.TenantId,
        context.CompanyId,
        context.TreasuryAccountId,
        context.Currency,
        input);
    Equal(3, proposal.Matches.Count, "Valid many-to-many reconciliation proposal was rejected.");
    var expectedFirstStatementId = input.Select(match => match.StatementLine.StatementLineId).Order().First();
    Equal(expectedFirstStatementId, proposal.Matches[0].StatementLine.StatementLineId, "Reconciliation ordering is not deterministic.");
    Equal(context.TenantId, proposal.TenantId, "Reconciliation proposal tenant changed.");
    Equal(context.CompanyId, proposal.CompanyId, "Reconciliation proposal company changed.");
    Equal(context.TreasuryAccountId, proposal.TreasuryAccountId, "Reconciliation proposal account changed.");
    Equal(context.Currency, proposal.Currency, "Reconciliation proposal currency changed.");
    input[0] = first;
    if (!proposal.Matches.Contains(third))
    {
        throw new InvalidOperationException("Validated reconciliation proposal retained a mutable input collection.");
    }

    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCH_DUPLICATE",
        () => CreateReconciliationProposal(context, [first, first]));
    ExpectReconciliationInvariant(
        "RECONCILIATION_STATEMENT_CAPACITY_EXCEEDED",
        () => CreateReconciliationProposal(
            context,
            [CreateReconciliationMatch(statementA, movementA, 80m), CreateReconciliationMatch(statementA, movementB, 30m)]));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MOVEMENT_CAPACITY_EXCEEDED",
        () => CreateReconciliationProposal(
            context,
            [CreateReconciliationMatch(statementA, movementA, 80m), CreateReconciliationMatch(statementB, movementA, 50m)]));
    ExpectReconciliationInvariant(
        "RECONCILIATION_PROPOSAL_TENANT_MISMATCH",
        () => TreasuryReconciliation.ValidatedReconciliationProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            context.CompanyId,
            context.TreasuryAccountId,
            context.Currency,
            [first]));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCHES_REQUIRED",
        () => CreateReconciliationProposal(context, []));
    ExpectReconciliationInvariant(
        "RECONCILIATION_MATCH_REQUIRED",
        () => CreateReconciliationProposal(context, [first, null!]));

    if (proposal.Matches is IList<TreasuryReconciliation.ReconciliationMatchDraft> list)
    {
        Throws<NotSupportedException>(() => list[0] = first);
    }
}

static void FinancialReportSliceBoundariesAreEnforced()
{
    var context = CreateReportTestContext();

    ExpectReportingInvariant("REPORT_CURRENCY_INVALID", () => ReportingControlAccounts.ReportCurrencyCode.Create("try"));
    ExpectReportingInvariant(
        "REPORT_DIMENSION_CODE_REQUIRED",
        () => ReportingControlAccounts.ReportDimensionAssignment.Create(" ", "north"));
    ExpectReportingInvariant(
        "REPORT_DIMENSION_VALUE_REQUIRED",
        () => ReportingControlAccounts.ReportDimensionAssignment.Create("branch", string.Empty));
    ExpectReportingInvariant(
        "REPORT_DIMENSIONS_REQUIRED",
        () => ReportingControlAccounts.ReportDimensionSlice.Create(null));
    ExpectReportingInvariant(
        "REPORT_DIMENSION_REQUIRED",
        () => ReportingControlAccounts.ReportDimensionSlice.Create([null]));
    ExpectReportingInvariant(
        "REPORT_DIMENSION_DUPLICATE",
        () => CreateReportDimensions(("branch", "north"), ("branch", "south")));

    var first = ReportingControlAccounts.ReportDimensionAssignment.Create(" project ", " alpha ");
    var second = ReportingControlAccounts.ReportDimensionAssignment.Create("branch", "north");
    var input = new[] { first, second };
    var dimensions = ReportingControlAccounts.ReportDimensionSlice.Create(input);
    Equal("branch", dimensions.Assignments[0].DimensionCode, "Report dimensions are not deterministically ordered.");
    Equal("project", dimensions.Assignments[1].DimensionCode, "Report dimension code was not canonicalized.");
    Equal("alpha", dimensions.Assignments[1].ValueCode, "Report dimension value was not canonicalized.");
    input[0] = second;
    Equal(2, dimensions.Assignments.Count, "Report dimension slice retained a mutable input collection.");
    if (dimensions.Assignments is IList<ReportingControlAccounts.ReportDimensionAssignment> dimensionList)
    {
        Throws<NotSupportedException>(() => dimensionList[0] = first);
    }

    ExpectReportingInvariant("REPORT_TENANT_REQUIRED", () => CreateReportSlice(context with { TenantId = Guid.Empty }));
    ExpectReportingInvariant("REPORT_COMPANY_REQUIRED", () => CreateReportSlice(context with { CompanyId = Guid.Empty }));
    ExpectReportingInvariant(
        "REPORT_PROJECTION_GENERATION_REQUIRED",
        () => CreateReportSlice(context with { ProjectionGenerationId = Guid.Empty }));
    ExpectReportingInvariant("REPORT_CODE_REQUIRED", () => CreateReportSlice(context, reportCode: " "));
    ExpectReportingInvariant("REPORT_DEFINITION_VERSION_INVALID", () => CreateReportSlice(context, definitionVersion: 0));
    ExpectReportingInvariant("REPORT_AS_OF_REQUIRED", () => CreateReportSlice(context, effectiveAsOf: default(DateOnly)));
    ExpectReportingInvariant(
        "REPORT_TIMESTAMP_NOT_UTC",
        () => CreateReportSlice(
            context,
            dataCutoffAt: new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(3))));
    ExpectReportingInvariant(
        "REPORT_GENERATED_BEFORE_CUTOFF",
        () => CreateReportSlice(
            context,
            dataCutoffAt: new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero),
            generatedAt: new DateTimeOffset(2026, 8, 24, 9, 59, 59, TimeSpan.Zero)));

    var slice = CreateReportSlice(context, reportCode: " party-control-account ", dimensions: dimensions);
    Equal("party-control-account", slice.ReportCode, "Report code was not canonicalized.");
    Equal(context.TenantId, slice.TenantId, "Report tenant changed.");
    Equal(context.Currency, slice.Currency, "Report currency changed.");
    Equal(TimeSpan.Zero, slice.DataCutoffAt.Offset, "Report data cutoff is not UTC.");
}

static void ControlAccountBalanceCrossFootIsEnforced()
{
    var context = CreateReportTestContext();

    ExpectReportingInvariant(
        "REPORT_BALANCE_SNAPSHOT_REQUIRED",
        () => CreateControlBalance(context, ReportingControlAccounts.LedgerSide.Subledger, snapshotId: Guid.Empty));
    ExpectReportingInvariant(
        "REPORT_CONTROL_ACCOUNT_REQUIRED",
        () => CreateControlBalance(context with { ControlAccountId = Guid.Empty }, ReportingControlAccounts.LedgerSide.Subledger));
    ExpectReportingInvariant(
        "REPORT_LEDGER_SIDE_INVALID",
        () => CreateControlBalance(context, (ReportingControlAccounts.LedgerSide)99));
    ExpectReportingInvariant(
        "REPORT_BALANCE_MOVEMENT_INVALID",
        () => CreateControlBalance(context, ReportingControlAccounts.LedgerSide.Subledger, debits: -1m));
    ExpectReportingInvariant(
        "REPORT_ROW_COUNT_INVALID",
        () => CreateControlBalance(context, ReportingControlAccounts.LedgerSide.Subledger, rowCount: -1));
    ExpectReportingInvariant(
        "REPORT_SOURCE_CHECKSUM_INVALID",
        () => CreateControlBalance(
            context,
            ReportingControlAccounts.LedgerSide.Subledger,
            sourceChecksumSha256: new string('A', 64)));
    ExpectReportingInvariant(
        "REPORT_BALANCE_CROSS_FOOT_MISMATCH",
        () => CreateControlBalance(
            context,
            ReportingControlAccounts.LedgerSide.Subledger,
            opening: 100m,
            debits: 50m,
            credits: 20m,
            closing: 129.9999m));

    var snapshot = CreateControlBalance(
        context,
        ReportingControlAccounts.LedgerSide.Subledger,
        opening: -10m,
        debits: 15.4321m,
        credits: 2.1111m,
        closing: 3.3210m,
        rowCount: 2);
    Equal(3.3210m, snapshot.ClosingBalance, "Exact decimal balance cross-foot changed.");
    Equal(2L, snapshot.RowCount, "Balance row count changed.");
}

static void ControlAccountReconciliationContextIsEnforced()
{
    var context = CreateReportTestContext();
    var slice = CreateReportSlice(context, dimensions: CreateReportDimensions(("branch", "north")));
    var subledger = CreateControlBalance(
        context,
        ReportingControlAccounts.LedgerSide.Subledger,
        opening: 100m,
        debits: 50m,
        credits: 20m,
        closing: 130m,
        reportSlice: slice);
    var generalLedger = CreateControlBalance(
        context,
        ReportingControlAccounts.LedgerSide.GeneralLedger,
        opening: 100m,
        debits: 50m,
        credits: 20m,
        closing: 130m,
        reportSlice: slice);

    var exact = ReportingControlAccounts.ControlAccountReconciliationResult.Create(
        Guid.NewGuid(),
        subledger,
        generalLedger);
    Equal(decimal.Zero, exact.Difference, "Exact control-account difference changed.");
    Equal(true, exact.IsReconciled, "Zero-difference control account is not reconciled.");

    var differentGl = CreateControlBalance(
        context,
        ReportingControlAccounts.LedgerSide.GeneralLedger,
        opening: 100m,
        debits: 45m,
        credits: 20m,
        closing: 125m,
        reportSlice: slice);
    var difference = ReportingControlAccounts.ControlAccountReconciliationResult.Create(
        Guid.NewGuid(),
        subledger,
        differentGl);
    Equal(5m, difference.Difference, "Control-account difference is not subledger closing minus GL closing.");
    Equal(false, difference.IsReconciled, "Non-zero control-account difference was hidden.");

    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_LEDGER_SIDE_MISMATCH",
        () => ReportingControlAccounts.ControlAccountReconciliationResult.Create(Guid.NewGuid(), subledger, subledger));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_ACCOUNT_MISMATCH",
        () => ReconcileWithChangedSlice(
            context,
            subledger,
            slice,
            context with { ControlAccountId = Guid.NewGuid() }));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_TENANT_MISMATCH",
        () => ReconcileWithChangedSlice(context, subledger, CreateReportSlice(context with { TenantId = Guid.NewGuid() })));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_COMPANY_MISMATCH",
        () => ReconcileWithChangedSlice(context, subledger, CreateReportSlice(context with { CompanyId = Guid.NewGuid() })));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_DEFINITION_MISMATCH",
        () => ReconcileWithChangedSlice(context, subledger, CreateReportSlice(context, definitionVersion: 2)));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_AS_OF_MISMATCH",
        () => ReconcileWithChangedSlice(context, subledger, CreateReportSlice(context, effectiveAsOf: new DateOnly(2026, 8, 23))));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_DATA_CUTOFF_MISMATCH",
        () => ReconcileWithChangedSlice(
            context,
            subledger,
            CreateReportSlice(
                context,
                dataCutoffAt: new DateTimeOffset(2026, 8, 24, 10, 0, 1, TimeSpan.Zero),
                generatedAt: new DateTimeOffset(2026, 8, 24, 10, 1, 0, TimeSpan.Zero))));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_GENERATION_MISMATCH",
        () => ReconcileWithChangedSlice(context, subledger, CreateReportSlice(context with { ProjectionGenerationId = Guid.NewGuid() })));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_CURRENCY_MISMATCH",
        () => ReconcileWithChangedSlice(
            context,
            subledger,
            CreateReportSlice(context with { Currency = ReportingControlAccounts.ReportCurrencyCode.Create("EUR") })));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_DIMENSION_MISMATCH",
        () => ReconcileWithChangedSlice(
            context,
            subledger,
            CreateReportSlice(context, dimensions: CreateReportDimensions(("branch", "south")))));
}

static void PartyStatementEventBoundariesAreEnforced()
{
    var context = CreateReportTestContext();

    ExpectReportingInvariant("PARTY_REPORT_EVENT_REQUIRED", () => CreatePartyStatementEvent(context, eventId: Guid.Empty));
    ExpectReportingInvariant(
        "PARTY_REPORT_ACCOUNT_REQUIRED",
        () => CreatePartyStatementEvent(context with { PartyAccountId = Guid.Empty }));
    ExpectReportingInvariant(
        "PARTY_REPORT_CONTROL_ACCOUNT_REQUIRED",
        () => CreatePartyStatementEvent(context with { ControlAccountId = Guid.Empty }));
    ExpectReportingInvariant(
        "PARTY_REPORT_EVENT_KIND_INVALID",
        () => CreatePartyStatementEvent(context, kind: (ReportingParty.PartyStatementEventKind)99));
    ExpectReportingInvariant("PARTY_REPORT_SOURCE_TYPE_REQUIRED", () => CreatePartyStatementEvent(context, sourceType: " "));
    ExpectReportingInvariant(
        "PARTY_REPORT_PAYMENT_REQUIRED",
        () => CreatePartyStatementEvent(
            context,
            kind: ReportingParty.PartyStatementEventKind.Allocation,
            exposureEffect: -10m));
    ExpectReportingInvariant(
        "PARTY_REPORT_PAYMENT_NOT_ALLOWED",
        () => CreatePartyStatementEvent(context, paymentId: Guid.NewGuid()));
    ExpectReportingInvariant("PARTY_REPORT_EFFECT_INVALID", () => CreatePartyStatementEvent(context, exposureEffect: -10m));
    ExpectReportingInvariant(
        "PARTY_REPORT_EFFECT_INVALID",
        () => CreatePartyStatementEvent(
            context,
            kind: ReportingParty.PartyStatementEventKind.WriteOff,
            exposureEffect: 10m));
    ExpectReportingInvariant(
        "PARTY_REPORT_EFFECTIVE_DATE_REQUIRED",
        () => CreatePartyStatementEvent(context, effectiveDate: default(DateOnly)));
    ExpectReportingInvariant("PARTY_REPORT_SEQUENCE_INVALID", () => CreatePartyStatementEvent(context, sequenceKey: 0));
    ExpectReportingInvariant(
        "PARTY_REPORT_RECORDED_AT_NOT_UTC",
        () => CreatePartyStatementEvent(
            context,
            recordedAt: new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(3))));

    var allocation = CreatePartyStatementEvent(
        context,
        kind: ReportingParty.PartyStatementEventKind.Allocation,
        paymentId: Guid.NewGuid(),
        exposureEffect: -25.4321m,
        sourceType: " treasury.payment-allocation ");
    Equal("treasury.payment-allocation", allocation.SourceType, "Party statement source type was not canonicalized.");
    Equal(-25.4321m, allocation.ExposureEffect, "Party statement exact decimal exposure effect changed.");
}

static void PartyStatementIsDerivedBitemporally()
{
    var context = CreateReportTestContext();
    var openItem = CreatePartyStatementEvent(
        context,
        exposureEffect: 100m,
        effectiveDate: new DateOnly(2026, 8, 20),
        recordedAt: new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero));
    var allocation = CreatePartyStatementEvent(
        context,
        kind: ReportingParty.PartyStatementEventKind.Allocation,
        paymentId: Guid.NewGuid(),
        exposureEffect: -60m,
        effectiveDate: new DateOnly(2026, 8, 22),
        recordedAt: new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero));
    var futureEffective = CreatePartyStatementEvent(
        context,
        exposureEffect: 50m,
        effectiveDate: new DateOnly(2026, 8, 25));
    var lateRecorded = CreatePartyStatementEvent(
        context,
        kind: ReportingParty.PartyStatementEventKind.Unallocation,
        paymentId: allocation.PaymentId,
        exposureEffect: 10m,
        effectiveDate: new DateOnly(2026, 8, 23),
        recordedAt: new DateTimeOffset(2026, 8, 24, 10, 0, 1, TimeSpan.Zero));
    var input = new[] { lateRecorded, allocation, futureEffective, openItem };
    var statement = CreatePartyStatement(context, input);

    Equal(2, statement.Lines.Count, "Party statement did not apply both effective-date and recorded-at cuts.");
    Equal(openItem.EventId, statement.Lines[0].EventSnapshot.EventId, "Party statement ordering is not deterministic.");
    Equal(100m, statement.Lines[0].RunningExposure, "Party statement first running exposure changed.");
    Equal(40m, statement.ClosingExposure, "Party statement closing exposure changed.");
    input[0] = openItem;
    if (!statement.Lines.Any(line => line.EventSnapshot == allocation))
    {
        throw new InvalidOperationException("Party statement retained a mutable event input collection.");
    }

    ExpectReportingInvariant(
        "PARTY_STATEMENT_EVENT_DUPLICATE",
        () => CreatePartyStatement(context, [openItem, openItem]));
    ExpectReportingInvariant(
        "PARTY_STATEMENT_TENANT_MISMATCH",
        () => CreatePartyStatement(context, [CreatePartyStatementEvent(context with { TenantId = Guid.NewGuid() })]));
    ExpectReportingInvariant(
        "PARTY_STATEMENT_CURRENCY_MISMATCH",
        () => CreatePartyStatement(
            context,
            [CreatePartyStatementEvent(context with { Currency = ReportingControlAccounts.ReportCurrencyCode.Create("EUR") })]));
    ExpectReportingInvariant(
        "PARTY_STATEMENT_NEGATIVE_EXPOSURE_UNSUPPORTED",
        () => CreatePartyStatement(
            context,
            [CreatePartyStatementEvent(
                context,
                kind: ReportingParty.PartyStatementEventKind.Allocation,
                paymentId: Guid.NewGuid(),
                exposureEffect: -100.0001m)]));

    if (statement.Lines is IList<ReportingParty.PartyStatementLine> list)
    {
        Throws<NotSupportedException>(() => list[0] = statement.Lines[1]);
    }
}

static void PartyAgingPolicyAndTotalsAreEnforced()
{
    var context = CreateReportTestContext();

    ExpectReportingInvariant(
        "AGING_BUCKET_RANGE_INVALID",
        () => ReportingParty.CalendarDayAgingBucket.Create("invalid", 10, 9));
    ExpectReportingInvariant(
        "AGING_BUCKETS_REQUIRED",
        () => ReportingParty.CalendarDayAgingPolicySnapshot.Create(
            context.TenantId,
            context.CompanyId,
            context.AgingPolicyId,
            1,
            []));
    ExpectReportingInvariant(
        "AGING_BUCKET_COVERAGE_INCOMPLETE",
        () => CreateAgingPolicy(
            context,
            ReportingParty.CalendarDayAgingBucket.Create("partial", 0, int.MaxValue)));
    ExpectReportingInvariant(
        "AGING_BUCKET_COVERAGE_INVALID",
        () => CreateAgingPolicy(
            context,
            ReportingParty.CalendarDayAgingBucket.Create("future", int.MinValue, -2),
            ReportingParty.CalendarDayAgingBucket.Create("due", 0, int.MaxValue)));
    ExpectReportingInvariant(
        "AGING_BUCKET_COVERAGE_INVALID",
        () => CreateAgingPolicy(
            context,
            ReportingParty.CalendarDayAgingBucket.Create("all-a", int.MinValue, int.MaxValue),
            ReportingParty.CalendarDayAgingBucket.Create("all-b", int.MinValue, int.MaxValue)));

    var policy = CreateAgingPolicy(context);
    var future = CreateAgingItem(context, 20m, dueDate: new DateOnly(2026, 8, 25));
    var current = CreateAgingItem(context, 30m, dueDate: new DateOnly(2026, 8, 24));
    var overdue = CreateAgingItem(
        context,
        40m,
        dueDate: new DateOnly(2026, 8, 10),
        isDisputed: true,
        isBlocked: true);
    var input = new[] { current, future, overdue };
    var aging = CreatePartyAging(context, input, policy: policy);

    Equal(90m, aging.TotalRemaining, "Party aging total changed.");
    Equal(20m, aging.BucketSummaries.Single(summary => summary.BucketCode == "future").RemainingAmount, "Future bucket total changed.");
    Equal(30m, aging.BucketSummaries.Single(summary => summary.BucketCode == "current").RemainingAmount, "Current bucket total changed.");
    Equal(40m, aging.BucketSummaries.Single(summary => summary.BucketCode == "overdue").RemainingAmount, "Overdue bucket total changed.");
    Equal(true, aging.Items.Single(item => item.OpenItemId == overdue.OpenItemId).IsDisputed, "Dispute evidence was lost.");
    input[0] = overdue;
    Equal(3, aging.Items.Count, "Party aging retained a mutable input collection.");

    ExpectReportingInvariant(
        "AGING_AMOUNT_INVALID",
        () => CreateAgingItem(context, remainingAmount: 101m, originalAmount: 100m));
    ExpectReportingInvariant(
        "AGING_ITEM_CUT_MISMATCH",
        () => CreatePartyAging(
            context,
            [CreateAgingItem(
                context,
                10m,
                dataCutoffAt: new DateTimeOffset(2026, 8, 24, 9, 59, 59, TimeSpan.Zero))],
            policy: policy));
    ExpectReportingInvariant(
        "AGING_OPEN_ITEM_DUPLICATE",
        () => CreatePartyAging(context, [future, future], policy: policy));

    if (aging.Items is IList<ReportingParty.OpenItemAgingSnapshot> itemList)
    {
        Throws<NotSupportedException>(() => itemList[0] = overdue);
    }
}

static void PartyStatementAgingCrossFootIsEnforced()
{
    var context = CreateReportTestContext();
    var slice = CreateReportSlice(context);
    var statement = CreatePartyStatement(
        context,
        [
            CreatePartyStatementEvent(context, exposureEffect: 100m),
            CreatePartyStatementEvent(
                context,
                kind: ReportingParty.PartyStatementEventKind.Allocation,
                paymentId: Guid.NewGuid(),
                exposureEffect: -10m,
                sequenceKey: 2),
        ],
        reportSlice: slice);
    var aging = CreatePartyAging(
        context,
        [CreateAgingItem(context, 40m), CreateAgingItem(context, 50m)],
        reportSlice: slice);

    var crossFoot = ReportingParty.PartyStatementAgingCrossFoot.Create(Guid.NewGuid(), statement, aging);
    Equal(statement.StatementId, crossFoot.Statement.StatementId, "Party statement cross-foot changed its statement.");
    Equal(90m, crossFoot.Aging.TotalRemaining, "Party statement-aging exact cross-foot changed.");
    var otherPartyContext = context with { PartyAccountId = Guid.NewGuid() };

    ExpectReportingInvariant(
        "PARTY_CROSS_FOOT_TOTAL_MISMATCH",
        () => ReportingParty.PartyStatementAgingCrossFoot.Create(
            Guid.NewGuid(),
            statement,
            CreatePartyAging(context, [CreateAgingItem(context, 89.9999m)], reportSlice: slice)));
    ExpectReportingInvariant(
        "PARTY_CROSS_FOOT_ACCOUNT_MISMATCH",
        () => ReportingParty.PartyStatementAgingCrossFoot.Create(
            Guid.NewGuid(),
            statement,
            CreatePartyAging(
                otherPartyContext,
                [CreateAgingItem(otherPartyContext, 90m)])));
    ExpectReportingInvariant(
        "PARTY_CROSS_FOOT_SLICE_MISMATCH",
        () => ReportingParty.PartyStatementAgingCrossFoot.Create(
            Guid.NewGuid(),
            statement,
            CreatePartyAging(
                context,
                [CreateAgingItem(
                    context,
                    90m,
                    effectiveAsOf: new DateOnly(2026, 8, 23),
                    dataCutoffAt: new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero))],
                reportSlice: CreateReportSlice(
                    context,
                    effectiveAsOf: new DateOnly(2026, 8, 23),
                    dataCutoffAt: new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero),
                    generatedAt: new DateTimeOffset(2026, 8, 23, 10, 1, 0, TimeSpan.Zero)))));
}

static void GoldenPartyCollectionCycleCrossFootIsExact()
{
    var tenantId = Guid.NewGuid();
    var companyId = Guid.NewGuid();
    var partyAccountId = Guid.NewGuid();
    var invoiceEventId = Guid.NewGuid();
    var controlAccountId = Guid.NewGuid();
    var treasuryAccountId = Guid.NewGuid();
    var paymentId = Guid.NewGuid();
    var asOf = new DateOnly(2026, 8, 24);
    var dataCutoff = new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    var dueContext = new DueScheduleTestContext(
        tenantId,
        companyId,
        partyAccountId,
        invoiceEventId,
        Guid.NewGuid(),
        controlAccountId,
        PartyAllocations.AllocationCurrencyCode.Create("GBP"));
    var dueLine = CreateDueLine(dueContext, 100m, dueDate: new DateOnly(2026, 8, 20));
    var dueSchedule = CreateDueSchedule(dueContext, 100m, [dueLine]);

    var paymentContext = new TreasuryPaymentTestContext(
        tenantId,
        companyId,
        partyAccountId,
        treasuryAccountId,
        Guid.NewGuid(),
        TreasuryPayments.TreasuryCurrencyCode.Create("GBP"));
    var payment = CreatePaymentDraft(
        paymentContext,
        paymentId,
        TreasuryPayments.PaymentDirection.Incoming,
        transactionAmount: 60m,
        functionalAmount: 60m,
        recordedAt: UtcAt(2026, 8, 22),
        sourceType: "treasury.receipt",
        postingPurpose: "party-collection",
        effectiveDate: new DateOnly(2026, 8, 22));
    var paymentCapacity = PartyAllocations.PaymentAllocationCapacity.Create(
        tenantId,
        companyId,
        partyAccountId,
        payment.PaymentId,
        dueContext.Currency,
        payment.TransactionAmount);
    var allocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        payment.TransactionAmount,
        effectiveDate: payment.EffectiveDate,
        recordedAt: payment.RecordedAt,
        paymentId: payment.PaymentId);
    var openItem = DeriveOpenItem(dueLine, [allocation], asOf, dataCutoff);
    var paymentAllocation = DerivePaymentAllocations(paymentCapacity, [allocation], asOf, dataCutoff);

    Equal(100m, dueSchedule.SourceOriginalAmount, "Golden due-schedule source amount changed.");
    Equal(60m, paymentAllocation.AllocatedAmount, "Golden payment allocation changed.");
    Equal(0m, paymentAllocation.RemainingUsableAmount, "Golden payment retained an unexpected usable amount.");
    Equal(40m, openItem.RemainingAmount, "Golden open-item remaining amount did not equal 100 - 60.");

    var revenueAccountId = Guid.NewGuid();
    var invoiceJournal = CreateConfiguredDraft(
        [
            JournalLineDraft.Create(controlAccountId, dueLine.DueScheduleLineId, JournalAmount.Create(100m, 0m)),
            JournalLineDraft.Create(revenueAccountId, dueLine.DueScheduleLineId, JournalAmount.Create(0m, 100m)),
        ],
        tenantId: tenantId,
        companyId: companyId,
        sourceEventId: invoiceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "party-receivable",
        functionalCurrency: CurrencyCode.Create("GBP"),
        effectiveDate: new DateOnly(2026, 8, 20));
    var collectionJournal = CreateConfiguredDraft(
        [
            JournalLineDraft.Create(treasuryAccountId, payment.PaymentId, JournalAmount.Create(60m, 0m)),
            JournalLineDraft.Create(controlAccountId, payment.PaymentId, JournalAmount.Create(0m, 60m)),
        ],
        tenantId: tenantId,
        companyId: companyId,
        sourceEventId: payment.PaymentId,
        sourceType: "treasury.payment",
        postingPurpose: "party-collection",
        functionalCurrency: CurrencyCode.Create("GBP"),
        effectiveDate: payment.EffectiveDate);
    var journalSet = ValidatedJournalDraftSet.Create([invoiceJournal, collectionJournal]);
    var controlDebit = journalSet.Drafts
        .SelectMany(draft => draft.Lines)
        .Where(line => line.AccountId == controlAccountId)
        .Sum(line => line.Amount.Debit);
    var controlCredit = journalSet.Drafts
        .SelectMany(draft => draft.Lines)
        .Where(line => line.AccountId == controlAccountId)
        .Sum(line => line.Amount.Credit);
    Equal(2, journalSet.Drafts.Count, "Golden cycle did not retain one journal intent per economic event.");
    Equal(100m, controlDebit, "Golden GL control-account debit changed.");
    Equal(60m, controlCredit, "Golden GL control-account credit changed.");

    var reportContext = new ReportTestContext(
        tenantId,
        companyId,
        partyAccountId,
        controlAccountId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        ReportingControlAccounts.ReportCurrencyCode.Create("GBP"));
    var reportSlice = CreateReportSlice(
        reportContext,
        reportCode: "mp03-party-golden-cycle",
        effectiveAsOf: asOf,
        dataCutoffAt: dataCutoff,
        generatedAt: dataCutoff.AddMinutes(1));
    var statement = CreatePartyStatement(
        reportContext,
        [
            CreatePartyStatementEvent(
                reportContext,
                kind: ReportingParty.PartyStatementEventKind.OpenItem,
                sourceType: "sales.invoice",
                sourceEventId: invoiceEventId,
                dueScheduleLineId: dueLine.DueScheduleLineId,
                exposureEffect: dueSchedule.SourceOriginalAmount,
                effectiveDate: new DateOnly(2026, 8, 20),
                sequenceKey: 1,
                recordedAt: UtcAt(2026, 8, 20)),
            CreatePartyStatementEvent(
                reportContext,
                kind: ReportingParty.PartyStatementEventKind.Allocation,
                sourceType: "party.payment-allocation",
                sourceEventId: allocation.EventId,
                dueScheduleLineId: dueLine.DueScheduleLineId,
                paymentId: payment.PaymentId,
                exposureEffect: -paymentAllocation.AllocatedAmount,
                effectiveDate: allocation.EffectiveDate,
                sequenceKey: 1,
                recordedAt: allocation.RecordedAt),
        ],
        reportSlice: reportSlice);
    var agingItem = ReportingParty.OpenItemAgingSnapshot.Create(
        Guid.NewGuid(),
        tenantId,
        companyId,
        partyAccountId,
        controlAccountId,
        invoiceEventId,
        dueLine.DueScheduleLineId,
        reportContext.Currency,
        dueLine.OriginalAmount,
        openItem.RemainingAmount,
        dueLine.DueDate,
        asOf,
        dataCutoff,
        isDisputed: false,
        isBlocked: false);
    var aging = CreatePartyAging(reportContext, [agingItem], reportSlice: reportSlice);
    var statementAging = ReportingParty.PartyStatementAgingCrossFoot.Create(Guid.NewGuid(), statement, aging);

    Equal(40m, statement.ClosingExposure, "Golden Party statement closing exposure changed.");
    Equal(openItem.RemainingAmount, aging.TotalRemaining, "Golden aging did not derive the open-item remainder.");
    Equal(statement.ClosingExposure, statementAging.Aging.TotalRemaining, "Golden Party reports did not cross-foot.");

    var subledger = CreateControlBalance(
        reportContext,
        ReportingControlAccounts.LedgerSide.Subledger,
        opening: 0m,
        debits: dueSchedule.SourceOriginalAmount,
        credits: paymentAllocation.AllocatedAmount,
        closing: openItem.RemainingAmount,
        rowCount: statement.Lines.Count,
        reportSlice: reportSlice);
    var generalLedger = CreateControlBalance(
        reportContext,
        ReportingControlAccounts.LedgerSide.GeneralLedger,
        opening: 0m,
        debits: controlDebit,
        credits: controlCredit,
        closing: controlDebit - controlCredit,
        rowCount: journalSet.Drafts.SelectMany(draft => draft.Lines).Count(),
        reportSlice: reportSlice);
    var reconciliation = ReportingControlAccounts.ControlAccountReconciliationResult.Create(
        Guid.NewGuid(),
        subledger,
        generalLedger);

    Equal(true, reconciliation.IsReconciled, "Golden Party subledger and GL control account did not reconcile.");
    Equal(0m, reconciliation.Difference, "Golden control-account reconciliation introduced a plug or tolerance.");

    var otherCompanyContext = reportContext with { CompanyId = Guid.NewGuid() };
    var otherCompanyGl = CreateControlBalance(
        otherCompanyContext,
        ReportingControlAccounts.LedgerSide.GeneralLedger,
        opening: 0m,
        debits: controlDebit,
        credits: controlCredit,
        closing: controlDebit - controlCredit,
        reportSlice: CreateReportSlice(
            otherCompanyContext,
            reportCode: "mp03-party-golden-cycle",
            effectiveAsOf: asOf,
            dataCutoffAt: dataCutoff,
            generatedAt: dataCutoff.AddMinutes(1)));
    ExpectReportingInvariant(
        "REPORT_RECONCILIATION_COMPANY_MISMATCH",
        () => ReportingControlAccounts.ControlAccountReconciliationResult.Create(
            Guid.NewGuid(),
            subledger,
            otherCompanyGl));
}

static void DueScheduleLineBoundariesAreEnforced()
{
    var context = CreateDueScheduleTestContext();

    ExpectPartyOpenItemInvariant("DUE_TENANT_REQUIRED", () => CreateDueLine(context with { TenantId = Guid.Empty }, 10m));
    ExpectPartyOpenItemInvariant("DUE_COMPANY_REQUIRED", () => CreateDueLine(context with { CompanyId = Guid.Empty }, 10m));
    ExpectPartyOpenItemInvariant(
        "DUE_PARTY_ACCOUNT_REQUIRED",
        () => CreateDueLine(context with { PartyAccountId = Guid.Empty }, 10m));
    ExpectPartyOpenItemInvariant("DUE_SOURCE_REQUIRED", () => CreateDueLine(context with { SourceEventId = Guid.Empty }, 10m));
    ExpectPartyOpenItemInvariant("DUE_LINE_REQUIRED", () => CreateDueLine(context, 10m, lineId: Guid.Empty));
    ExpectPartyOpenItemInvariant("DUE_ORIGINAL_AMOUNT_INVALID", () => CreateDueLine(context, 0m));
    ExpectPartyOpenItemInvariant("DUE_DATE_REQUIRED", () => CreateDueLine(context, 10m, dueDate: default(DateOnly)));
    ExpectPartyOpenItemInvariant(
        "DUE_PAYMENT_TERM_REQUIRED",
        () => CreateDueLine(context with { PaymentTermSnapshotId = Guid.Empty }, 10m));
    ExpectPartyOpenItemInvariant(
        "DUE_PAYMENT_TERM_VERSION_INVALID",
        () => CreateDueLine(context, 10m, paymentTermVersion: 0));
    ExpectPartyOpenItemInvariant(
        "DUE_CONTROL_ACCOUNT_REQUIRED",
        () => CreateDueLine(context with { ControlAccountId = Guid.Empty }, 10m));

    var line = CreateDueLine(context, 10.1234m, dueDate: new DateOnly(2026, 10, 15));
    Equal(10.1234m, line.OriginalAmount, "Due-line amount changed.");
    Equal(new DateOnly(2026, 10, 15), line.DueDate, "Due date changed.");
    Equal(context.Currency, line.Currency, "Due-line currency changed.");
}

static void DueScheduleTotalAndImmutabilityAreEnforced()
{
    var context = CreateDueScheduleTestContext();
    var first = CreateDueLine(context, 30m, dueDate: new DateOnly(2026, 11, 1));
    var second = CreateDueLine(context, 30m, dueDate: new DateOnly(2026, 9, 1));
    var third = CreateDueLine(context, 40m, dueDate: new DateOnly(2026, 10, 1));
    var input = new[] { first, second, third };
    var schedule = CreateDueSchedule(context, 100m, input);

    Equal(100m, schedule.SourceOriginalAmount, "Source original amount changed.");
    Equal(second.DueScheduleLineId, schedule.Lines[0].DueScheduleLineId, "Due lines are not ordered by date.");
    Equal(third.DueScheduleLineId, schedule.Lines[1].DueScheduleLineId, "Due-line ordering changed.");
    input[0] = CreateDueLine(context, 30m);
    if (!schedule.Lines.Contains(first))
    {
        throw new InvalidOperationException("Validated due schedule retained a mutable input collection.");
    }

    if (schedule.Lines is IList<PartyDueSchedules.DueScheduleLine> list)
    {
        Throws<NotSupportedException>(() => list[0] = input[0]);
    }

    ExpectPartyOpenItemInvariant("DUE_SOURCE_AMOUNT_INVALID", () => CreateDueSchedule(context, 0m, [first]));
    ExpectPartyOpenItemInvariant("DUE_LINES_REQUIRED", () => CreateDueSchedule(context, 100m, []));
    ExpectPartyOpenItemInvariant("DUE_LINES_REQUIRED", () => CreateDueSchedule(context, 100m, null));
    ExpectPartyOpenItemInvariant("DUE_LINE_REQUIRED", () => CreateDueSchedule(context, 100m, [first, null!]));
    ExpectPartyOpenItemInvariant("DUE_LINE_DUPLICATE", () => CreateDueSchedule(context, 60m, [first, first]));
    ExpectPartyOpenItemInvariant("DUE_TOTAL_MISMATCH", () => CreateDueSchedule(context, 99.9999m, [first, second, third]));
    ExpectPartyOpenItemInvariant(
        "DUE_TOTAL_OVERFLOW",
        () => CreateDueSchedule(
            context,
            decimal.MaxValue,
            [CreateDueLine(context, decimal.MaxValue), CreateDueLine(context, 1m)]));
}

static void DueScheduleScopeIsEnforced()
{
    var context = CreateDueScheduleTestContext();

    ExpectPartyOpenItemInvariant(
        "DUE_TENANT_MISMATCH",
        () => CreateDueSchedule(context, 10m, [CreateDueLine(context with { TenantId = Guid.NewGuid() }, 10m)]));
    ExpectPartyOpenItemInvariant(
        "DUE_COMPANY_MISMATCH",
        () => CreateDueSchedule(context, 10m, [CreateDueLine(context with { CompanyId = Guid.NewGuid() }, 10m)]));
    ExpectPartyOpenItemInvariant(
        "DUE_PARTY_ACCOUNT_MISMATCH",
        () => CreateDueSchedule(context, 10m, [CreateDueLine(context with { PartyAccountId = Guid.NewGuid() }, 10m)]));
    ExpectPartyOpenItemInvariant(
        "DUE_SOURCE_MISMATCH",
        () => CreateDueSchedule(context, 10m, [CreateDueLine(context with { SourceEventId = Guid.NewGuid() }, 10m)]));
    ExpectPartyOpenItemInvariant(
        "DUE_CURRENCY_MISMATCH",
        () => CreateDueSchedule(
            context,
            10m,
            [CreateDueLine(context with { Currency = PartyAllocations.AllocationCurrencyCode.Create("EUR") }, 10m)]));
}

static void OpenItemRemainingIsDerivedAsOf()
{
    var context = CreateDueScheduleTestContext();
    var dueLine = CreateDueLine(context, 100m);
    var allocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        60m,
        new DateOnly(2026, 8, 1),
        UtcAt(2026, 8, 10));
    var writeOff = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.WriteOff,
        10m,
        new DateOnly(2026, 8, 3),
        UtcAt(2026, 8, 4));
    var unallocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        new DateOnly(2026, 8, 12),
        UtcAt(2026, 8, 12),
        allocation.EventId,
        paymentId: allocation.PaymentId);
    var writeOffReversal = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.WriteOffReversal,
        10m,
        new DateOnly(2026, 8, 13),
        UtcAt(2026, 8, 13),
        writeOff.EventId);
    var events = new[] { unallocation, allocation, writeOff, writeOffReversal };

    var earlyRecordedCutoff = PartyOpenItems.DerivedOpenItemSnapshot.Create(
        dueLine,
        new DateOnly(2026, 8, 10),
        UtcAt(2026, 8, 5),
        events);
    Equal(0m, earlyRecordedCutoff.AllocatedAmount, "Late-recorded allocation leaked into the cutoff.");
    Equal(10m, earlyRecordedCutoff.WrittenOffAmount, "Recorded write-off was not included.");
    Equal(90m, earlyRecordedCutoff.RemainingAmount, "Early recorded-cutoff remaining amount is wrong.");

    var beforeUnallocation = PartyOpenItems.DerivedOpenItemSnapshot.Create(
        dueLine,
        new DateOnly(2026, 8, 11),
        UtcAt(2026, 8, 20),
        events);
    Equal(60m, beforeUnallocation.AllocatedAmount, "Allocation was not included as of its effective date.");
    Equal(30m, beforeUnallocation.RemainingAmount, "Pre-unallocation remaining amount is wrong.");

    var afterUnallocation = PartyOpenItems.DerivedOpenItemSnapshot.Create(
        dueLine,
        new DateOnly(2026, 8, 12),
        UtcAt(2026, 8, 20),
        events);
    Equal(0m, afterUnallocation.AllocatedAmount, "Unallocation did not counter the allocation.");
    Equal(90m, afterUnallocation.RemainingAmount, "Post-unallocation remaining amount is wrong.");

    var afterAllCounters = PartyOpenItems.DerivedOpenItemSnapshot.Create(
        dueLine,
        new DateOnly(2026, 8, 13),
        UtcAt(2026, 8, 20),
        events);
    Equal(0m, afterAllCounters.WrittenOffAmount, "Write-off reversal did not counter the write-off.");
    Equal(100m, afterAllCounters.RemainingAmount, "Fully countered remaining amount is wrong.");

    events[0] = writeOff;
    Equal(4, afterAllCounters.ConsideredEvents.Count, "Derived open item retained a mutable input collection.");
    if (afterAllCounters.ConsideredEvents is IList<PartyOpenItems.OpenItemImpactEvent> list)
    {
        Throws<NotSupportedException>(() => list[0] = allocation);
    }

    var empty = PartyOpenItems.DerivedOpenItemSnapshot.Create(
        dueLine,
        new DateOnly(2026, 8, 1),
        UtcAt(2026, 8, 1),
        []);
    Equal(100m, empty.RemainingAmount, "Empty history invented an open-item impact.");
}

static void OpenItemCounterEventsAreEnforced()
{
    var context = CreateDueScheduleTestContext();
    var dueLine = CreateDueLine(context, 100m);
    var allocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        60m,
        new DateOnly(2026, 8, 1),
        UtcAt(2026, 8, 1));
    var writeOff = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.WriteOff,
        10m,
        new DateOnly(2026, 8, 2),
        UtcAt(2026, 8, 2));

    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_EVENT_REQUIRED",
        () => CreateOpenItemEvent(dueLine, PartyOpenItems.OpenItemImpactKind.Allocation, 1m, eventId: Guid.Empty));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_EVENT_AMOUNT_INVALID",
        () => CreateOpenItemEvent(dueLine, PartyOpenItems.OpenItemImpactKind.Allocation, 0m));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_SOURCE_TYPE_INVALID",
        () => CreateOpenItemEvent(
            dueLine,
            PartyOpenItems.OpenItemImpactKind.Allocation,
            1m,
            sourceType: " "));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_SOURCE_VERSION_INVALID",
        () => CreateOpenItemEvent(
            dueLine,
            PartyOpenItems.OpenItemImpactKind.Allocation,
            1m,
            sourceVersion: 0));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_POSTING_PURPOSE_INVALID",
        () => CreateOpenItemEvent(
            dueLine,
            PartyOpenItems.OpenItemImpactKind.Allocation,
            1m,
            sourcePostingPurpose: " "));
    var normalizedSource = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        1m,
        sourceType: " party.payment-allocation ",
        sourcePostingPurpose: " party.payment-allocation.post ");
    Equal("party.payment-allocation", normalizedSource.SourceType, "Impact source type was not normalized.");
    Equal(
        "party.payment-allocation.post",
        normalizedSource.SourcePostingPurpose,
        "Impact posting purpose was not normalized.");
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_EVENT_KIND_INVALID",
        () => CreateOpenItemEvent(dueLine, (PartyOpenItems.OpenItemImpactKind)99, 1m));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_RECORDED_AT_NOT_UTC",
        () => CreateOpenItemEvent(
            dueLine,
            PartyOpenItems.OpenItemImpactKind.Allocation,
            1m,
            recordedAt: new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.FromHours(3))));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_REVERSAL_LINK_REQUIRED",
        () => CreateOpenItemEvent(dueLine, PartyOpenItems.OpenItemImpactKind.Unallocation, 1m));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_REVERSAL_LINK_UNEXPECTED",
        () => CreateOpenItemEvent(
            dueLine,
            PartyOpenItems.OpenItemImpactKind.Allocation,
            1m,
            reversesEventId: Guid.NewGuid()));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_CUTOFF_NOT_UTC",
        () => DeriveOpenItem(
            dueLine,
            [allocation],
            recordedCutoff: new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.FromHours(3))));
    ExpectPartyOpenItemInvariant("OPEN_ITEM_EVENTS_REQUIRED", () => DeriveOpenItem(dueLine, null));
    ExpectPartyOpenItemInvariant("OPEN_ITEM_EVENT_REQUIRED", () => DeriveOpenItem(dueLine, [allocation, null!]));
    ExpectPartyOpenItemInvariant("OPEN_ITEM_EVENT_DUPLICATE", () => DeriveOpenItem(dueLine, [allocation, allocation]));

    ExpectOpenItemScopeMismatch(dueLine, allocation, "OPEN_ITEM_TENANT_MISMATCH", tenantId: Guid.NewGuid());
    ExpectOpenItemScopeMismatch(dueLine, allocation, "OPEN_ITEM_COMPANY_MISMATCH", companyId: Guid.NewGuid());
    ExpectOpenItemScopeMismatch(dueLine, allocation, "OPEN_ITEM_PARTY_ACCOUNT_MISMATCH", partyAccountId: Guid.NewGuid());
    ExpectOpenItemScopeMismatch(dueLine, allocation, "OPEN_ITEM_DUE_LINE_MISMATCH", dueLineId: Guid.NewGuid());
    ExpectOpenItemScopeMismatch(
        dueLine,
        allocation,
        "OPEN_ITEM_CURRENCY_MISMATCH",
        currency: PartyAllocations.AllocationCurrencyCode.Create("EUR"));

    var missingOriginal = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        reversesEventId: Guid.NewGuid());
    ExpectPartyOpenItemInvariant("OPEN_ITEM_REVERSED_EVENT_MISSING", () => DeriveOpenItem(dueLine, [missingOriginal]));

    var wrongKind = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        10m,
        reversesEventId: writeOff.EventId);
    ExpectPartyOpenItemInvariant("OPEN_ITEM_REVERSAL_KIND_MISMATCH", () => DeriveOpenItem(dueLine, [writeOff, wrongKind]));

    var wrongAmount = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        59m,
        reversesEventId: allocation.EventId);
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_REVERSAL_AMOUNT_MISMATCH",
        () => DeriveOpenItem(dueLine, [allocation, wrongAmount]));

    var preceding = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        effectiveDate: new DateOnly(2026, 7, 31),
        recordedAt: UtcAt(2026, 8, 1),
        reversesEventId: allocation.EventId,
        paymentId: allocation.PaymentId);
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_REVERSAL_PRECEDES_ORIGINAL",
        () => DeriveOpenItem(dueLine, [allocation, preceding]));

    var firstReversal = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        reversesEventId: allocation.EventId,
        paymentId: allocation.PaymentId);
    var secondReversal = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        reversesEventId: allocation.EventId,
        paymentId: allocation.PaymentId);
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_REVERSAL_DUPLICATE",
        () => DeriveOpenItem(dueLine, [allocation, firstReversal, secondReversal]));

    var overCapacity = CreateOpenItemEvent(dueLine, PartyOpenItems.OpenItemImpactKind.Allocation, 100.0001m);
    ExpectPartyOpenItemInvariant("OPEN_ITEM_CAPACITY_EXCEEDED", () => DeriveOpenItem(dueLine, [overCapacity]));

    var maximumLine = CreateDueLine(context, decimal.MaxValue);
    var maximumAllocation = CreateOpenItemEvent(
        maximumLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        decimal.MaxValue);
    var maximumWriteOff = CreateOpenItemEvent(
        maximumLine,
        PartyOpenItems.OpenItemImpactKind.WriteOff,
        decimal.MaxValue);
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_AMOUNT_OVERFLOW",
        () => DeriveOpenItem(maximumLine, [maximumAllocation, maximumWriteOff]));
}

static void PaymentAllocationIsDerivedAsOf()
{
    var context = CreateDueScheduleTestContext();
    var payment = PartyAllocations.PaymentAllocationCapacity.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        Guid.NewGuid(),
        context.Currency,
        100m);
    var firstDueLine = CreateDueLine(context, 100m);
    var secondDueLine = CreateDueLine(context, 100m);
    var firstAllocation = CreateOpenItemEvent(
        firstDueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        60m,
        new DateOnly(2026, 8, 1),
        UtcAt(2026, 8, 5),
        paymentId: payment.PaymentId);
    var secondAllocation = CreateOpenItemEvent(
        secondDueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        30m,
        new DateOnly(2026, 8, 3),
        UtcAt(2026, 8, 3),
        paymentId: payment.PaymentId);
    var unallocation = CreateOpenItemEvent(
        firstDueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        new DateOnly(2026, 8, 10),
        UtcAt(2026, 8, 10),
        firstAllocation.EventId,
        paymentId: payment.PaymentId);
    var events = new[] { unallocation, firstAllocation, secondAllocation };

    var earlyRecordedCutoff = DerivePaymentAllocations(
        payment,
        events,
        new DateOnly(2026, 8, 4),
        UtcAt(2026, 8, 4));
    Equal(30m, earlyRecordedCutoff.AllocatedAmount, "Late-recorded payment allocation leaked into the cutoff.");
    Equal(70m, earlyRecordedCutoff.RemainingUsableAmount, "Early payment remaining amount is wrong.");

    var beforeUnallocation = DerivePaymentAllocations(
        payment,
        events,
        new DateOnly(2026, 8, 9),
        UtcAt(2026, 8, 20));
    Equal(90m, beforeUnallocation.AllocatedAmount, "Payment allocations were not accumulated.");
    Equal(10m, beforeUnallocation.RemainingUsableAmount, "Pre-unallocation payment remainder is wrong.");

    var afterUnallocation = DerivePaymentAllocations(
        payment,
        events,
        new DateOnly(2026, 8, 10),
        UtcAt(2026, 8, 20));
    Equal(30m, afterUnallocation.AllocatedAmount, "Unallocation did not restore payment capacity.");
    Equal(70m, afterUnallocation.RemainingUsableAmount, "Restored payment capacity is wrong.");

    events[0] = secondAllocation;
    Equal(3, afterUnallocation.ConsideredEvents.Count, "Payment allocation snapshot retained mutable input.");
    if (afterUnallocation.ConsideredEvents is IList<PartyOpenItems.OpenItemImpactEvent> list)
    {
        Throws<NotSupportedException>(() => list[0] = firstAllocation);
    }

    var unused = DerivePaymentAllocations(payment, []);
    Equal(0m, unused.AllocatedAmount, "Empty payment history invented an allocation.");
    Equal(100m, unused.RemainingUsableAmount, "Unused payment capacity changed.");
}

static void PaymentAllocationScopeAndCapacityAreEnforced()
{
    var context = CreateDueScheduleTestContext();
    var payment = PartyAllocations.PaymentAllocationCapacity.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        Guid.NewGuid(),
        context.Currency,
        100m);
    var dueLine = CreateDueLine(context, 100m);
    var allocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        60m,
        paymentId: payment.PaymentId);

    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_PAYMENT_REQUIRED",
        () => CreateOpenItemEvent(
            dueLine,
            PartyOpenItems.OpenItemImpactKind.Allocation,
            1m,
            paymentId: Guid.Empty));
    ExpectPartyOpenItemInvariant(
        "OPEN_ITEM_PAYMENT_UNEXPECTED",
        () => CreateOpenItemEvent(
            dueLine,
            PartyOpenItems.OpenItemImpactKind.WriteOff,
            1m,
            paymentId: Guid.NewGuid()));
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_CUTOFF_NOT_UTC",
        () => DerivePaymentAllocations(
            payment,
            [allocation],
            recordedCutoff: new DateTimeOffset(2026, 8, 2, 9, 0, 0, TimeSpan.FromHours(3))));
    ExpectPartyOpenItemInvariant("PAYMENT_ALLOCATION_EVENTS_REQUIRED", () => DerivePaymentAllocations(payment, null));
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_EVENT_REQUIRED",
        () => DerivePaymentAllocations(payment, [allocation, null!]));
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_EVENT_DUPLICATE",
        () => DerivePaymentAllocations(payment, [allocation, allocation]));

    var writeOff = CreateOpenItemEvent(dueLine, PartyOpenItems.OpenItemImpactKind.WriteOff, 1m);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_EVENT_KIND_INVALID",
        () => DerivePaymentAllocations(payment, [writeOff]));
    ExpectPaymentAllocationScopeMismatch(payment, dueLine, "PAYMENT_ALLOCATION_TENANT_MISMATCH", tenantId: Guid.NewGuid());
    ExpectPaymentAllocationScopeMismatch(payment, dueLine, "PAYMENT_ALLOCATION_COMPANY_MISMATCH", companyId: Guid.NewGuid());
    ExpectPaymentAllocationScopeMismatch(
        payment,
        dueLine,
        "PAYMENT_ALLOCATION_PARTY_ACCOUNT_MISMATCH",
        partyAccountId: Guid.NewGuid());
    ExpectPaymentAllocationScopeMismatch(
        payment,
        dueLine,
        "PAYMENT_ALLOCATION_PAYMENT_MISMATCH",
        paymentId: Guid.NewGuid());
    ExpectPaymentAllocationScopeMismatch(
        payment,
        dueLine,
        "PAYMENT_ALLOCATION_CURRENCY_MISMATCH",
        currency: PartyAllocations.AllocationCurrencyCode.Create("EUR"));

    var overCapacity = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        100.0001m,
        paymentId: payment.PaymentId);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_CAPACITY_EXCEEDED",
        () => DerivePaymentAllocations(payment, [overCapacity]));

    var maximumPayment = PartyAllocations.PaymentAllocationCapacity.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        Guid.NewGuid(),
        context.Currency,
        decimal.MaxValue);
    var firstMaximum = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        decimal.MaxValue,
        paymentId: maximumPayment.PaymentId);
    var secondMaximum = CreateOpenItemEvent(
        CreateDueLine(context, decimal.MaxValue),
        PartyOpenItems.OpenItemImpactKind.Allocation,
        decimal.MaxValue,
        paymentId: maximumPayment.PaymentId);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_AMOUNT_OVERFLOW",
        () => DerivePaymentAllocations(maximumPayment, [firstMaximum, secondMaximum]));
}

static void PaymentUnallocationLinkageIsEnforced()
{
    var context = CreateDueScheduleTestContext();
    var payment = PartyAllocations.PaymentAllocationCapacity.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        Guid.NewGuid(),
        context.Currency,
        100m);
    var dueLine = CreateDueLine(context, 100m);
    var allocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        60m,
        new DateOnly(2026, 8, 1),
        UtcAt(2026, 8, 1),
        paymentId: payment.PaymentId);

    var missingOriginal = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        reversesEventId: Guid.NewGuid(),
        paymentId: payment.PaymentId);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_REVERSED_EVENT_MISSING",
        () => DerivePaymentAllocations(payment, [missingOriginal]));

    var wrongAmount = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        59m,
        reversesEventId: allocation.EventId,
        paymentId: payment.PaymentId);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_REVERSAL_AMOUNT_MISMATCH",
        () => DerivePaymentAllocations(payment, [allocation, wrongAmount]));

    var otherDueLine = CreateDueLine(context, 100m);
    var wrongDueLine = CreateOpenItemEvent(
        otherDueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        reversesEventId: allocation.EventId,
        paymentId: payment.PaymentId);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_REVERSAL_CONTEXT_MISMATCH",
        () => DerivePaymentAllocations(payment, [allocation, wrongDueLine]));

    var preceding = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        new DateOnly(2026, 7, 31),
        UtcAt(2026, 8, 1),
        allocation.EventId,
        paymentId: payment.PaymentId);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_REVERSAL_PRECEDES_ORIGINAL",
        () => DerivePaymentAllocations(payment, [allocation, preceding]));

    var firstUnallocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        reversesEventId: allocation.EventId,
        paymentId: payment.PaymentId);
    var secondUnallocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        reversesEventId: allocation.EventId,
        paymentId: payment.PaymentId);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_REVERSAL_DUPLICATE",
        () => DerivePaymentAllocations(payment, [allocation, firstUnallocation, secondUnallocation]));

    var chainedUnallocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Unallocation,
        60m,
        reversesEventId: firstUnallocation.EventId,
        paymentId: payment.PaymentId);
    ExpectPartyOpenItemInvariant(
        "PAYMENT_ALLOCATION_REVERSAL_KIND_MISMATCH",
        () => DerivePaymentAllocations(payment, [allocation, firstUnallocation, chainedUnallocation]));
}

static void JournalReversalIsExactAndLinked()
{
    var context = CreateCurrencyTestContext();
    var firstDimension = AccountingDimensions.DimensionAssignment.Create(Guid.NewGuid(), Guid.NewGuid());
    var secondDimension = AccountingDimensions.DimensionAssignment.Create(Guid.NewGuid(), Guid.NewGuid());
    var debitCurrency = AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        CreateExchangeRate(context, numerator: 321m, denominator: 100m),
        CreateRoundingPolicy(context, scale: 2),
        JournalAmount.Create(10.005m, 0m));
    var creditCurrency = AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        CreateExchangeRate(context, numerator: 321m, denominator: 100m),
        CreateRoundingPolicy(context, scale: 2),
        JournalAmount.Create(0m, 10.005m));
    var debitSourceLineId = Guid.NewGuid();
    var creditSourceLineId = Guid.NewGuid();
    var original = CreateConfiguredDraft(
        [
            JournalLineDraft.Create(
                Guid.NewGuid(),
                debitSourceLineId,
                debitCurrency.FunctionalAmount,
                [secondDimension, firstDimension],
                debitCurrency),
            JournalLineDraft.Create(
                Guid.NewGuid(),
                creditSourceLineId,
                creditCurrency.FunctionalAmount,
                [firstDimension, secondDimension],
                creditCurrency),
        ],
        tenantId: context.TenantId,
        companyId: context.CompanyId,
        functionalCurrency: context.FunctionalCurrency);
    var originalJournalId = Guid.NewGuid();
    var reversalRuleVersionId = Guid.NewGuid();
    var recordedAt = new DateTimeOffset(2026, 8, 22, 10, 30, 0, TimeSpan.Zero);
    var reversal = AccountingReversals.JournalReversalDraft.Create(
        originalJournalId,
        original,
        reversalRuleVersionId,
        "accounting.reversal-test",
        "technical-exact-reversal",
        new DateOnly(2026, 8, 22),
        recordedAt);

    Equal(originalJournalId, reversal.OriginalJournalId, "Original journal link changed.");
    Equal(original, reversal.OriginalJournalDraft, "Original journal reference changed.");
    Equal(originalJournalId, reversal.ReversalJournalDraft.SourceEventId, "Reversal source link changed.");
    Equal(reversalRuleVersionId, reversal.ReversalJournalDraft.PostingRuleVersionId, "Reversal rule changed.");
    Equal(recordedAt, reversal.ReversalJournalDraft.RecordedAt, "Reversal recorded time changed.");
    Equal(original.TotalDebit, reversal.ReversalJournalDraft.TotalCredit, "Original debits were not reversed.");
    Equal(original.TotalCredit, reversal.ReversalJournalDraft.TotalDebit, "Original credits were not reversed.");

    for (var index = 0; index < original.Lines.Count; index++)
    {
        var originalLine = original.Lines[index];
        var reversedLine = reversal.ReversalJournalDraft.Lines[index];
        Equal(originalLine.AccountId, reversedLine.AccountId, "Reversal account changed.");
        Equal(originalLine.SourceLineId, reversedLine.SourceLineId, "Reversal source-line link changed.");
        Equal(originalLine.Dimensions.Count, reversedLine.Dimensions.Count, "Reversal dimensions changed.");
        Equal(originalLine.Amount.Debit, reversedLine.Amount.Credit, "Debit was not reversed to credit.");
        Equal(originalLine.Amount.Credit, reversedLine.Amount.Debit, "Credit was not reversed to debit.");

        for (var dimensionIndex = 0; dimensionIndex < originalLine.Dimensions.Count; dimensionIndex++)
        {
            Equal(
                originalLine.Dimensions[dimensionIndex],
                reversedLine.Dimensions[dimensionIndex],
                "Reversal dimension assignment changed.");
        }

        if (originalLine.CurrencyAmount is null || reversedLine.CurrencyAmount is null)
        {
            throw new InvalidOperationException("Currency snapshot was lost while creating a reversal.");
        }

        Equal(
            originalLine.CurrencyAmount.TransactionAmount.Debit,
            reversedLine.CurrencyAmount.TransactionAmount.Credit,
            "Transaction debit was not reversed to credit.");
        Equal(
            originalLine.CurrencyAmount.TransactionAmount.Credit,
            reversedLine.CurrencyAmount.TransactionAmount.Debit,
            "Transaction credit was not reversed to debit.");
        Equal(
            originalLine.CurrencyAmount.UnroundedFunctionalAmount,
            reversedLine.CurrencyAmount.UnroundedFunctionalAmount,
            "Reversal currency calculation changed.");
        Equal(
            originalLine.CurrencyAmount.RoundingDifference,
            reversedLine.CurrencyAmount.RoundingDifference,
            "Reversal rounding difference changed.");
    }

    Equal(32.12m, original.Lines[0].Amount.Debit, "Original journal was mutated.");
    Equal(0m, original.Lines[0].Amount.Credit, "Original journal side was mutated.");
}

static void JournalReversalContextIsEnforced()
{
    var original = CreateDefaultDraft(CreateBalancedLines(10m));

    ExpectReversalInvariant(
        "REVERSAL_ORIGINAL_JOURNAL_REQUIRED",
        () => CreateJournalReversal(Guid.Empty, original));
    Throws<ArgumentNullException>(
        () => CreateJournalReversal(Guid.NewGuid(), null!));
    ExpectInvariant(
        "JOURNAL_RULE_VERSION_REQUIRED",
        () => CreateJournalReversal(Guid.NewGuid(), original, reversalPostingRuleVersionId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_SOURCE_TYPE_REQUIRED",
        () => CreateJournalReversal(Guid.NewGuid(), original, reversalSourceType: " "));
    ExpectInvariant(
        "JOURNAL_PURPOSE_REQUIRED",
        () => CreateJournalReversal(Guid.NewGuid(), original, reversalPostingPurpose: string.Empty));
    ExpectInvariant(
        "JOURNAL_RECORDED_AT_NOT_UTC",
        () => CreateJournalReversal(
            Guid.NewGuid(),
            original,
            recordedAt: new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.FromHours(3))));

    var noCurrencyReversal = CreateJournalReversal(Guid.NewGuid(), original);
    if (noCurrencyReversal.ReversalJournalDraft.Lines.Any(line => line.CurrencyAmount is not null))
    {
        throw new InvalidOperationException("Reversal silently introduced a currency snapshot.");
    }
}

static void DuplicateJournalReversalIsRejected()
{
    var originalJournalId = Guid.NewGuid();
    var firstOriginal = CreateDefaultDraft(CreateBalancedLines(10m));
    var first = CreateJournalReversal(originalJournalId, firstOriginal);
    var duplicate = CreateJournalReversal(
        originalJournalId,
        firstOriginal,
        reversalPostingPurpose: "second-technical-reversal-attempt");

    ExpectReversalInvariant(
        "REVERSAL_ORIGINAL_DUPLICATE",
        () => AccountingReversals.ValidatedJournalReversalDraftSet.Create([first, duplicate]));
    ExpectReversalInvariant(
        "REVERSAL_DRAFT_SET_EMPTY",
        () => AccountingReversals.ValidatedJournalReversalDraftSet.Create([]));
    ExpectReversalInvariant(
        "REVERSAL_DRAFT_REQUIRED",
        () => AccountingReversals.ValidatedJournalReversalDraftSet.Create([first, null!]));

    var secondOriginal = CreateConfiguredDraft(
        CreateBalancedLines(5m),
        tenantId: firstOriginal.TenantId,
        companyId: Guid.NewGuid());
    var second = CreateJournalReversal(originalJournalId, secondOriginal);
    var input = new[] { second, first };
    var validated = AccountingReversals.ValidatedJournalReversalDraftSet.Create(input);

    Equal(2, validated.Reversals.Count, "Company-scoped reversal identities collided.");
    var expectedFirstCompanyId = input.Select(item => item.OriginalJournalDraft.CompanyId).Order().First();
    Equal(
        expectedFirstCompanyId,
        validated.Reversals[0].OriginalJournalDraft.CompanyId,
        "Reversal ordering is not deterministic.");
    input[0] = duplicate;
    if (!validated.Reversals.Contains(second))
    {
        throw new InvalidOperationException("Validated reversal set retained a mutable input collection.");
    }

    if (validated.Reversals is IList<AccountingReversals.JournalReversalDraft> list)
    {
        Throws<NotSupportedException>(() => list[0] = duplicate);
    }
}

static void CurrencySnapshotBoundariesAreEnforced()
{
    var context = CreateCurrencyTestContext();

    ExpectCurrencyInvariant(
        "RATE_TENANT_REQUIRED",
        () => CreateExchangeRate(context with { TenantId = Guid.Empty }));
    ExpectCurrencyInvariant(
        "RATE_COMPANY_REQUIRED",
        () => CreateExchangeRate(context with { CompanyId = Guid.Empty }));
    ExpectCurrencyInvariant(
        "RATE_SNAPSHOT_REQUIRED",
        () => CreateExchangeRate(context with { RateSnapshotId = Guid.Empty }));
    ExpectCurrencyInvariant("RATE_VERSION_INVALID", () => CreateExchangeRate(context, version: 0));
    ExpectCurrencyInvariant("RATE_TYPE_REQUIRED", () => CreateExchangeRate(context, rateType: " "));
    ExpectCurrencyInvariant("RATE_SOURCE_REQUIRED", () => CreateExchangeRate(context, source: string.Empty));
    ExpectCurrencyInvariant("RATE_NUMERATOR_INVALID", () => CreateExchangeRate(context, numerator: 0m));
    ExpectCurrencyInvariant("RATE_DENOMINATOR_INVALID", () => CreateExchangeRate(context, denominator: -1m));

    ExpectCurrencyInvariant(
        "ROUNDING_TENANT_REQUIRED",
        () => CreateRoundingPolicy(context with { TenantId = Guid.Empty }));
    ExpectCurrencyInvariant(
        "ROUNDING_COMPANY_REQUIRED",
        () => CreateRoundingPolicy(context with { CompanyId = Guid.Empty }));
    ExpectCurrencyInvariant(
        "ROUNDING_POLICY_REQUIRED",
        () => CreateRoundingPolicy(context with { RoundingPolicyId = Guid.Empty }));
    ExpectCurrencyInvariant("ROUNDING_POLICY_VERSION_INVALID", () => CreateRoundingPolicy(context, version: 0));
    ExpectCurrencyInvariant("ROUNDING_SCALE_INVALID", () => CreateRoundingPolicy(context, scale: -1));
    ExpectCurrencyInvariant("ROUNDING_SCALE_INVALID", () => CreateRoundingPolicy(context, scale: 29));
    ExpectCurrencyInvariant(
        "ROUNDING_MODE_INVALID",
        () => CreateRoundingPolicy(context, mode: (AccountingCurrencies.RoundingMode)99));

    var rate = CreateExchangeRate(context, rateType: " daily ", source: " synthetic-fixture ");
    Equal("daily", rate.RateType, "Rate type was not canonicalized.");
    Equal("synthetic-fixture", rate.Source, "Rate source was not canonicalized.");
}

static void CurrencyConversionIsReproducible()
{
    var context = CreateCurrencyTestContext();
    var rate = CreateExchangeRate(context, numerator: 321m, denominator: 100m);
    var rounding = CreateRoundingPolicy(context, scale: 2, mode: AccountingCurrencies.RoundingMode.ToEven);
    var debit = AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        rate,
        rounding,
        JournalAmount.Create(10.005m, 0m));
    var credit = AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        rate,
        rounding,
        JournalAmount.Create(0m, 10.005m));

    Equal(32.11605m, debit.UnroundedFunctionalAmount, "Unrounded functional debit changed.");
    Equal(32.12m, debit.FunctionalAmount.Debit, "Rounded functional debit changed.");
    Equal(0.00395m, debit.RoundingDifference, "Debit rounding difference changed.");
    Equal(32.12m, credit.FunctionalAmount.Credit, "Credit side was not preserved.");
    Equal(debit.UnroundedFunctionalAmount, credit.UnroundedFunctionalAmount, "Debit/credit conversion differs.");

    var identityRate = CreateExchangeRate(
        context,
        transactionCurrency: CurrencyCode.Create("TRY"),
        functionalCurrency: CurrencyCode.Create("TRY"),
        numerator: 1m,
        denominator: 1m);
    var toEven = AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        identityRate,
        rounding,
        JournalAmount.Create(1.005m, 0m));
    var awayFromZero = AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        identityRate,
        CreateRoundingPolicy(context, scale: 2, mode: AccountingCurrencies.RoundingMode.AwayFromZero),
        JournalAmount.Create(1.005m, 0m));

    Equal(1.00m, toEven.FunctionalAmount.Debit, "To-even midpoint result changed.");
    Equal(1.01m, awayFromZero.FunctionalAmount.Debit, "Away-from-zero midpoint result changed.");
    Equal(1.005m, toEven.TransactionAmount.Debit, "Transaction amount was mutated.");

    ExpectCurrencyInvariant(
        "CURRENCY_POLICY_TENANT_MISMATCH",
        () => AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
            rate,
            CreateRoundingPolicy(context with { TenantId = Guid.NewGuid() }),
            JournalAmount.Create(1m, 0m)));
    ExpectCurrencyInvariant(
        "CURRENCY_POLICY_COMPANY_MISMATCH",
        () => AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
            rate,
            CreateRoundingPolicy(context with { CompanyId = Guid.NewGuid() }),
            JournalAmount.Create(1m, 0m)));
    ExpectCurrencyInvariant(
        "CURRENCY_TRANSACTION_AMOUNT_INVALID",
        () => AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(rate, rounding, default));
    ExpectCurrencyInvariant(
        "CURRENCY_FUNCTIONAL_AMOUNT_ZERO",
        () => AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
            CreateExchangeRate(context, numerator: 1m, denominator: 1000m),
            CreateRoundingPolicy(context, scale: 0),
            JournalAmount.Create(0.01m, 0m)));
    ExpectCurrencyInvariant(
        "CURRENCY_CALCULATION_OVERFLOW",
        () => AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
            CreateExchangeRate(context, numerator: 2m, denominator: 1m),
            rounding,
            JournalAmount.Create(decimal.MaxValue, 0m)));
}

static void JournalCurrencyContextIsEnforced()
{
    var context = CreateCurrencyTestContext();
    var debitSnapshot = CreateCurrencyAmount(context, JournalAmount.Create(10m, 0m));
    var creditSnapshot = CreateCurrencyAmount(context, JournalAmount.Create(0m, 10m));
    var debitLine = CreateCurrencyLine(debitSnapshot);
    var creditLine = CreateCurrencyLine(creditSnapshot);
    var journal = CreateConfiguredDraft(
        [debitLine, creditLine],
        tenantId: context.TenantId,
        companyId: context.CompanyId,
        functionalCurrency: context.FunctionalCurrency);
    var validated = AccountingCurrencies.ValidatedJournalCurrencySet.Create(journal);

    Equal(2, validated.LineAmounts.Count, "Unexpected currency-snapshot count.");
    Equal(debitSnapshot, validated.LineAmounts[0], "Currency snapshot order changed.");
    if (validated.LineAmounts is IList<AccountingCurrencies.JournalCurrencyAmountSnapshot> list)
    {
        Throws<NotSupportedException>(() => list[0] = creditSnapshot);
    }

    ExpectInvariant(
        "JOURNAL_CURRENCY_AMOUNT_MISMATCH",
        () => JournalLineDraft.Create(
            Guid.NewGuid(),
            null,
            JournalAmount.Create(11m, 0m),
            [],
            debitSnapshot));
    ExpectCurrencyInvariant(
        "JOURNAL_CURRENCY_SNAPSHOT_REQUIRED",
        () => AccountingCurrencies.ValidatedJournalCurrencySet.Create(
            CreateConfiguredDraft(
                [JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(10m, 0m)), creditLine],
                tenantId: context.TenantId,
                companyId: context.CompanyId,
                functionalCurrency: context.FunctionalCurrency)));

    var otherTenant = context with { TenantId = Guid.NewGuid() };
    ExpectCurrencyInvariant(
        "JOURNAL_CURRENCY_TENANT_MISMATCH",
        () => ValidateCurrencyJournalWithSnapshots(context, CreateCurrencyAmount(otherTenant, JournalAmount.Create(10m, 0m))));

    var otherCompany = context with { CompanyId = Guid.NewGuid() };
    ExpectCurrencyInvariant(
        "JOURNAL_CURRENCY_COMPANY_MISMATCH",
        () => ValidateCurrencyJournalWithSnapshots(context, CreateCurrencyAmount(otherCompany, JournalAmount.Create(10m, 0m))));

    var otherFunctionalCurrency = context with { FunctionalCurrency = CurrencyCode.Create("USD") };
    ExpectCurrencyInvariant(
        "JOURNAL_FUNCTIONAL_CURRENCY_MISMATCH",
        () => ValidateCurrencyJournalWithSnapshots(
            context,
            CreateCurrencyAmount(otherFunctionalCurrency, JournalAmount.Create(10m, 0m))));
}

static void DimensionSnapshotBoundariesAreEnforced()
{
    var context = CreateDimensionTestContext();

    ExpectDimensionInvariant(
        "DIMENSION_ID_REQUIRED",
        () => AccountingDimensions.DimensionAssignment.Create(Guid.Empty, context.FirstValueId));
    ExpectDimensionInvariant(
        "DIMENSION_VALUE_ID_REQUIRED",
        () => AccountingDimensions.DimensionAssignment.Create(context.FirstDimensionId, Guid.Empty));
    ExpectDimensionInvariant(
        "DIMENSION_TENANT_REQUIRED",
        () => CreateDimensionRequirement(context with { TenantId = Guid.Empty }, []));
    ExpectDimensionInvariant(
        "DIMENSION_COMPANY_REQUIRED",
        () => CreateDimensionRequirement(context with { CompanyId = Guid.Empty }, []));
    ExpectDimensionInvariant(
        "DIMENSION_RULE_VERSION_REQUIRED",
        () => CreateDimensionRequirement(context with { PostingRuleVersionId = Guid.Empty }, []));
    ExpectDimensionInvariant(
        "DIMENSION_REQUIREMENT_VERSION_INVALID",
        () => CreateDimensionRequirement(context, [], version: 0));
    ExpectDimensionInvariant(
        "DIMENSION_REQUIREMENT_DUPLICATE",
        () => CreateDimensionRequirement(context, [context.FirstDimensionId, context.FirstDimensionId]));
    ExpectDimensionInvariant(
        "DIMENSION_ID_REQUIRED",
        () => CreateDimensionRequirement(context, [Guid.Empty]));

    var emptyRequirement = CreateDimensionRequirement(context, []);
    Equal(0, emptyRequirement.RequiredDimensionIds.Count, "A no-dimension rule gained a silent requirement.");
}

static void JournalLineDimensionsAreImmutable()
{
    var context = CreateDimensionTestContext();
    var first = AccountingDimensions.DimensionAssignment.Create(context.FirstDimensionId, context.FirstValueId);
    var second = AccountingDimensions.DimensionAssignment.Create(context.SecondDimensionId, context.SecondValueId);
    var input = new[] { second, first };
    var line = JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(10m, 0m), input);
    var expectedIds = input.Select(assignment => assignment.DimensionId).Order().ToArray();

    Equal(expectedIds[0], line.Dimensions[0].DimensionId, "Dimension ordering is not deterministic.");
    Equal(expectedIds[1], line.Dimensions[1].DimensionId, "Dimension ordering is not deterministic.");

    input[0] = AccountingDimensions.DimensionAssignment.Create(Guid.NewGuid(), Guid.NewGuid());
    if (!line.Dimensions.Contains(second))
    {
        throw new InvalidOperationException("Journal line retained a mutable dimension input collection.");
    }

    if (line.Dimensions is IList<AccountingDimensions.DimensionAssignment> list)
    {
        Throws<NotSupportedException>(() => list[0] = input[0]);
    }

    ExpectInvariant(
        "JOURNAL_DIMENSION_DUPLICATE",
        () => JournalLineDraft.Create(
            Guid.NewGuid(),
            null,
            JournalAmount.Create(10m, 0m),
            [first, AccountingDimensions.DimensionAssignment.Create(context.FirstDimensionId, Guid.NewGuid())]));
    ExpectInvariant(
        "JOURNAL_DIMENSIONS_REQUIRED",
        () => JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(10m, 0m), null));
}

static void RequiredJournalDimensionsAreEnforced()
{
    var context = CreateDimensionTestContext();
    var first = AccountingDimensions.DimensionAssignment.Create(context.FirstDimensionId, context.FirstValueId);
    var second = AccountingDimensions.DimensionAssignment.Create(context.SecondDimensionId, context.SecondValueId);
    var requirement = CreateDimensionRequirement(context, [context.SecondDimensionId, context.FirstDimensionId]);
    var completeJournal = CreateJournalForDimensions(context, [first, second], [second, first]);
    var validated = AccountingDimensions.ValidatedJournalDimensions.Create(completeJournal, requirement);

    Equal(completeJournal, validated.JournalDraft, "Validated dimension result changed the journal draft.");
    Equal(
        context.FirstDimensionId.CompareTo(context.SecondDimensionId) < 0 ? context.FirstDimensionId : context.SecondDimensionId,
        requirement.RequiredDimensionIds[0],
        "Requirement ordering is not deterministic.");

    var incompleteJournal = CreateJournalForDimensions(context, [first, second], [first]);
    ExpectDimensionInvariant(
        "JOURNAL_DIMENSION_REQUIRED",
        () => AccountingDimensions.ValidatedJournalDimensions.Create(incompleteJournal, requirement));
    Equal(1, incompleteJournal.Lines[1].Dimensions.Count, "Failed validation silently inserted a dimension.");

    ExpectDimensionInvariant(
        "JOURNAL_DIMENSION_TENANT_MISMATCH",
        () => AccountingDimensions.ValidatedJournalDimensions.Create(
            completeJournal,
            CreateDimensionRequirement(context with { TenantId = Guid.NewGuid() }, [context.FirstDimensionId])));
    ExpectDimensionInvariant(
        "JOURNAL_DIMENSION_COMPANY_MISMATCH",
        () => AccountingDimensions.ValidatedJournalDimensions.Create(
            completeJournal,
            CreateDimensionRequirement(context with { CompanyId = Guid.NewGuid() }, [context.FirstDimensionId])));
    ExpectDimensionInvariant(
        "JOURNAL_DIMENSION_RULE_VERSION_MISMATCH",
        () => AccountingDimensions.ValidatedJournalDimensions.Create(
            completeJournal,
            CreateDimensionRequirement(
                context with { PostingRuleVersionId = Guid.NewGuid() },
                [context.FirstDimensionId])));

    var noDimensionJournal = CreateJournalForDimensions(context, [], []);
    AccountingDimensions.ValidatedJournalDimensions.Create(
        noDimensionJournal,
        CreateDimensionRequirement(context, []));
}

static TreasuryPaymentTestContext CreateTreasuryPaymentTestContext() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        TreasuryPayments.TreasuryCurrencyCode.Create("GBP"));

static TreasuryPayments.SameCurrencyPaymentRateSnapshot CreatePaymentRate(
    TreasuryPaymentTestContext context,
    long version = 1,
    string rateType = "identity",
    string source = "technical-fixture",
    TreasuryPayments.TreasuryCurrencyCode? transactionCurrency = null,
    TreasuryPayments.TreasuryCurrencyCode? functionalCurrency = null,
    decimal numerator = 1m,
    decimal denominator = 1m) =>
    TreasuryPayments.SameCurrencyPaymentRateSnapshot.Create(
        context.TenantId,
        context.CompanyId,
        context.RateSnapshotId,
        version,
        transactionCurrency ?? context.Currency,
        functionalCurrency ?? context.Currency,
        rateType,
        source,
        new DateOnly(2026, 8, 24),
        numerator,
        denominator);

static TreasuryPayments.ValidatedPaymentEconomicEventDraft CreatePaymentDraft(
    TreasuryPaymentTestContext context,
    Guid? paymentId = null,
    TreasuryPayments.PaymentDirection direction = TreasuryPayments.PaymentDirection.Outgoing,
    decimal transactionAmount = 100m,
    decimal functionalAmount = 100m,
    DateTimeOffset? recordedAt = null,
    string sourceType = "technical.payment-source",
    Guid? sourceEventId = null,
    string postingPurpose = "technical-cash-movement",
    TreasuryPayments.SameCurrencyPaymentRateSnapshot? rateSnapshot = null,
    DateOnly? effectiveDate = null) =>
    TreasuryPayments.ValidatedPaymentEconomicEventDraft.Create(
        paymentId ?? Guid.NewGuid(),
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        context.TreasuryAccountId,
        direction,
        transactionAmount,
        functionalAmount,
        effectiveDate ?? new DateOnly(2026, 8, 24),
        recordedAt ?? new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero),
        sourceType,
        sourceEventId ?? Guid.NewGuid(),
        postingPurpose,
        rateSnapshot ?? CreatePaymentRate(context));

static void ExpectPaymentInvariant(string expectedCode, Action action)
{
    var exception = Throws<TreasuryPayments.PaymentInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected payment invariant code.");
}

static TreasuryStatementTestContext CreateStatementTestContext() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        TreasuryPayments.TreasuryCurrencyCode.Create("GBP"));

static TreasuryStatements.StatementLineExternalIdentity CreateStatementIdentity(
    TreasuryStatementTestContext context,
    string sourceSystem = "technical-bank-profile",
    string identityKind = "bank-reference",
    string externalKey = "REFERENCE-001") =>
    TreasuryStatements.StatementLineExternalIdentity.Create(
        context.TenantId,
        context.CompanyId,
        context.TreasuryAccountId,
        sourceSystem,
        identityKind,
        externalKey);

static TreasuryStatements.ValidatedStatementLineDraft CreateStatementLine(
    TreasuryStatementTestContext context,
    Guid? statementLineId = null,
    decimal signedAmount = 100m,
    DateOnly? bookingDate = null,
    DateOnly? valueDate = null,
    DateTimeOffset? recordedAt = null,
    string rawObjectSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    long parserVersion = 1,
    TreasuryStatements.StatementLineExternalIdentity? externalIdentity = null) =>
    TreasuryStatements.ValidatedStatementLineDraft.Create(
        statementLineId ?? Guid.NewGuid(),
        context.StatementImportId,
        externalIdentity ?? CreateStatementIdentity(context, externalKey: Guid.NewGuid().ToString("N")),
        context.Currency,
        signedAmount,
        bookingDate ?? new DateOnly(2026, 8, 24),
        valueDate ?? new DateOnly(2026, 8, 25),
        recordedAt ?? new DateTimeOffset(2026, 8, 24, 11, 0, 0, TimeSpan.Zero),
        rawObjectSha256,
        parserVersion);

static TreasuryReconciliation.InternalMovementCapacitySnapshot CreateMovementCapacity(
    TreasuryStatementTestContext context,
    decimal usableAmount,
    Guid? movementId = null,
    long version = 1,
    TreasuryPayments.PaymentDirection direction = TreasuryPayments.PaymentDirection.Outgoing,
    TreasuryPayments.TreasuryCurrencyCode? currency = null) =>
    TreasuryReconciliation.InternalMovementCapacitySnapshot.Create(
        context.TenantId,
        context.CompanyId,
        context.TreasuryAccountId,
        movementId ?? Guid.NewGuid(),
        version,
        direction,
        currency ?? context.Currency,
        usableAmount);

static TreasuryReconciliation.ReconciliationMatchDraft CreateReconciliationMatch(
    TreasuryStatements.ValidatedStatementLineDraft statementLine,
    TreasuryReconciliation.InternalMovementCapacitySnapshot movement,
    decimal matchedAmount) =>
    TreasuryReconciliation.ReconciliationMatchDraft.Create(statementLine, movement, matchedAmount);

static TreasuryReconciliation.ValidatedReconciliationProposal CreateReconciliationProposal(
    TreasuryStatementTestContext context,
    IEnumerable<TreasuryReconciliation.ReconciliationMatchDraft?> matches) =>
    TreasuryReconciliation.ValidatedReconciliationProposal.Create(
        Guid.NewGuid(),
        context.TenantId,
        context.CompanyId,
        context.TreasuryAccountId,
        context.Currency,
        matches);

static void ExpectStatementInvariant(string expectedCode, Action action)
{
    var exception = Throws<TreasuryStatements.StatementInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected statement invariant code.");
}

static void ExpectReconciliationInvariant(string expectedCode, Action action)
{
    var exception = Throws<TreasuryReconciliation.ReconciliationInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected reconciliation invariant code.");
}

static ReportTestContext CreateReportTestContext() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        ReportingControlAccounts.ReportCurrencyCode.Create("GBP"));

static ReportingControlAccounts.ReportDimensionSlice CreateReportDimensions(
    params (string DimensionCode, string ValueCode)[] assignments) =>
    ReportingControlAccounts.ReportDimensionSlice.Create(
        assignments.Select(
            assignment => ReportingControlAccounts.ReportDimensionAssignment.Create(
                assignment.DimensionCode,
                assignment.ValueCode)));

static ReportingControlAccounts.FinancialReportSlice CreateReportSlice(
    ReportTestContext context,
    string reportCode = "party-control-account",
    long definitionVersion = 1,
    DateOnly? effectiveAsOf = null,
    DateTimeOffset? dataCutoffAt = null,
    DateTimeOffset? generatedAt = null,
    ReportingControlAccounts.ReportDimensionSlice? dimensions = null) =>
    ReportingControlAccounts.FinancialReportSlice.Create(
        context.TenantId,
        context.CompanyId,
        reportCode,
        definitionVersion,
        effectiveAsOf ?? new DateOnly(2026, 8, 24),
        dataCutoffAt ?? new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero),
        generatedAt ?? new DateTimeOffset(2026, 8, 24, 10, 1, 0, TimeSpan.Zero),
        context.ProjectionGenerationId,
        context.Currency,
        dimensions ?? CreateReportDimensions());

static ReportingControlAccounts.ControlAccountBalanceSnapshot CreateControlBalance(
    ReportTestContext context,
    ReportingControlAccounts.LedgerSide ledgerSide,
    Guid? snapshotId = null,
    decimal opening = 100m,
    decimal debits = 50m,
    decimal credits = 20m,
    decimal closing = 130m,
    long rowCount = 1,
    string sourceChecksumSha256 = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
    ReportingControlAccounts.FinancialReportSlice? reportSlice = null) =>
    ReportingControlAccounts.ControlAccountBalanceSnapshot.Create(
        snapshotId ?? Guid.NewGuid(),
        context.ControlAccountId,
        ledgerSide,
        opening,
        debits,
        credits,
        closing,
        rowCount,
        sourceChecksumSha256,
        reportSlice ?? CreateReportSlice(context));

static void ReconcileWithChangedSlice(
    ReportTestContext originalContext,
    ReportingControlAccounts.ControlAccountBalanceSnapshot subledger,
    ReportingControlAccounts.FinancialReportSlice changedSlice,
    ReportTestContext? changedBalanceContext = null)
{
    var generalLedger = CreateControlBalance(
        changedBalanceContext ?? originalContext,
        ReportingControlAccounts.LedgerSide.GeneralLedger,
        reportSlice: changedSlice);
    ReportingControlAccounts.ControlAccountReconciliationResult.Create(
        Guid.NewGuid(),
        subledger,
        generalLedger);
}

static void ExpectReportingInvariant(string expectedCode, Action action)
{
    var exception = Throws<ReportingControlAccounts.ReportingInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected reporting invariant code.");
}

static ReportingParty.PartyStatementEventSnapshot CreatePartyStatementEvent(
    ReportTestContext context,
    Guid? eventId = null,
    ReportingParty.PartyStatementEventKind kind = ReportingParty.PartyStatementEventKind.OpenItem,
    string sourceType = "party.open-item",
    Guid? sourceEventId = null,
    Guid? dueScheduleLineId = null,
    Guid? paymentId = null,
    decimal exposureEffect = 100m,
    DateOnly? effectiveDate = null,
    long sequenceKey = 1,
    DateTimeOffset? recordedAt = null) =>
    ReportingParty.PartyStatementEventSnapshot.Create(
        eventId ?? Guid.NewGuid(),
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        context.ControlAccountId,
        context.Currency,
        kind,
        sourceType,
        sourceEventId ?? Guid.NewGuid(),
        dueScheduleLineId ?? Guid.NewGuid(),
        paymentId,
        exposureEffect,
        effectiveDate ?? new DateOnly(2026, 8, 24),
        sequenceKey,
        recordedAt ?? new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero));

static ReportingParty.ValidatedPartyStatement CreatePartyStatement(
    ReportTestContext context,
    IEnumerable<ReportingParty.PartyStatementEventSnapshot?> events,
    decimal openingExposure = 0m,
    ReportingParty.PartyBalanceSide balanceSide = ReportingParty.PartyBalanceSide.Receivable,
    ReportingControlAccounts.FinancialReportSlice? reportSlice = null) =>
    ReportingParty.ValidatedPartyStatement.Create(
        Guid.NewGuid(),
        context.PartyAccountId,
        context.ControlAccountId,
        balanceSide,
        openingExposure,
        reportSlice ?? CreateReportSlice(context),
        events);

static ReportingParty.CalendarDayAgingPolicySnapshot CreateAgingPolicy(
    ReportTestContext context,
    params ReportingParty.CalendarDayAgingBucket[] buckets) =>
    ReportingParty.CalendarDayAgingPolicySnapshot.Create(
        context.TenantId,
        context.CompanyId,
        context.AgingPolicyId,
        1,
        buckets.Length == 0
            ?
            [
                ReportingParty.CalendarDayAgingBucket.Create("future", int.MinValue, -1),
                ReportingParty.CalendarDayAgingBucket.Create("current", 0, 0),
                ReportingParty.CalendarDayAgingBucket.Create("overdue", 1, int.MaxValue),
            ]
            : buckets);

static ReportingParty.OpenItemAgingSnapshot CreateAgingItem(
    ReportTestContext context,
    decimal remainingAmount,
    decimal originalAmount = 100m,
    Guid? openItemId = null,
    DateOnly? dueDate = null,
    DateOnly? effectiveAsOf = null,
    DateTimeOffset? dataCutoffAt = null,
    bool isDisputed = false,
    bool isBlocked = false) =>
    ReportingParty.OpenItemAgingSnapshot.Create(
        openItemId ?? Guid.NewGuid(),
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        context.ControlAccountId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        context.Currency,
        originalAmount,
        remainingAmount,
        dueDate ?? new DateOnly(2026, 8, 24),
        effectiveAsOf ?? new DateOnly(2026, 8, 24),
        dataCutoffAt ?? new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero),
        isDisputed,
        isBlocked);

static ReportingParty.ValidatedPartyAgingReport CreatePartyAging(
    ReportTestContext context,
    IEnumerable<ReportingParty.OpenItemAgingSnapshot?> items,
    ReportingParty.CalendarDayAgingPolicySnapshot? policy = null,
    ReportingParty.PartyBalanceSide balanceSide = ReportingParty.PartyBalanceSide.Receivable,
    ReportingControlAccounts.FinancialReportSlice? reportSlice = null) =>
    ReportingParty.ValidatedPartyAgingReport.Create(
        Guid.NewGuid(),
        context.PartyAccountId,
        context.ControlAccountId,
        balanceSide,
        reportSlice ?? CreateReportSlice(context),
        policy ?? CreateAgingPolicy(context),
        items);

static DueScheduleTestContext CreateDueScheduleTestContext() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        PartyAllocations.AllocationCurrencyCode.Create("GBP"));

static PartyDueSchedules.DueScheduleLine CreateDueLine(
    DueScheduleTestContext context,
    decimal originalAmount,
    Guid? lineId = null,
    DateOnly? dueDate = null,
    long paymentTermVersion = 1) =>
    PartyDueSchedules.DueScheduleLine.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        context.SourceEventId,
        lineId ?? Guid.NewGuid(),
        context.Currency,
        originalAmount,
        dueDate ?? new DateOnly(2026, 9, 1),
        context.PaymentTermSnapshotId,
        paymentTermVersion,
        context.ControlAccountId);

static PartyDueSchedules.ValidatedDueSchedule CreateDueSchedule(
    DueScheduleTestContext context,
    decimal sourceOriginalAmount,
    IEnumerable<PartyDueSchedules.DueScheduleLine?>? lines) =>
    PartyDueSchedules.ValidatedDueSchedule.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        context.SourceEventId,
        context.Currency,
        sourceOriginalAmount,
        lines);

static PartyOpenItems.OpenItemImpactEvent CreateOpenItemEvent(
    PartyDueSchedules.DueScheduleLine dueLine,
    PartyOpenItems.OpenItemImpactKind kind,
    decimal amount,
    DateOnly? effectiveDate = null,
    DateTimeOffset? recordedAt = null,
    Guid? reversesEventId = null,
    Guid? eventId = null,
    Guid? tenantId = null,
    Guid? companyId = null,
    Guid? partyAccountId = null,
    Guid? dueLineId = null,
    PartyAllocations.AllocationCurrencyCode? currency = null,
    Guid? paymentId = null,
    string sourceType = "party.test-impact",
    long sourceVersion = 1,
    string sourcePostingPurpose = "party.test-impact.post")
{
    var resolvedPaymentId = kind is PartyOpenItems.OpenItemImpactKind.Allocation or
        PartyOpenItems.OpenItemImpactKind.Unallocation
        ? paymentId ?? Guid.NewGuid()
        : paymentId;

    return PartyOpenItems.OpenItemImpactEvent.Create(
        eventId ?? Guid.NewGuid(),
        tenantId ?? dueLine.TenantId,
        companyId ?? dueLine.CompanyId,
        partyAccountId ?? dueLine.PartyAccountId,
        dueLineId ?? dueLine.DueScheduleLineId,
        resolvedPaymentId,
        currency ?? dueLine.Currency,
        sourceType,
        sourceVersion,
        sourcePostingPurpose,
        kind,
        amount,
        effectiveDate ?? new DateOnly(2026, 8, 2),
        recordedAt ?? UtcAt(2026, 8, 2),
        reversesEventId);
}

static PartyOpenItems.DerivedOpenItemSnapshot DeriveOpenItem(
    PartyDueSchedules.DueScheduleLine dueLine,
    IEnumerable<PartyOpenItems.OpenItemImpactEvent?>? events,
    DateOnly? asOfEffectiveDate = null,
    DateTimeOffset? recordedCutoff = null) =>
    PartyOpenItems.DerivedOpenItemSnapshot.Create(
        dueLine,
        asOfEffectiveDate ?? new DateOnly(2026, 12, 31),
        recordedCutoff ?? UtcAt(2026, 12, 31),
        events);

static PartyAllocations.DerivedPaymentAllocationSnapshot DerivePaymentAllocations(
    PartyAllocations.PaymentAllocationCapacity payment,
    IEnumerable<PartyOpenItems.OpenItemImpactEvent?>? events,
    DateOnly? asOfEffectiveDate = null,
    DateTimeOffset? recordedCutoff = null) =>
    PartyAllocations.DerivedPaymentAllocationSnapshot.Create(
        payment,
        asOfEffectiveDate ?? new DateOnly(2026, 12, 31),
        recordedCutoff ?? UtcAt(2026, 12, 31),
        events);

static void ExpectPaymentAllocationScopeMismatch(
    PartyAllocations.PaymentAllocationCapacity payment,
    PartyDueSchedules.DueScheduleLine dueLine,
    string expectedCode,
    Guid? tenantId = null,
    Guid? companyId = null,
    Guid? partyAccountId = null,
    Guid? paymentId = null,
    PartyAllocations.AllocationCurrencyCode? currency = null)
{
    var allocation = CreateOpenItemEvent(
        dueLine,
        PartyOpenItems.OpenItemImpactKind.Allocation,
        1m,
        tenantId: tenantId,
        companyId: companyId,
        partyAccountId: partyAccountId,
        currency: currency,
        paymentId: paymentId ?? payment.PaymentId);
    ExpectPartyOpenItemInvariant(expectedCode, () => DerivePaymentAllocations(payment, [allocation]));
}

static void ExpectOpenItemScopeMismatch(
    PartyDueSchedules.DueScheduleLine dueLine,
    PartyOpenItems.OpenItemImpactEvent source,
    string expectedCode,
    Guid? tenantId = null,
    Guid? companyId = null,
    Guid? partyAccountId = null,
    Guid? dueLineId = null,
    PartyAllocations.AllocationCurrencyCode? currency = null)
{
    var changed = CreateOpenItemEvent(
        dueLine,
        source.Kind,
        source.Amount,
        source.EffectiveDate,
        source.RecordedAt,
        source.ReversesEventId,
        source.EventId,
        tenantId,
        companyId,
        partyAccountId,
        dueLineId,
        currency,
        source.PaymentId);
    ExpectPartyOpenItemInvariant(expectedCode, () => DeriveOpenItem(dueLine, [changed]));
}

static DateTimeOffset UtcAt(int year, int month, int day) =>
    new(year, month, day, 9, 0, 0, TimeSpan.Zero);

static void ExpectPartyOpenItemInvariant(string expectedCode, Action action)
{
    var exception = Throws<PartyDueSchedules.PartyOpenItemInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected party open-item invariant code.");
}

static AccountingReversals.JournalReversalDraft CreateJournalReversal(
    Guid originalJournalId,
    ValidatedJournalDraft original,
    Guid? reversalPostingRuleVersionId = null,
    string reversalSourceType = "accounting.reversal-test",
    string reversalPostingPurpose = "technical-exact-reversal",
    DateTimeOffset? recordedAt = null) =>
    AccountingReversals.JournalReversalDraft.Create(
        originalJournalId,
        original,
        reversalPostingRuleVersionId ?? Guid.NewGuid(),
        reversalSourceType,
        reversalPostingPurpose,
        new DateOnly(2026, 8, 22),
        recordedAt ?? new DateTimeOffset(2026, 8, 22, 10, 30, 0, TimeSpan.Zero));

static void ExpectReversalInvariant(string expectedCode, Action action)
{
    var exception = Throws<AccountingReversals.ReversalInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected reversal invariant code.");
}

static CurrencyTestContext CreateCurrencyTestContext() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        CurrencyCode.Create("EUR"),
        CurrencyCode.Create("TRY"));

static AccountingCurrencies.ExchangeRateSnapshot CreateExchangeRate(
    CurrencyTestContext context,
    long version = 1,
    string rateType = "technical-test",
    string source = "synthetic-fixture",
    CurrencyCode? transactionCurrency = null,
    CurrencyCode? functionalCurrency = null,
    decimal numerator = 1m,
    decimal denominator = 1m) =>
    AccountingCurrencies.ExchangeRateSnapshot.Create(
        context.TenantId,
        context.CompanyId,
        context.RateSnapshotId,
        version,
        transactionCurrency ?? context.TransactionCurrency,
        functionalCurrency ?? context.FunctionalCurrency,
        rateType,
        source,
        new DateOnly(2026, 8, 22),
        numerator,
        denominator);

static AccountingCurrencies.RoundingPolicySnapshot CreateRoundingPolicy(
    CurrencyTestContext context,
    long version = 1,
    int scale = 4,
    AccountingCurrencies.RoundingMode mode = AccountingCurrencies.RoundingMode.ToEven) =>
    AccountingCurrencies.RoundingPolicySnapshot.Create(
        context.TenantId,
        context.CompanyId,
        context.RoundingPolicyId,
        version,
        scale,
        mode);

static AccountingCurrencies.JournalCurrencyAmountSnapshot CreateCurrencyAmount(
    CurrencyTestContext context,
    JournalAmount transactionAmount) =>
    AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        CreateExchangeRate(context),
        CreateRoundingPolicy(context),
        transactionAmount);

static JournalLineDraft CreateCurrencyLine(AccountingCurrencies.JournalCurrencyAmountSnapshot currencyAmount) =>
    JournalLineDraft.Create(
        Guid.NewGuid(),
        null,
        currencyAmount.FunctionalAmount,
        [],
        currencyAmount);

static void ValidateCurrencyJournalWithSnapshots(
    CurrencyTestContext journalContext,
    AccountingCurrencies.JournalCurrencyAmountSnapshot debitSnapshot)
{
    var creditSnapshot = CreateCurrencyAmount(
        debitSnapshot.ExchangeRate.TenantId == journalContext.TenantId &&
        debitSnapshot.ExchangeRate.CompanyId == journalContext.CompanyId &&
        debitSnapshot.ExchangeRate.FunctionalCurrency == journalContext.FunctionalCurrency
            ? journalContext
            : journalContext with
            {
                TenantId = debitSnapshot.ExchangeRate.TenantId,
                CompanyId = debitSnapshot.ExchangeRate.CompanyId,
                FunctionalCurrency = debitSnapshot.ExchangeRate.FunctionalCurrency,
            },
        JournalAmount.Create(0m, 10m));
    var journal = CreateConfiguredDraft(
        [CreateCurrencyLine(debitSnapshot), CreateCurrencyLine(creditSnapshot)],
        tenantId: journalContext.TenantId,
        companyId: journalContext.CompanyId,
        functionalCurrency: journalContext.FunctionalCurrency);

    AccountingCurrencies.ValidatedJournalCurrencySet.Create(journal);
}

static void ExpectCurrencyInvariant(string expectedCode, Action action)
{
    var exception = Throws<AccountingCurrencies.CurrencyInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected currency invariant code.");
}

static DimensionTestContext CreateDimensionTestContext() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid());

static AccountingDimensions.PostingDimensionRequirementSnapshot CreateDimensionRequirement(
    DimensionTestContext context,
    IEnumerable<Guid> requiredDimensionIds,
    long version = 1) =>
    AccountingDimensions.PostingDimensionRequirementSnapshot.Create(
        context.TenantId,
        context.CompanyId,
        context.PostingRuleVersionId,
        version,
        requiredDimensionIds);

static ValidatedJournalDraft CreateJournalForDimensions(
    DimensionTestContext context,
    IEnumerable<AccountingDimensions.DimensionAssignment> debitDimensions,
    IEnumerable<AccountingDimensions.DimensionAssignment> creditDimensions) =>
    CreateConfiguredDraft(
        [
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(10m, 0m), debitDimensions),
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 10m), creditDimensions),
        ],
        tenantId: context.TenantId,
        companyId: context.CompanyId,
        postingRuleVersionId: context.PostingRuleVersionId);

static void ExpectDimensionInvariant(string expectedCode, Action action)
{
    var exception = Throws<AccountingDimensions.DimensionInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected dimension invariant code.");
}

static AccountTestContext CreateAccountTestContext() =>
    new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

static AccountingAccounts.AccountPostingSnapshot CreateAccountSnapshot(
    AccountTestContext context,
    Guid accountId,
    AccountingAccounts.AccountKind kind = AccountingAccounts.AccountKind.Posting,
    bool isActive = true,
    long version = 1) =>
    AccountingAccounts.AccountPostingSnapshot.Create(
        context.TenantId,
        context.CompanyId,
        accountId,
        context.ChartVersionId,
        kind,
        isActive,
        version);

static ValidatedJournalDraft CreateJournalForAccounts(AccountTestContext context) =>
    CreateConfiguredDraft(
        [
            JournalLineDraft.Create(context.DebitAccountId, null, JournalAmount.Create(10m, 0m)),
            JournalLineDraft.Create(context.CreditAccountId, null, JournalAmount.Create(0m, 10m)),
        ],
        tenantId: context.TenantId,
        companyId: context.CompanyId);

static AccountingAccounts.ValidatedJournalAccountSet ValidateJournalAccounts(
    ValidatedJournalDraft journal,
    AccountTestContext context,
    IEnumerable<AccountingAccounts.AccountPostingSnapshot> accounts) =>
    AccountingAccounts.ValidatedJournalAccountSet.Create(journal, context.ChartVersionId, accounts);

static void ExpectAccountInvariant(string expectedCode, Action action)
{
    var exception = Throws<AccountingAccounts.AccountInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected account invariant code.");
}

static PeriodTestContext CreatePeriodTestContext() =>
    new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

static AccountingPeriods.PeriodLockSnapshot CreatePeriodLock(
    PeriodTestContext context,
    AccountingPeriods.PeriodLockScope scope,
    AccountingPeriods.PeriodCloseStage stage = AccountingPeriods.PeriodCloseStage.Open,
    long version = 1) =>
    AccountingPeriods.PeriodLockSnapshot.Create(
        context.TenantId,
        context.CompanyId,
        context.PeriodId,
        scope,
        stage,
        version);

static AccountingPeriods.ValidatedPeriodLockSet CreatePeriodLockSet(
    PeriodTestContext context,
    IEnumerable<AccountingPeriods.PeriodLockSnapshot> locks) =>
    AccountingPeriods.ValidatedPeriodLockSet.Create(
        context.TenantId,
        context.CompanyId,
        context.PeriodId,
        locks);

static void ExpectPeriodInvariant(string expectedCode, Action action)
{
    var exception = Throws<AccountingPeriods.PeriodInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected period invariant code.");
}

static AllocationTestContext CreateAllocationTestContext() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        PartyAllocations.AllocationCurrencyCode.Create("GBP"));

static PartyAllocations.PaymentAllocationCapacity CreatePayment(
    AllocationTestContext context,
    decimal usableAmount) =>
    PartyAllocations.PaymentAllocationCapacity.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        Guid.NewGuid(),
        context.Currency,
        usableAmount);

static PartyAllocations.OpenItemAllocationCapacity CreateOpenItem(
    AllocationTestContext context,
    decimal remainingAmount) =>
    PartyAllocations.OpenItemAllocationCapacity.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        Guid.NewGuid(),
        context.Currency,
        remainingAmount);

static PartyAllocations.ValidatedSameCurrencyAllocationPlan CreateAllocationPlan(
    PartyAllocations.PaymentAllocationCapacity payment,
    PartyAllocations.OpenItemAllocationCapacity openItem,
    decimal amount) =>
    PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
        payment,
        [PartyAllocations.AllocationPlanLine.Create(openItem, amount)]);

static void ExpectAllocationInvariant(string expectedCode, Action action)
{
    var exception = Throws<PartyAllocations.AllocationInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected allocation invariant code.");
}

static JournalLineDraft[] CreateBalancedLines(decimal amount) =>
[
    JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(amount, 0m)),
    JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, amount)),
];

static PostingCandidateFixture CreatePostingCandidateFixture(Guid? tenantId = null, Guid? companyId = null)
{
    Guid tenant = tenantId ?? Guid.NewGuid();
    Guid company = companyId ?? Guid.NewGuid();
    Guid actorId = Guid.NewGuid();
    Guid chartVersionId = Guid.NewGuid();
    Guid postingRuleVersionId = Guid.NewGuid();
    Guid debitAccountId = Guid.NewGuid();
    Guid creditAccountId = Guid.NewGuid();
    Guid dimensionId = Guid.NewGuid();
    var exchangeRate = AccountingCurrencies.ExchangeRateSnapshot.Create(
        tenant,
        company,
        Guid.NewGuid(),
        1,
        CurrencyCode.Create("GBP"),
        CurrencyCode.Create("GBP"),
        "spot",
        "technical-fixture",
        new DateOnly(2026, 8, 25),
        1m,
        1m);
    var rounding = AccountingCurrencies.RoundingPolicySnapshot.Create(
        tenant,
        company,
        Guid.NewGuid(),
        1,
        4,
        AccountingCurrencies.RoundingMode.ToEven);
    var debitCurrency = AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        exchangeRate,
        rounding,
        JournalAmount.Create(10m, 0m));
    var creditCurrency = AccountingCurrencies.JournalCurrencyAmountSnapshot.Create(
        exchangeRate,
        rounding,
        JournalAmount.Create(0m, 10m));
    AccountingDimensions.DimensionAssignment debitDimension =
        AccountingDimensions.DimensionAssignment.Create(dimensionId, Guid.NewGuid());
    AccountingDimensions.DimensionAssignment creditDimension =
        AccountingDimensions.DimensionAssignment.Create(dimensionId, Guid.NewGuid());
    ValidatedJournalDraft draft = CreateConfiguredDraft(
        [
            JournalLineDraft.Create(debitAccountId, null, debitCurrency.FunctionalAmount, [debitDimension], debitCurrency),
            JournalLineDraft.Create(creditAccountId, null, creditCurrency.FunctionalAmount, [creditDimension], creditCurrency),
        ],
        tenantId: tenant,
        companyId: company,
        postingRuleVersionId: postingRuleVersionId,
        functionalCurrency: CurrencyCode.Create("GBP"),
        effectiveDate: new DateOnly(2026, 8, 25));
    AccountingAccounts.ValidatedJournalAccountSet accounts = AccountingAccounts.ValidatedJournalAccountSet.Create(
        draft,
        chartVersionId,
        [
            AccountingAccounts.AccountPostingSnapshot.Create(
                tenant, company, debitAccountId, chartVersionId, AccountingAccounts.AccountKind.Posting, true, 1),
            AccountingAccounts.AccountPostingSnapshot.Create(
                tenant, company, creditAccountId, chartVersionId, AccountingAccounts.AccountKind.Posting, true, 1),
        ]);
    AccountingDimensions.ValidatedJournalDimensions dimensions = AccountingDimensions.ValidatedJournalDimensions.Create(
        draft,
        AccountingDimensions.PostingDimensionRequirementSnapshot.Create(
            tenant,
            company,
            postingRuleVersionId,
            1,
            [dimensionId]));
    AccountingCurrencies.ValidatedJournalCurrencySet currencies =
        AccountingCurrencies.ValidatedJournalCurrencySet.Create(draft);
    Guid periodId = Guid.NewGuid();
    AccountingPeriods.ValidatedPeriodLockSet periodLocks = AccountingPeriods.ValidatedPeriodLockSet.Create(
        tenant,
        company,
        periodId,
        [
            AccountingPeriods.PeriodLockSnapshot.Create(
                tenant, company, periodId, AccountingPeriods.PeriodLockScope.GeneralLedger,
                AccountingPeriods.PeriodCloseStage.Open, 1),
            AccountingPeriods.PeriodLockSnapshot.Create(
                tenant, company, periodId, AccountingPeriods.PeriodLockScope.HardLegal,
                AccountingPeriods.PeriodCloseStage.Open, 1),
        ]);
    return new PostingCandidateFixture(actorId, draft, accounts, dimensions, currencies, periodLocks);
}

static ValidatedJournalDraft CreateDefaultDraft(
    params JournalLineDraft[] lines) => CreateConfiguredDraft(lines);

static ValidatedJournalDraft CreateConfiguredDraft(
    IEnumerable<JournalLineDraft> lines,
    Guid? tenantId = null,
    Guid? companyId = null,
    Guid? sourceEventId = null,
    Guid? postingRuleVersionId = null,
    string sourceType = "synthetic.accounting-event",
    string postingPurpose = "technical-invariant-spike",
    DateTimeOffset? recordedAt = null,
    CurrencyCode? functionalCurrency = null,
    DateOnly? effectiveDate = null) =>
    ValidatedJournalDraft.Create(
        tenantId ?? Guid.NewGuid(),
        companyId ?? Guid.NewGuid(),
        sourceEventId ?? Guid.NewGuid(),
        postingRuleVersionId ?? Guid.NewGuid(),
        sourceType,
        postingPurpose,
        effectiveDate ?? new DateOnly(2026, 8, 21),
        recordedAt ?? new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.Zero),
        functionalCurrency ?? CurrencyCode.Create("TRY"),
        lines);

static void ExpectInvariant(string expectedCode, Action action)
{
    var exception = Throws<JournalInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected invariant code.");
}

static TException Throws<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
    {
        throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
    }
}

internal sealed record PostingCandidateFixture(
    Guid ActorId,
    ValidatedJournalDraft Draft,
    AccountingAccounts.ValidatedJournalAccountSet Accounts,
    AccountingDimensions.ValidatedJournalDimensions Dimensions,
    AccountingCurrencies.ValidatedJournalCurrencySet Currencies,
    AccountingPeriods.ValidatedPeriodLockSet PeriodLocks);

internal sealed record AllocationTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid PartyAccountId,
    PartyAllocations.AllocationCurrencyCode Currency);

internal sealed record TreasuryPaymentTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid PartyAccountId,
    Guid TreasuryAccountId,
    Guid RateSnapshotId,
    TreasuryPayments.TreasuryCurrencyCode Currency);

internal sealed record TreasuryStatementTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid TreasuryAccountId,
    Guid StatementImportId,
    TreasuryPayments.TreasuryCurrencyCode Currency);

internal sealed record ReportTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid PartyAccountId,
    Guid ControlAccountId,
    Guid ProjectionGenerationId,
    Guid AgingPolicyId,
    ReportingControlAccounts.ReportCurrencyCode Currency);

internal sealed record DueScheduleTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid PartyAccountId,
    Guid SourceEventId,
    Guid PaymentTermSnapshotId,
    Guid ControlAccountId,
    PartyAllocations.AllocationCurrencyCode Currency);

internal sealed record PeriodTestContext(Guid TenantId, Guid CompanyId, Guid PeriodId);

internal sealed record AccountTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid ChartVersionId,
    Guid DebitAccountId,
    Guid CreditAccountId);

internal sealed record CurrencyTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid RateSnapshotId,
    Guid RoundingPolicyId,
    CurrencyCode TransactionCurrency,
    CurrencyCode FunctionalCurrency);

internal sealed record DimensionTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid PostingRuleVersionId,
    Guid FirstDimensionId,
    Guid FirstValueId,
    Guid SecondDimensionId,
    Guid SecondValueId);
