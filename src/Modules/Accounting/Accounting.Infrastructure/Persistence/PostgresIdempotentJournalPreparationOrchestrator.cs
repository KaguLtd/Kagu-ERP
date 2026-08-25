using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KaguERP.BuildingBlocks.Application.Idempotency;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Application.Posting;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public delegate ValueTask<IdempotencyRecord> JournalPreparationIdempotencyAcquirer(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    ExecutionScope scope,
    Guid companyId,
    Guid recordId,
    string commandName,
    string idempotencyKey,
    string requestHash,
    CancellationToken cancellationToken);

public delegate ValueTask<IdempotencyRecord> JournalPreparationIdempotencyCompleter(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    ExecutionScope scope,
    IdempotencyRecord acquired,
    int responseStatus,
    string responseBodyJson,
    Guid? aggregateId,
    CancellationToken cancellationToken);

public sealed record IdempotentJournalPreparationResult(
    JournalPreparationResult Preparation,
    bool Replayed);

public static class PostgresIdempotentJournalPreparationOrchestrator
{
    private const string CommandName = "accounting.journal.prepare";

    public static async ValueTask<IdempotentJournalPreparationResult> PrepareAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPreparationCommand command,
        Guid idempotencyRecordId,
        string idempotencyKey,
        JournalPreparationIdempotencyAcquirer acquireIdempotency,
        JournalPreparationIdempotencyCompleter completeIdempotency,
        JournalPreparationSourceLoader loadSource,
        JournalPreparationAuditAppender appendAudit,
        JournalPreparationOutboxAppender appendOutbox,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acquireIdempotency);
        ArgumentNullException.ThrowIfNull(completeIdempotency);
        string requestHash = ComputeRequestHash(command);
        IdempotencyRecord acquired = await acquireIdempotency(
            connection, transaction, command.Scope, command.SourceIdentity.CompanyId, idempotencyRecordId,
            CommandName, idempotencyKey, requestHash, cancellationToken);

        if (!acquired.Created)
        {
            if (acquired.Status != IdempotencyRecordStatus.Completed || acquired.ResponseBodyJson is null)
            {
                throw new InvalidOperationException("IDEMPOTENCY_REQUEST_IN_PROGRESS");
            }

            JournalPreparationResult replay = JsonSerializer.Deserialize<JournalPreparationResult>(acquired.ResponseBodyJson)
                ?? throw new InvalidOperationException("Completed idempotency response is invalid.");
            return new IdempotentJournalPreparationResult(replay, true);
        }

        JournalPreparationResult prepared = await PostgresJournalPreparationOrchestrator.PrepareFromSourceAsync(
            connection, transaction, command, loadSource, appendAudit, appendOutbox, cancellationToken);
        _ = await completeIdempotency(
            connection, transaction, command.Scope, acquired, 201, JsonSerializer.Serialize(prepared),
            prepared.JournalDraftId, cancellationToken);
        return new IdempotentJournalPreparationResult(prepared, false);
    }

    public static string ComputeRequestHash(JournalPreparationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        string canonical = string.Join(
            '\u001f',
            command.SourceIdentity.TenantId.ToString("D"),
            command.SourceIdentity.CompanyId.ToString("D"),
            command.SourceIdentity.SourceType,
            command.SourceIdentity.SourceEventId.ToString("D"),
            command.SourceIdentity.PostingPurpose,
            command.ExpectedSourceVersion.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
