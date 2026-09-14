using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Parties.Infrastructure.Persistence;
using KaguERP.Modules.Purchasing.Contracts.Invoices;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>Checks payable/currency and item activation/UOM/precision. Not finalization or complete supplier eligibility.</summary>
public static class PostgresSupplierInvoiceCaptureMasterCheck
{
    public static async ValueTask<SupplierInvoiceCapturedDocument?> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid invoiceId, RequestAuditContext audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Invoice master check requires the caller's ReadCommitted transaction.");
        const string savepoint = "supplier_invoice_capture_master_check";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var document = await PostgresSupplierInvoiceCapture.LoadAsync(connection, transaction, scope, companyId, invoiceId, audit, cancellationToken);
            if (document is not null)
            {
                var capture = document.Capture;
                await PostgresSupplierAccountCheck.EnsureAsync(connection, transaction, scope, companyId,
                    capture.PartyAccountId, capture.Currency, cancellationToken);
                await PostgresInvoiceItemCheck.EnsureAsync(connection, transaction, scope, companyId,
                    capture.Lines.Select(line => new InvoiceItemCheckLine(line.ItemId, line.BaseUomCode, line.BaseQuantity)), cancellationToken);
                await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                    audit with { CompanyIds = new HashSet<Guid> { companyId } }, Guid.CreateVersion7(),
                    new AuthorizationAuditEvent("purchasing.invoice.capture.master-check", "supplier-invoice-capture", invoiceId.ToString("D"),
                        "allowed", "SUPPLIER_INVOICE_CAPTURE_MASTER_CHECKED"), cancellationToken);
            }
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return document;
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
