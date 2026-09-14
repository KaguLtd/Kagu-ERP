using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Purchasing.Contracts.Invoices;
using KaguERP.Modules.Purchasing.Infrastructure;
using Npgsql;

namespace KaguERP.Bootstrap;

public static class PostgresSupplierInvoiceCapture
{
    public static async ValueTask<SupplierInvoiceCapturedDocument?> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid invoiceId, RequestAuditContext audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(audit);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Invoice capture read requires the caller's ReadCommitted transaction.");
        if (scope.TenantId != audit.TenantId || scope.ActorId != audit.ActorId || !scope.CompanyIds.SetEquals(audit.CompanyIds))
            throw new UnauthorizedAccessException("Invoice capture audit must match the trusted actor scope.");
        const string savepoint = "supplier_invoice_capture_read_audit";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var result = await PostgresSupplierInvoiceCaptureReader.LoadAsync(connection, transaction, scope, companyId, invoiceId, cancellationToken);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                audit with { CompanyIds = new HashSet<Guid> { companyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("purchasing.invoice.capture.read", "supplier-invoice-capture", invoiceId.ToString("D"),
                    "allowed", result is null ? "SUPPLIER_INVOICE_CAPTURE_NOT_FOUND" : "SUPPLIER_INVOICE_CAPTURE_READ"), cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    public static async ValueTask<SupplierInvoiceCaptureResult> CreateAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, SupplierInvoiceCapture capture, RequestAuditContext audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(capture); ArgumentNullException.ThrowIfNull(audit);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Invoice capture requires the caller's ReadCommitted transaction.");
        if (scope.TenantId != audit.TenantId || scope.ActorId != audit.ActorId || !scope.CompanyIds.SetEquals(audit.CompanyIds))
            throw new UnauthorizedAccessException("Invoice capture audit must match the trusted actor scope.");
        const string savepoint = "supplier_invoice_capture_audit";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var result = await PostgresSupplierInvoiceCaptureWriter.CreateAsync(connection, transaction, scope, capture, cancellationToken);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                audit with { CompanyIds = new HashSet<Guid> { capture.CompanyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("purchasing.invoice.capture", "supplier-invoice-capture", capture.InvoiceId.ToString("D"),
                    "allowed", result.Created ? "SUPPLIER_INVOICE_CAPTURED" : "SUPPLIER_INVOICE_CAPTURE_REPLAYED"), cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
