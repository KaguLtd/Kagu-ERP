using System.Text;
using System.Text.Json;
using KaguERP.BuildingBlocks.Application.Idempotency;
using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;
using NpgsqlTypes;

namespace KaguERP.Bootstrap;

public static class PostgresIdempotencyWriter
{
    private const int MaximumResponseBytes = 256 * 1024;

    public static async ValueTask<IdempotencyRecord> AcquireAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid recordId,
        string commandName,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        ValidateTransaction(connection, transaction);
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        ValidateId(recordId, nameof(recordId));
        commandName = ValidateText(commandName, 160, nameof(commandName));
        idempotencyKey = ValidateText(idempotencyKey, 200, nameof(idempotencyKey));
        requestHash = ValidateHash(requestHash);

        const string insertSql = """
            INSERT INTO platform.idempotency_record
                (record_id, tenant_id, company_id, actor_id, command_name, idempotency_key, request_hash)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            ON CONFLICT (tenant_id, company_id, actor_id, command_name, idempotency_key) DO NOTHING
            RETURNING record_id
            """;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            insert.Parameters.AddWithValue(recordId);
            insert.Parameters.AddWithValue(scope.TenantId);
            insert.Parameters.AddWithValue(companyId);
            insert.Parameters.AddWithValue(scope.ActorId);
            insert.Parameters.AddWithValue(commandName);
            insert.Parameters.AddWithValue(idempotencyKey);
            insert.Parameters.AddWithValue(requestHash);
            if (await insert.ExecuteScalarAsync(cancellationToken) is Guid insertedId)
            {
                return new IdempotencyRecord(
                    insertedId, scope.TenantId, companyId, scope.ActorId, commandName, idempotencyKey,
                    requestHash, IdempotencyRecordStatus.InProgress, null, null, null, true);
            }
        }

        const string existingSql = """
            SELECT record_id, request_hash, record_status, response_status, response_body::text, aggregate_id
            FROM platform.idempotency_record
            WHERE tenant_id = $1 AND company_id = $2 AND actor_id = $3
              AND command_name = $4 AND idempotency_key = $5
            FOR UPDATE
            """;
        await using var existing = new NpgsqlCommand(existingSql, connection, transaction);
        existing.Parameters.AddWithValue(scope.TenantId);
        existing.Parameters.AddWithValue(companyId);
        existing.Parameters.AddWithValue(scope.ActorId);
        existing.Parameters.AddWithValue(commandName);
        existing.Parameters.AddWithValue(idempotencyKey);
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Idempotency record disappeared after its unique-key conflict.");
        }

        string storedHash = reader.GetString(1);
        if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal))
        {
            throw new IdempotencyKeyReusedException();
        }

        return new IdempotencyRecord(
            reader.GetGuid(0),
            scope.TenantId,
            companyId,
            scope.ActorId,
            commandName,
            idempotencyKey,
            storedHash,
            (IdempotencyRecordStatus)reader.GetInt16(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3),
            reader.IsDBNull(4) ? null : NormalizeJson(reader.GetString(4)),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            false);
    }

    public static async ValueTask<IdempotencyRecord> CompleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        IdempotencyRecord acquired,
        int responseStatus,
        string responseBodyJson,
        Guid? aggregateId,
        CancellationToken cancellationToken = default)
    {
        ValidateTransaction(connection, transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(acquired);
        scope.EnsureAllowed(acquired.TenantId, acquired.CompanyId);
        if (scope.ActorId != acquired.ActorId || acquired.Status != IdempotencyRecordStatus.InProgress)
        {
            throw new InvalidOperationException("Only the acquiring actor can complete an in-progress idempotency record.");
        }

        if (responseStatus is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(responseStatus));
        }

        string normalizedBody = NormalizeJson(responseBodyJson);
        const string sql = """
            UPDATE platform.idempotency_record
            SET record_status = 2, response_status = $2, response_body = $3,
                aggregate_id = $4, completed_at = clock_timestamp()
            WHERE record_id = $1 AND tenant_id = $5 AND company_id = $6 AND actor_id = $7
              AND record_status = 1 AND request_hash = $8
            RETURNING 1
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(acquired.RecordId);
        command.Parameters.AddWithValue(responseStatus);
        command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, normalizedBody);
        command.Parameters.AddWithValue((object?)aggregateId ?? DBNull.Value);
        command.Parameters.AddWithValue(acquired.TenantId);
        command.Parameters.AddWithValue(acquired.CompanyId);
        command.Parameters.AddWithValue(acquired.ActorId);
        command.Parameters.AddWithValue(acquired.RequestHash);
        if (await command.ExecuteScalarAsync(cancellationToken) is not int value || value != 1)
        {
            throw new InvalidOperationException("Idempotency record could not be completed from its current state.");
        }

        return acquired with
        {
            Status = IdempotencyRecordStatus.Completed,
            ResponseStatus = responseStatus,
            ResponseBodyJson = normalizedBody,
            AggregateId = aggregateId,
        };
    }

    private static void ValidateTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
    }

    private static void ValidateId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }

    private static string ValidateText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be blank.", parameterName);
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value exceeds {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string ValidateHash(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64 ||
            value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Request hash must be 64 lowercase hexadecimal characters.", nameof(value));
        }

        return value;
    }

    private static string NormalizeJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) > MaximumResponseBytes)
        {
            throw new ArgumentException("Response JSON is blank or exceeds 256 KiB.", nameof(value));
        }

        using JsonDocument document = JsonDocument.Parse(value, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
        return JsonSerializer.Serialize(document.RootElement);
    }
}

public sealed class IdempotencyKeyReusedException : InvalidOperationException
{
    public const string ErrorCode = "IDEMPOTENCY_KEY_REUSED";

    public IdempotencyKeyReusedException()
        : base("The idempotency key was already used with a different request payload.")
    {
        Code = ErrorCode;
    }

    public string Code { get; }
}
