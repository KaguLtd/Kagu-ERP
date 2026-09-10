using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed record SalesOrderConfirmationResult(SalesOrderLifecyclePersistenceResult Confirmation,
    IReadOnlyList<InventoryReservationRequestResult> Reservations);

/// <summary>Internal stock-only confirmation participant. No stock movement, valuation or GL posting is performed.</summary>
public static class PostgresSalesOrderConfirmationOrchestrator
{
    // Stable IDs bind each confirmation retry to the same line requests, including zero results.
    public static Guid ReservationRequestId(Guid confirmationId, Guid orderLineId)
    {
        if (confirmationId == Guid.Empty || orderLineId == Guid.Empty)
            throw new ArgumentException("Confirmation and order-line identities are required.");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"kagu.sales.confirm-reservation.v1/{confirmationId:D}/{orderLineId:D}"));
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    public static async ValueTask<SalesOrderConfirmationResult> ConfirmAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, AuthorizedSalesOrderTransitionCommand command,
        IReadOnlyList<InventoryReservationRequest> requests, RequestAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(auditContext);
        if (!ReferenceEquals(transaction.Connection, connection) ||
            transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Confirmation requires the caller's ReadCommitted transaction.");
        var snapshot = requests.ToArray();
        if (command.Transition != SalesOrderTransition.Confirm || snapshot.Length is < 1 or > 500 ||
            snapshot.Any(request => request is null) ||
            auditContext.TenantId != command.Scope.TenantId || auditContext.ActorId != command.Scope.ActorId ||
            !auditContext.CompanyIds.SetEquals(command.Scope.CompanyIds))
            throw new ArgumentException("Confirmation requires a bounded reservation selection and matching audit context.");
        foreach (var request in snapshot)
        {
            if (request.Scope.TenantId != command.Scope.TenantId || request.Scope.ActorId != command.Scope.ActorId ||
                !request.Scope.CompanyIds.SetEquals(command.Scope.CompanyIds) || request.CompanyId != command.CompanyId ||
                request.Source.SourceType != "sales.order" || request.Source.SourceId != command.OrderId ||
                request.Source.SourceVersion != checked(command.ExpectedVersion + 1) ||
                request.RequestId != ReservationRequestId(command.CorrelationId, request.Source.SourceLineId))
                throw new ArgumentException("Reservation selection must match the exact confirmation scope, version and stable line identity.");
        }
        if (snapshot.Select(request => request.Source.SourceLineId).Distinct().Count() != snapshot.Length ||
            snapshot.Select(request => request.EffectiveDate).Distinct().Count() != 1)
            throw new ArgumentException("Confirmation requires unique order lines and one effective date.");

        const string savepoint = "sales_confirm_with_reservations";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            // Must precede the Sales row update: ordinary reservation batches lock requests before that row.
            bool hasMissingResult = false;
            foreach (var request in snapshot.OrderBy(request => request.RequestId))
                hasMissingResult |= await PostgresInventoryReservationRequestGate.AcquireAsync(
                    connection, transaction, request, cancellationToken) is null;
            var confirmed = await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction, command, cancellationToken);
            if (!confirmed.Created && hasMissingResult)
                throw new SalesOrderConfirmationReservationConflictException();
            if (!confirmed.Commitment.Lines.Select(line => line.OrderLineId).ToHashSet()
                .SetEquals(snapshot.Select(request => request.Source.SourceLineId)))
                throw new ArgumentException("Every stock-order line must have exactly one reservation selection.");
            var results = await PostgresSalesOrderReservationBatch.CreateAsync(
                connection, transaction, snapshot, auditContext, cancellationToken);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                auditContext with { CompanyIds = new HashSet<Guid> { command.CompanyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("sales.order.confirm", "sales-order", command.OrderId.ToString("D"), "allowed",
                    confirmed.Created ? "SALES_ORDER_CONFIRMED_WITH_RESERVATIONS" : "SALES_ORDER_CONFIRM_RESERVATIONS_REPLAYED"), cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(confirmed, results);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}

public sealed class SalesOrderConfirmationReservationConflictException()
    : InvalidOperationException("The original confirmation does not have a complete immutable reservation result set.")
{
    public string Code { get; } = "SALES_CONFIRM_RESERVATION_RESULT_MISSING";
}
