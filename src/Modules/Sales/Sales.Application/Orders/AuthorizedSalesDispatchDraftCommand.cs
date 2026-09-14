using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Domain.Orders;

namespace KaguERP.Modules.Sales.Application.Orders;

public sealed record SalesDispatchDraftSelection(Guid OrderLineId, Guid WarehouseId, decimal Quantity);

public sealed class AuthorizedSalesDispatchDraftCommand
{
    public AuthorizedSalesDispatchDraftCommand(ExecutionScope scope, Guid companyId, Guid dispatchId,
        Guid orderId, long expectedOrderVersion, DateOnly effectiveDate, IReadOnlyList<SalesDispatchDraftSelection> lines)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(lines);
        EnsurePermission(scope, companyId);
        if (dispatchId == Guid.Empty || orderId == Guid.Empty || expectedOrderVersion <= 0 || effectiveDate == default ||
            lines.Count is < 1 or > SalesOrderCommitment.MaximumLineCount)
            throw new ArgumentException("Dispatch draft requires identities, order version, date and a bounded line selection.");
        var snapshot = lines.ToArray();
        if (snapshot.Any(line => line is null || line.OrderLineId == Guid.Empty || line.WarehouseId == Guid.Empty) ||
            snapshot.Select(line => line.OrderLineId).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("Dispatch draft lines must be unique and have warehouse identities.");
        foreach (var line in snapshot) _ = SalesOrderQuantity.Create(line.Quantity);
        Scope = scope;
        CompanyId = companyId;
        DispatchId = dispatchId;
        OrderId = orderId;
        ExpectedOrderVersion = expectedOrderVersion;
        EffectiveDate = effectiveDate;
        Lines = Array.AsReadOnly(snapshot.OrderBy(line => line.OrderLineId).ToArray());
        Fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Format = "sales-dispatch-draft/v1",
            scope.TenantId,
            CompanyId,
            scope.ActorId,
            DispatchId,
            OrderId,
            ExpectedOrderVersion,
            Date = effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Lines = Lines.Select(line => new
            {
                line.OrderLineId,
                line.WarehouseId,
                Quantity = line.Quantity.ToString("G29", CultureInfo.InvariantCulture)
            }),
        })));
    }

    public ExecutionScope Scope { get; }
    public Guid CompanyId { get; }
    public Guid DispatchId { get; }
    public Guid OrderId { get; }
    public long ExpectedOrderVersion { get; }
    public DateOnly EffectiveDate { get; }
    public IReadOnlyList<SalesDispatchDraftSelection> Lines { get; }
    public string Fingerprint { get; }

    public static void EnsurePermission(ExecutionScope scope, Guid companyId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, "dispatch.create") || !scope.HasPermission(companyId, "sales.order.view"))
            throw new SalesOrderAuthorizationException("SALES_DISPATCH_DRAFT_PERMISSION_REQUIRED",
                "The actor must be allowed to prepare dispatches and view the source order.");
    }
}

public sealed record SalesDispatchDraftLine(Guid OrderLineId, Guid WarehouseId, Guid ItemId,
    string BaseUomCode, decimal Quantity);

public sealed record SalesDispatchDraftSnapshot(Guid TenantId, Guid CompanyId, Guid DispatchId, Guid OrderId,
    long OrderVersion, DateOnly EffectiveDate, Guid CreatedBy, DateTimeOffset RecordedAt,
    IReadOnlyList<SalesDispatchDraftLine> Lines);

public sealed record SalesDispatchDraftOutcome(SalesDispatchDraftSnapshot Draft, bool Created);

public sealed class SalesDispatchDraftConflictException()
    : InvalidOperationException("The dispatch draft identity already has different immutable content.")
{
    public string Code { get; } = "SALES_DISPATCH_DRAFT_CONFLICT";
}

public sealed class SalesDispatchDraftNotFoundException()
    : InvalidOperationException("The dispatch draft is unavailable in the active scope.")
{
    public string Code { get; } = "SALES_DISPATCH_DRAFT_NOT_FOUND";
}
