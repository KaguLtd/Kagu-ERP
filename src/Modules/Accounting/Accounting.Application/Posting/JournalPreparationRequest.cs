using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Journals;

namespace KaguERP.Modules.Accounting.Application.Posting;

public sealed record JournalPreparationRequest(
    ExecutionScope Scope,
    RequestAuditContext AuditContext,
    ValidatedJournalDraft Draft,
    Guid ChartOfAccountsVersionId,
    Guid ReservationId,
    Guid JournalDraftId,
    Guid AuditEventId,
    Guid OutboxEventId);

public sealed record JournalPreparationResult(
    Guid ReservationId,
    Guid JournalDraftId,
    Guid PeriodId,
    string DraftHash,
    bool ReservationCreated,
    bool DraftCreated);

public sealed record JournalPreparationCommand(
    ExecutionScope Scope,
    RequestAuditContext AuditContext,
    JournalPostingIdentity SourceIdentity,
    long ExpectedSourceVersion,
    Guid ReservationId,
    Guid JournalDraftId,
    Guid AuditEventId,
    Guid OutboxEventId);

public sealed record CanonicalJournalPreparationSource(
    ValidatedJournalDraft Draft,
    Guid ChartOfAccountsVersionId,
    long SourceVersion);
