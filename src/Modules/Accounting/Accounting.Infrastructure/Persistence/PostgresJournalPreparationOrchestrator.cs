using KaguERP.Modules.Accounting.Application.Posting;
using KaguERP.Modules.Accounting.Domain.Accounts;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Domain.Dimensions;
using KaguERP.Modules.Accounting.Domain.Periods;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public delegate ValueTask JournalPreparationAuditAppender(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    JournalPreparationRequest request,
    Guid persistedJournalDraftId,
    CancellationToken cancellationToken);

public delegate ValueTask<bool> JournalPreparationOutboxAppender(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    JournalPreparationRequest request,
    Guid persistedJournalDraftId,
    Guid reservationId,
    Guid periodId,
    CancellationToken cancellationToken);

public delegate ValueTask<CanonicalJournalPreparationSource> JournalPreparationSourceLoader(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    JournalPreparationCommand command,
    CancellationToken cancellationToken);

public static class PostgresJournalPreparationOrchestrator
{
    public static async ValueTask<JournalPreparationResult> PrepareFromSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPreparationCommand command,
        JournalPreparationSourceLoader loadSource,
        JournalPreparationAuditAppender appendAudit,
        JournalPreparationOutboxAppender appendOutbox,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(loadSource);
        ValidateCommandContext(command);
        EnsurePostingPermission(command.Scope, command.SourceIdentity);
        if (command.ExpectedSourceVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Expected source version must be positive.");
        }

        CanonicalJournalPreparationSource source = await loadSource(
            connection, transaction, command, cancellationToken);
        ArgumentNullException.ThrowIfNull(source);
        if (source.Draft.PostingIdentity != command.SourceIdentity)
        {
            throw new JournalPostingCandidateException(
                "JOURNAL_SOURCE_IDENTITY_MISMATCH",
                "The canonical journal draft does not match the requested source identity.");
        }

        if (source.SourceVersion != command.ExpectedSourceVersion)
        {
            throw new JournalPostingCandidateException(
                "JOURNAL_SOURCE_VERSION_MISMATCH",
                "The canonical source version does not match the expected source version.");
        }

        KaguERP.BuildingBlocks.Application.Approvals.ApprovalSubjectReference approvalSubject =
            command.ResolveApprovalSubject();
        _ = await PostgresAuthoritativeApprovalCompletionLoader.LoadAsync(
            connection,
            transaction,
            command.Scope,
            approvalSubject.TenantId,
            approvalSubject.CompanyId,
            approvalSubject.SubjectType,
            approvalSubject.SubjectId,
            approvalSubject.SubjectVersion,
            cancellationToken);

        var request = new JournalPreparationRequest(
            command.Scope,
            command.AuditContext,
            source.Draft,
            source.ChartOfAccountsVersionId,
            command.ReservationId,
            command.JournalDraftId,
            command.AuditEventId,
            command.OutboxEventId);
        return await PrepareAsync(
            connection, transaction, request, appendAudit, appendOutbox, cancellationToken);
    }

    public static async ValueTask<JournalPreparationResult> PrepareAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPreparationRequest request,
        JournalPreparationAuditAppender appendAudit,
        JournalPreparationOutboxAppender appendOutbox,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(appendAudit);
        ArgumentNullException.ThrowIfNull(appendOutbox);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        ValidateAuditContext(request);
        EnsurePostingPermission(request);
        ValidatedPeriodLockSet periodLocks = await PostgresAuthoritativePeriodGateLoader.LoadForStandardPostingAsync(
            connection, transaction, request.Scope, request.Draft, cancellationToken);
        ValidatedJournalAccountSet accounts = await PostgresAuthoritativeJournalAccountLoader.LoadAsync(
            connection, transaction, request.Scope, request.Draft, request.ChartOfAccountsVersionId, cancellationToken);
        ValidatedJournalDimensions dimensions = await PostgresAuthoritativeJournalDimensionLoader.LoadAsync(
            connection, transaction, request.Scope, request.Draft, cancellationToken);
        ValidatedJournalCurrencySet currencies = await PostgresAuthoritativeJournalCurrencyLoader.LoadAsync(
            connection, transaction, request.Scope, request.Draft, cancellationToken);
        _ = AuthorizedJournalPostingCandidate.Create(
            request.Scope,
            request.Draft,
            accounts,
            dimensions,
            currencies,
            periodLocks);
        JournalSourceReservationResult reservation = await PostgresJournalSourceReservationWriter.ReserveAsync(
            connection, transaction, request.Scope, request.ReservationId, request.Draft, cancellationToken);
        ValidatedJournalDraftPersistenceResult draft = await PostgresValidatedJournalDraftWriter.PersistAsync(
            connection, transaction, request.Scope, request.JournalDraftId, reservation, request.Draft, cancellationToken);

        await appendAudit(connection, transaction, request, draft.JournalDraftId, cancellationToken);
        if (!await appendOutbox(
                connection,
                transaction,
                request,
                draft.JournalDraftId,
                reservation.ReservationId,
                periodLocks.PeriodId,
                cancellationToken))
        {
            throw new InvalidOperationException("Journal preparation outbox event was already present.");
        }

        return new JournalPreparationResult(
            reservation.ReservationId,
            draft.JournalDraftId,
            periodLocks.PeriodId,
            draft.DraftHash,
            reservation.Created,
            draft.Created);
    }

    private static void EnsurePostingPermission(JournalPreparationRequest request)
    {
        EnsurePostingPermission(request.Scope, request.Draft.PostingIdentity);
    }

    private static void EnsurePostingPermission(
        KaguERP.BuildingBlocks.Application.Security.ExecutionScope scope,
        KaguERP.Modules.Accounting.Domain.Journals.JournalPostingIdentity sourceIdentity)
    {
        scope.EnsureAllowed(sourceIdentity.TenantId, sourceIdentity.CompanyId);
        if (!scope.HasPermission(sourceIdentity.CompanyId, AuthorizedJournalPostingCandidate.RequiredPermission))
        {
            throw new JournalPostingCandidateException(
                "JOURNAL_POST_PERMISSION_REQUIRED",
                "The active actor does not have permission to post journals for this company.");
        }
    }

    private static void ValidateCommandContext(JournalPreparationCommand command)
    {
        if (command.AuditContext.TenantId != command.Scope.TenantId ||
            command.AuditContext.ActorId != command.Scope.ActorId ||
            !command.AuditContext.CompanyIds.SetEquals(command.Scope.CompanyIds) ||
            !command.AuditContext.CompanyIds.Contains(command.SourceIdentity.CompanyId) ||
            command.SourceIdentity.TenantId != command.Scope.TenantId)
        {
            throw new ArgumentException("Audit, source and trusted execution scope must exactly match.", nameof(command));
        }
    }

    private static void ValidateAuditContext(JournalPreparationRequest request)
    {
        if (request.AuditContext.TenantId != request.Scope.TenantId ||
            request.AuditContext.ActorId != request.Scope.ActorId ||
            !request.AuditContext.CompanyIds.SetEquals(request.Scope.CompanyIds) ||
            !request.AuditContext.CompanyIds.Contains(request.Draft.CompanyId))
        {
            throw new ArgumentException("Audit context must exactly match the trusted execution scope.", nameof(request));
        }
    }
}
