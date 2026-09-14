using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Parties.Infrastructure.Persistence;
using KaguERP.Modules.Purchasing.Contracts.Invoices;
using KaguERP.Modules.Purchasing.Infrastructure;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertSupplierInvoiceCaptureAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenant, Guid company, Guid actor, Guid item, Guid otherCompany)
    {
        const string fixture = "supplier_invoice_capture_fixture";
        await transaction.SaveAsync(fixture);
        Guid party = Guid.CreateVersion7(), account = Guid.CreateVersion7(), invoice = Guid.CreateVersion7(), lineId = Guid.CreateVersion7();
        await using (var identity = new NpgsqlCommand("INSERT INTO party.party_identity(tenant_id,party_id,created_at,created_by) VALUES ($1,$2,clock_timestamp(),$3)", connection, transaction))
        {
            identity.Parameters.AddWithValue(tenant); identity.Parameters.AddWithValue(party); identity.Parameters.AddWithValue(actor);
            await identity.ExecuteNonQueryAsync();
        }
        await using (var master = new NpgsqlCommand("""
            INSERT INTO party.party_account(tenant_id,company_id,party_account_id,party_id,currency,control_account_id,created_at,created_by,balance_side)
            VALUES ($1,$2,$3,$4,'TRY',$5,clock_timestamp(),$6,2)
            """, connection, transaction))
        {
            master.Parameters.AddWithValue(tenant); master.Parameters.AddWithValue(company); master.Parameters.AddWithValue(account);
            master.Parameters.AddWithValue(party); master.Parameters.AddWithValue(Guid.CreateVersion7()); master.Parameters.AddWithValue(actor);
            await master.ExecuteNonQueryAsync();
        }
        SupplierInvoiceCapture Capture(Guid id, decimal net = 12.5m, Guid? itemOverride = null) => new(tenant, company, id, actor, account,
            "INV-TEST-1", new(2026, 9, 14), "TRY", [new(lineId, itemOverride ?? item, "EA", 2m, net)]);
        var scope = SalesScope(tenant, company, actor, PostgresSupplierInvoiceCaptureWriter.RequiredPermission);
        await ThrowsAsync<ArgumentException>(() => Task.FromResult(Capture(invoice, -1m)));
        await ThrowsAsync<ArgumentException>(() => Task.FromResult(Capture(invoice, .00001m)));
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "invoice-capture", tenant, actor, new HashSet<Guid> { company }, null);
        var created = await PostgresSupplierInvoiceCapture.CreateAsync(connection, transaction, scope, Capture(invoice), audit);
        var replay = await PostgresSupplierInvoiceCapture.CreateAsync(connection, transaction, scope, Capture(invoice, 12.5000m), audit);
        Assert(created.Created && !replay.Created && created.RecordedAt == replay.RecordedAt,
            "Invoice capture did not preserve original timestamp on exact replay.");
        var readScope = SalesScope(tenant, company, actor, PostgresSupplierInvoiceCaptureReader.RequiredPermission);
        Assert(await PostgresSupplierInvoiceCaptureMasterCheck.LoadAsync(connection, transaction, readScope, company, invoice, audit) is not null,
            "Valid capture failed its current account/item checks.");
        await ThrowsAsync<SupplierAccountUnavailableException>(async () =>
            await PostgresSupplierAccountCheck.EnsureAsync(connection, transaction, readScope, company, account, "EUR"));
        await ThrowsAsync<SupplierAccountUnavailableException>(async () =>
            await PostgresSupplierAccountCheck.EnsureAsync(connection, transaction,
                SalesScope(tenant, otherCompany, actor, PostgresSupplierInvoiceCaptureReader.RequiredPermission), otherCompany, account, "TRY"));
        await ThrowsAsync<UnauthorizedAccessException>(async () =>
            await PostgresSupplierAccountCheck.EnsureAsync(connection, transaction, scope, company, account, "TRY"));
        Guid receivable = Guid.CreateVersion7();
        await using (var wrongSide = new NpgsqlCommand("""
            INSERT INTO party.party_account(tenant_id,company_id,party_account_id,party_id,currency,control_account_id,created_at,created_by,balance_side)
            VALUES ($1,$2,$3,$4,'TRY',$5,clock_timestamp(),$6,1)
            """, connection, transaction))
        {
            wrongSide.Parameters.AddWithValue(tenant); wrongSide.Parameters.AddWithValue(company); wrongSide.Parameters.AddWithValue(receivable);
            wrongSide.Parameters.AddWithValue(party); wrongSide.Parameters.AddWithValue(Guid.NewGuid()); wrongSide.Parameters.AddWithValue(actor);
            await wrongSide.ExecuteNonQueryAsync();
        }
        await ThrowsAsync<SupplierAccountUnavailableException>(async () =>
            await PostgresSupplierAccountCheck.EnsureAsync(connection, transaction, readScope, company, receivable, "TRY"));
        await ThrowsAsync<InvoiceItemUnavailableException>(async () =>
            await PostgresInvoiceItemCheck.EnsureAsync(connection, transaction, readScope, company, [new(item, "KG", 2m)]));
        await ThrowsAsync<InvoiceItemUnavailableException>(async () =>
            await PostgresInvoiceItemCheck.EnsureAsync(connection, transaction, readScope, company, [new(Guid.NewGuid(), "EA", 2m)]));
        await ThrowsAsync<ArgumentException>(async () =>
            await PostgresInvoiceItemCheck.EnsureAsync(connection, transaction, readScope, company, Enumerable.Repeat(new InvoiceItemCheckLine(item, "EA", 1m), 501)));
        foreach (string table in new[] { "item", "item_company" })
        {
            await transaction.SaveAsync("invoice_inactive_master");
            string sql = table == "item"
                ? "UPDATE inventory.item SET is_active=false,version=version+1 WHERE tenant_id=$1 AND item_id=$2"
                : "UPDATE inventory.item_company SET is_active=false,version=version+1 WHERE tenant_id=$1 AND item_id=$2 AND company_id=$3";
            await using (var inactive = new NpgsqlCommand(sql, connection, transaction))
            {
                inactive.Parameters.AddWithValue(tenant); inactive.Parameters.AddWithValue(item);
                if (table == "item_company") inactive.Parameters.AddWithValue(company);
                await inactive.ExecuteNonQueryAsync();
            }
            var failedAudit = audit with { CorrelationId = Guid.CreateVersion7() };
            await ThrowsAsync<InvoiceItemUnavailableException>(async () =>
                await PostgresSupplierInvoiceCaptureMasterCheck.LoadAsync(connection, transaction, readScope, company, invoice, failedAudit));
            await using (var auditCount = new NpgsqlCommand("SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2", connection, transaction))
            {
                auditCount.Parameters.AddWithValue(tenant); auditCount.Parameters.AddWithValue(failedAudit.CorrelationId);
                Assert((long)(await auditCount.ExecuteScalarAsync())! == 0,
                    "Failed invoice master check retained a successful read audit.");
            }
            Assert(await PostgresSupplierInvoiceCapture.LoadAsync(connection, transaction, readScope, company, invoice, audit) is not null,
                "Inactive current master prevented historical capture reading.");
            await transaction.RollbackAsync("invoice_inactive_master");
            await transaction.ReleaseAsync("invoice_inactive_master");
        }
        await transaction.SaveAsync("invoice_master_precision");
        await using (var precision = new NpgsqlCommand("UPDATE inventory.item SET allows_fractional_quantity=false,quantity_scale=0,version=version+1 WHERE tenant_id=$1 AND item_id=$2", connection, transaction))
        {
            precision.Parameters.AddWithValue(tenant); precision.Parameters.AddWithValue(item);
            await precision.ExecuteNonQueryAsync();
        }
        await ThrowsAsync<InvoiceItemUnavailableException>(async () =>
            await PostgresInvoiceItemCheck.EnsureAsync(connection, transaction, readScope, company, [new(item, "EA", .5m)]));
        await transaction.RollbackAsync("invoice_master_precision");
        await transaction.ReleaseAsync("invoice_master_precision");
        var loaded = await PostgresSupplierInvoiceCapture.LoadAsync(connection, transaction, readScope, company, invoice, audit);
        Assert(loaded is not null && loaded.Capture.Fingerprint == Capture(invoice).Fingerprint &&
            loaded.RecordedAt == created.RecordedAt && loaded.Capture.Lines.Single().NetAmount == 12.5m,
            "Persisted capture round-trip changed its content or timestamp.");
        var otherReader = await PostgresSupplierInvoiceCaptureReader.LoadAsync(connection, transaction,
            SalesScope(tenant, company, Guid.NewGuid(), PostgresSupplierInvoiceCaptureReader.RequiredPermission), company, invoice);
        Assert(otherReader?.Capture.ActorId == actor, "Capture read replaced its original author with the reader.");
        await ThrowsAsync<UnauthorizedAccessException>(async () =>
            await PostgresSupplierInvoiceCaptureReader.LoadAsync(connection, transaction, scope, company, invoice));
        Assert(await PostgresSupplierInvoiceCaptureReader.LoadAsync(connection, transaction,
            SalesScope(tenant, otherCompany, actor, PostgresSupplierInvoiceCaptureReader.RequiredPermission), otherCompany, invoice) is null,
            "Capture reader leaked an invoice from another company.");
        Assert(await PostgresSupplierInvoiceCaptureReader.LoadAsync(connection, transaction,
            SalesScope(Guid.NewGuid(), company, actor, PostgresSupplierInvoiceCaptureReader.RequiredPermission), company, invoice) is null,
            "Capture reader leaked an invoice from another tenant.");
        Assert(await PostgresSupplierInvoiceCapture.LoadAsync(connection, transaction, readScope, company, Guid.NewGuid(), audit) is null,
            "Missing capture did not return the same not-found result.");
        await ThrowsAsync<UnauthorizedAccessException>(async () => await PostgresSupplierInvoiceCapture.LoadAsync(
            connection, transaction, readScope, company, invoice, audit with { ActorId = Guid.NewGuid() }));
        await ThrowsAsync<PostgresException>(async () => await PostgresSupplierInvoiceCapture.LoadAsync(
            connection, transaction, readScope, company, invoice, audit with { TraceId = new string('x', 65) }));
        Assert(await PostgresSupplierInvoiceCapture.LoadAsync(connection, transaction, readScope, company, invoice, audit) is not null,
            "Read audit failure poisoned the caller transaction.");
        await ThrowsAsync<SupplierInvoiceCaptureConflictException>(async () =>
            await PostgresSupplierInvoiceCapture.CreateAsync(connection, transaction, scope, Capture(invoice, 13m), audit));
        await ThrowsAsync<UnauthorizedAccessException>(async () =>
            await PostgresSupplierInvoiceCapture.CreateAsync(connection, transaction, SalesScope(tenant, company, actor), Capture(invoice), audit));
        async Task<long> Count(Guid id)
        {
            await using var sql = new NpgsqlCommand("SELECT count(*) FROM purchasing.invoice_capture WHERE tenant_id=$1 AND company_id=$2 AND invoice_id=$3", connection, transaction);
            sql.Parameters.AddWithValue(tenant); sql.Parameters.AddWithValue(company); sql.Parameters.AddWithValue(id);
            return (long)(await sql.ExecuteScalarAsync())!;
        }
        Guid failedId = Guid.CreateVersion7();
        await ThrowsAsync<PostgresException>(async () =>
            await PostgresSupplierInvoiceCapture.CreateAsync(connection, transaction, scope, Capture(failedId), audit with { TraceId = new string('x', 65) }));
        Assert(await Count(failedId) == 0, "Audit failure left the invoice capture behind.");
        Guid invalidId = Guid.CreateVersion7();
        await ThrowsAsync<PostgresException>(async () =>
            await PostgresSupplierInvoiceCapture.CreateAsync(connection, transaction, scope, Capture(invalidId, itemOverride: Guid.NewGuid()), audit));
        Assert(await Count(invalidId) == 0, "Line FK failure left a partial invoice header.");
        var foreignCapture = new SupplierInvoiceCapture(tenant, otherCompany, Guid.CreateVersion7(), actor, account,
            "FOREIGN-TEST", new(2026, 9, 14), "TRY", [new(lineId, item, "EA", 2m, 12.5m)]);
        var fk = await ThrowsAsync<PostgresException>(async () => await PostgresSupplierInvoiceCaptureWriter.CreateAsync(connection,
            transaction, SalesScope(tenant, otherCompany, actor, PostgresSupplierInvoiceCaptureWriter.RequiredPermission), foreignCapture));
        Assert(fk.SqlState == PostgresErrorCodes.ForeignKeyViolation, "Cross-company party reference bypassed capture FK.");
        await transaction.SaveAsync("invoice_capture_incomplete");
        Guid incompleteId = Guid.CreateVersion7();
        await using (var incomplete = new NpgsqlCommand("""
            INSERT INTO purchasing.invoice_capture
                (tenant_id,company_id,invoice_id,party_account_id,document_number,document_date,currency,line_count,total_net_amount,fingerprint,recorded_by)
            SELECT tenant_id,company_id,$4,party_account_id,document_number,document_date,currency,line_count,total_net_amount,fingerprint,recorded_by
            FROM purchasing.invoice_capture WHERE tenant_id=$1 AND company_id=$2 AND invoice_id=$3
            """, connection, transaction))
        {
            incomplete.Parameters.AddWithValue(tenant); incomplete.Parameters.AddWithValue(company);
            incomplete.Parameters.AddWithValue(invoice); incomplete.Parameters.AddWithValue(incompleteId);
            await incomplete.ExecuteNonQueryAsync();
        }
        await ThrowsAsync<SupplierInvoiceCaptureIntegrityException>(async () =>
            await PostgresSupplierInvoiceCaptureReader.LoadAsync(connection, transaction, readScope, company, incompleteId));
        // Copy valid lines: count and total now match, but the fingerprint belongs to a different invoice identity.
        await using (var copy = new NpgsqlCommand("""
            INSERT INTO purchasing.invoice_capture_line
                (tenant_id,company_id,invoice_id,line_id,item_id,base_uom_code,base_quantity,net_amount)
            SELECT tenant_id,company_id,$4,line_id,item_id,base_uom_code,base_quantity,net_amount
            FROM purchasing.invoice_capture_line WHERE tenant_id=$1 AND company_id=$2 AND invoice_id=$3
            """, connection, transaction))
        {
            copy.Parameters.AddWithValue(tenant); copy.Parameters.AddWithValue(company);
            copy.Parameters.AddWithValue(invoice); copy.Parameters.AddWithValue(incompleteId);
            await transaction.SaveAsync("invoice_capture_bad_hash");
            await copy.ExecuteNonQueryAsync();
            await ThrowsAsync<SupplierInvoiceCaptureIntegrityException>(async () =>
                await PostgresSupplierInvoiceCaptureReader.LoadAsync(connection, transaction, readScope, company, incompleteId));
            await transaction.RollbackAsync("invoice_capture_bad_hash");
            await transaction.ReleaseAsync("invoice_capture_bad_hash");
        }
        await using (var complete = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", connection, transaction))
        {
            var error = await ThrowsAsync<PostgresException>(async () => await complete.ExecuteNonQueryAsync());
            Assert(error.SqlState == PostgresErrorCodes.CheckViolation, "Incomplete invoice capture passed the commit guard.");
        }
        await transaction.RollbackAsync("invoice_capture_incomplete");
        await transaction.ReleaseAsync("invoice_capture_incomplete");
        await using (var privileges = new NpgsqlCommand("""
            SELECT bool_and(relrowsecurity AND relforcerowsecurity AND has_table_privilege('kagu_erp_app',oid,'SELECT')
                AND NOT has_table_privilege('kagu_erp_app',oid,'INSERT') AND NOT has_table_privilege('kagu_erp_app',oid,'UPDATE')
                AND NOT has_table_privilege('kagu_erp_app',oid,'DELETE'))
            FROM pg_class WHERE oid IN ('purchasing.invoice_capture'::regclass,'purchasing.invoice_capture_line'::regclass)
            """, connection, transaction))
            Assert((bool)(await privileges.ExecuteScalarAsync())!, "Invoice capture lost forced RLS or SELECT-only runtime grants.");
        await transaction.SaveAsync("invoice_capture_immutable");
        await using (var update = new NpgsqlCommand("UPDATE purchasing.invoice_capture SET total_net_amount=1 WHERE tenant_id=$1 AND company_id=$2 AND invoice_id=$3", connection, transaction))
        {
            update.Parameters.AddWithValue(tenant); update.Parameters.AddWithValue(company); update.Parameters.AddWithValue(invoice);
            var error = await ThrowsAsync<PostgresException>(async () => await update.ExecuteNonQueryAsync());
            Assert(error.SqlState == "55000", "Invoice capture accepted in-place mutation.");
        }
        await transaction.RollbackAsync("invoice_capture_immutable");
        await transaction.ReleaseAsync("invoice_capture_immutable");
        await using (var check = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", connection, transaction))
            await check.ExecuteNonQueryAsync();
        await transaction.RollbackAsync(fixture);
        await transaction.ReleaseAsync(fixture);
    }
}
