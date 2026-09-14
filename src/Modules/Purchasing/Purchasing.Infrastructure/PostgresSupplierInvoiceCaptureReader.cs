using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Purchasing.Contracts.Invoices;
using Npgsql;

namespace KaguERP.Modules.Purchasing.Infrastructure;

public static class PostgresSupplierInvoiceCaptureReader
{
    public const string RequiredPermission = "purchasing.invoice.view";

    public static async ValueTask<SupplierInvoiceCapturedDocument?> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, RequiredPermission))
            throw new UnauthorizedAccessException("Invoice viewing permission is required.");
        if (invoiceId == Guid.Empty) throw new ArgumentException("Invoice identity is required.", nameof(invoiceId));
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Invoice capture read requires the caller's ReadCommitted transaction.");
        await using (var context = new NpgsqlCommand("SELECT set_config('app.tenant_id',$1,true),set_config('app.actor_id',$2,true),set_config('app.company_ids',$3,true)", connection, transaction))
        {
            context.Parameters.AddWithValue(scope.TenantId.ToString("D")); context.Parameters.AddWithValue(scope.ActorId.ToString("D"));
            context.Parameters.AddWithValue("{" + companyId.ToString("D") + "}");
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        // One statement snapshot; LEFT JOIN also exposes an incomplete header before deferred constraints fire.
        await using var query = new NpgsqlCommand("""
            SELECT h.party_account_id,h.document_number,h.document_date,h.currency,h.version,
                   h.line_count,h.total_net_amount,h.fingerprint,h.recorded_at,h.recorded_by,
                   l.line_id,l.item_id,l.base_uom_code,l.base_quantity,l.net_amount
            FROM purchasing.invoice_capture h
            LEFT JOIN purchasing.invoice_capture_line l
              ON l.tenant_id=h.tenant_id AND l.company_id=h.company_id AND l.invoice_id=h.invoice_id
            WHERE h.tenant_id=$1 AND h.company_id=$2 AND h.invoice_id=$3
            ORDER BY l.line_id LIMIT 501
            """, connection, transaction);
        query.Parameters.AddWithValue(scope.TenantId); query.Parameters.AddWithValue(companyId); query.Parameters.AddWithValue(invoiceId);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        Guid partyId = reader.GetGuid(0), authorId = reader.GetGuid(9);
        string number = reader.GetString(1), currency = reader.GetString(3), fingerprint = reader.GetString(7);
        var date = reader.GetFieldValue<DateOnly>(2);
        long version = reader.GetInt64(4);
        int count = reader.GetInt32(5);
        decimal total = reader.GetDecimal(6);
        var recordedAt = reader.GetFieldValue<DateTimeOffset>(8);
        List<SupplierInvoiceCaptureLine> lines = [];
        do
        {
            if (reader.IsDBNull(10)) throw new SupplierInvoiceCaptureIntegrityException();
            lines.Add(new(reader.GetGuid(10), reader.GetGuid(11), reader.GetString(12), reader.GetDecimal(13), reader.GetDecimal(14)));
        } while (await reader.ReadAsync(cancellationToken));
        SupplierInvoiceCapture capture;
        try
        {
            // The original author, not the reader, is part of the persisted content identity.
            capture = new(scope.TenantId, companyId, invoiceId, authorId, partyId, number, date, currency, lines);
        }
        catch (ArgumentException) { throw new SupplierInvoiceCaptureIntegrityException(); }
        if (version != capture.Version || count != capture.Lines.Count || total != capture.TotalNetAmount ||
            !string.Equals(fingerprint, capture.Fingerprint, StringComparison.Ordinal) || recordedAt == default ||
            recordedAt == DateTimeOffset.MaxValue || recordedAt == DateTimeOffset.MinValue)
            throw new SupplierInvoiceCaptureIntegrityException();
        return new(capture, recordedAt);
    }
}

public sealed class SupplierInvoiceCaptureIntegrityException() : InvalidOperationException("Persisted invoice capture integrity check failed.");
