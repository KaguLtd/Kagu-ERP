using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Purchasing.Contracts.Invoices;
using Npgsql;

namespace KaguERP.Modules.Purchasing.Infrastructure;

public static class PostgresSupplierInvoiceCaptureWriter
{
    public const string RequiredPermission = "purchasing.invoice.create";
    public static async ValueTask<SupplierInvoiceCaptureResult> CreateAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, SupplierInvoiceCapture capture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(capture);
        scope.EnsureAllowed(capture.TenantId, capture.CompanyId);
        if (scope.ActorId != capture.ActorId || !scope.HasPermission(capture.CompanyId, RequiredPermission))
            throw new UnauthorizedAccessException("Invoice capture permission and actor scope are required.");
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Invoice capture requires the caller's ReadCommitted transaction.");
        await using (var context = new NpgsqlCommand("SELECT set_config('app.tenant_id',$1,true),set_config('app.actor_id',$2,true),set_config('app.company_ids',$3,true)", connection, transaction))
        {
            context.Parameters.AddWithValue(scope.TenantId.ToString("D")); context.Parameters.AddWithValue(scope.ActorId.ToString("D"));
            context.Parameters.AddWithValue("{" + capture.CompanyId.ToString("D") + "}");
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"purchasing.invoice.capture/v1/{capture.TenantId:D}/{capture.CompanyId:D}/{capture.InvoiceId:D}"));
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction))
        {
            gate.Parameters.AddWithValue(BinaryPrimitives.ReadInt64BigEndian(hash));
            await gate.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var existing = new NpgsqlCommand("SELECT fingerprint,recorded_at FROM purchasing.invoice_capture WHERE tenant_id=$1 AND company_id=$2 AND invoice_id=$3", connection, transaction))
        {
            Identity(existing, capture);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetString(0) != capture.Fingerprint) throw new SupplierInvoiceCaptureConflictException();
                return new(capture.InvoiceId, reader.GetFieldValue<DateTimeOffset>(1), false);
            }
        }
        const string savepoint = "supplier_invoice_capture_write";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            DateTimeOffset recorded;
            await using (var header = new NpgsqlCommand("""
                INSERT INTO purchasing.invoice_capture
                    (tenant_id,company_id,invoice_id,party_account_id,document_number,document_date,currency,line_count,total_net_amount,fingerprint,recorded_by)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11) RETURNING recorded_at
                """, connection, transaction))
            {
                Identity(header, capture); header.Parameters.AddWithValue(capture.PartyAccountId);
                header.Parameters.AddWithValue(capture.DocumentNumber); header.Parameters.AddWithValue(capture.DocumentDate);
                header.Parameters.AddWithValue(capture.Currency); header.Parameters.AddWithValue(capture.Lines.Count);
                header.Parameters.AddWithValue(capture.TotalNetAmount); header.Parameters.AddWithValue(capture.Fingerprint);
                header.Parameters.AddWithValue(capture.ActorId);
                await using var reader = await header.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Invoice capture header was not written.");
                recorded = reader.GetFieldValue<DateTimeOffset>(0);
            }
            foreach (var line in capture.Lines)
            {
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO purchasing.invoice_capture_line
                    (tenant_id,company_id,invoice_id,line_id,item_id,base_uom_code,base_quantity,net_amount)
                    VALUES ($1,$2,$3,$4,$5,$6,$7,$8)
                    """, connection, transaction);
                Identity(insert, capture); insert.Parameters.AddWithValue(line.LineId); insert.Parameters.AddWithValue(line.ItemId);
                insert.Parameters.AddWithValue(line.BaseUomCode); insert.Parameters.AddWithValue(line.BaseQuantity); insert.Parameters.AddWithValue(line.NetAmount);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(capture.InvoiceId, recorded, true);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
    private static void Identity(NpgsqlCommand command, SupplierInvoiceCapture capture)
    {
        command.Parameters.AddWithValue(capture.TenantId); command.Parameters.AddWithValue(capture.CompanyId); command.Parameters.AddWithValue(capture.InvoiceId);
    }
}

public sealed class SupplierInvoiceCaptureConflictException() : InvalidOperationException("Invoice capture identity already has different content.");
