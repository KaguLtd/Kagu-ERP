using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KaguERP.BuildingBlocks.Application.Messaging;
using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;
using NpgsqlTypes;

namespace KaguERP.Bootstrap;

public static class PostgresOutboxWriter
{
    private const int MaximumPayloadBytes = 256 * 1024;

    public static async ValueTask<bool> EnqueueAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(message);

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        scope.EnsureAllowed(message.TenantId, message.CompanyId);
        string normalizedPayload = ValidateAndNormalize(message);
        string messageHash = ComputeMessageHash(message, normalizedPayload);

        const string insertSql = """
            INSERT INTO platform.outbox_message
                (event_id, tenant_id, company_id, aggregate_type, aggregate_id, aggregate_sequence,
                 event_type, schema_version, occurred_at, payload, message_hash)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
            ON CONFLICT (event_id) DO NOTHING
            RETURNING 1
            """;
        await using (var command = new NpgsqlCommand(insertSql, connection, transaction))
        {
            command.Parameters.AddWithValue(message.EventId);
            command.Parameters.AddWithValue(message.TenantId);
            command.Parameters.AddWithValue(message.CompanyId);
            command.Parameters.AddWithValue(message.AggregateType);
            command.Parameters.AddWithValue(message.AggregateId);
            command.Parameters.AddWithValue(message.AggregateSequence);
            command.Parameters.AddWithValue(message.EventType);
            command.Parameters.AddWithValue(message.SchemaVersion);
            command.Parameters.AddWithValue(message.OccurredAt);
            command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, normalizedPayload);
            command.Parameters.AddWithValue(messageHash);
            object? inserted = await command.ExecuteScalarAsync(cancellationToken);
            if (inserted is int value && value == 1)
            {
                return true;
            }
        }

        const string existingSql = """
            SELECT message_hash
            FROM platform.outbox_message
            WHERE event_id = $1
            """;
        await using var existingCommand = new NpgsqlCommand(existingSql, connection, transaction);
        existingCommand.Parameters.AddWithValue(message.EventId);
        string? existingHash = (string?)await existingCommand.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(existingHash, messageHash, StringComparison.Ordinal))
        {
            throw new OutboxEventConflictException();
        }

        return false;
    }

    private static string ValidateAndNormalize(OutboxMessage message)
    {
        if (message.EventId == Guid.Empty || message.TenantId == Guid.Empty ||
            message.CompanyId == Guid.Empty || message.AggregateId == Guid.Empty)
        {
            throw new ArgumentException("Outbox identifiers cannot be empty.", nameof(message));
        }

        ValidateText(message.AggregateType, 120, nameof(message.AggregateType));
        ValidateText(message.EventType, 160, nameof(message.EventType));
        if (message.AggregateSequence <= 0 || message.SchemaVersion <= 0)
        {
            throw new ArgumentException("Outbox sequence and schema version must be positive.", nameof(message));
        }

        if (message.OccurredAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Outbox occurred time must be UTC.", nameof(message));
        }

        if (Encoding.UTF8.GetByteCount(message.PayloadJson) > MaximumPayloadBytes)
        {
            throw new ArgumentException("Outbox payload exceeds the 256 KiB limit.", nameof(message));
        }

        using JsonDocument payload = JsonDocument.Parse(message.PayloadJson, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
        return JsonSerializer.Serialize(payload.RootElement);
    }

    private static void ValidateText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} is blank or exceeds {maximumLength} characters.", parameterName);
        }
    }

    private static string ComputeMessageHash(OutboxMessage message, string normalizedPayload)
    {
        string canonical = string.Join(
            '\u001f',
            message.EventId.ToString("D"),
            message.TenantId.ToString("D"),
            message.CompanyId.ToString("D"),
            message.AggregateType,
            message.AggregateId.ToString("D"),
            message.AggregateSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            message.EventType,
            message.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            message.OccurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            normalizedPayload);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
