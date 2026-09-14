using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace KaguERP.Modules.Purchasing.Contracts.Invoices;

public sealed record SupplierInvoiceCaptureLine(Guid LineId, Guid ItemId, string BaseUomCode, decimal BaseQuantity, decimal NetAmount);

/// <summary>Initial non-posted capture only. Supplier eligibility, taxes, FX and matching remain unvalidated.</summary>
public sealed class SupplierInvoiceCapture
{
    public SupplierInvoiceCapture(Guid tenantId, Guid companyId, Guid invoiceId, Guid actorId, Guid partyAccountId,
        string documentNumber, DateOnly documentDate, string currency, IEnumerable<SupplierInvoiceCaptureLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (new[] { tenantId, companyId, invoiceId, actorId, partyAccountId }.Contains(Guid.Empty) ||
            documentDate == default || string.IsNullOrWhiteSpace(documentNumber) || documentNumber.Length > 120 ||
            documentNumber.Any(char.IsControl) || currency is not { Length: 3 } || currency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Invoice capture requires scope, identities, document reference and currency.");
        var rows = lines.Take(501).ToArray();
        if (rows.Length is < 1 or > 500 || rows.Any(line => line is null || line.LineId == Guid.Empty || line.ItemId == Guid.Empty ||
                line.BaseUomCode is not { Length: >= 1 and <= 16 } || !char.IsAsciiLetterOrDigit(line.BaseUomCode[0]) ||
                line.BaseUomCode.Any(c => !char.IsAsciiDigit(c) && c is not (>= 'A' and <= 'Z') && c != '-') ||
                line.BaseQuantity is <= 0m or > 99999999999999.999999m || decimal.Round(line.BaseQuantity, 6) != line.BaseQuantity ||
                line.NetAmount is < 0m or > 9999999999999999.9999m || decimal.Round(line.NetAmount, 4) != line.NetAmount) ||
            rows.Select(line => line.LineId).Distinct().Count() != rows.Length || rows.Sum(line => line.NetAmount) > 9999999999999999.9999m)
            throw new ArgumentException("Invoice capture requires bounded unique lines and exact quantities/amounts.");
        TenantId = tenantId; CompanyId = companyId; InvoiceId = invoiceId; ActorId = actorId; PartyAccountId = partyAccountId;
        DocumentNumber = documentNumber.Trim(); DocumentDate = documentDate; Currency = currency;
        Lines = Array.AsReadOnly(rows.OrderBy(line => line.LineId).ToArray());
        TotalNetAmount = rows.Sum(line => line.NetAmount);
        Fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Format = "supplier-invoice-capture/v1",
            TenantId,
            CompanyId,
            InvoiceId,
            ActorId,
            PartyAccountId,
            DocumentNumber,
            Date = DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Currency,
            Lines = Lines.Select(line => new
            {
                line.LineId,
                line.ItemId,
                line.BaseUomCode,
                Quantity = line.BaseQuantity.ToString("G29", CultureInfo.InvariantCulture),
                Net = line.NetAmount.ToString("G29", CultureInfo.InvariantCulture)
            }),
        })));
    }
    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid InvoiceId { get; }
    public long Version { get; } = 1;
    public Guid ActorId { get; }
    public Guid PartyAccountId { get; }
    public string DocumentNumber { get; }
    public DateOnly DocumentDate { get; }
    public string Currency { get; }
    public IReadOnlyList<SupplierInvoiceCaptureLine> Lines { get; }
    public decimal TotalNetAmount { get; }
    public string Fingerprint { get; }
}

public sealed record SupplierInvoiceCaptureResult(Guid InvoiceId, DateTimeOffset RecordedAt, bool Created);

/// <summary>Verified persisted capture, not a validated or finalized commercial invoice.</summary>
public sealed record SupplierInvoiceCapturedDocument(SupplierInvoiceCapture Capture, DateTimeOffset RecordedAt);
