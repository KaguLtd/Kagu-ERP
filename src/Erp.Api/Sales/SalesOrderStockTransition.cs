using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;

namespace KaguERP.Api.Sales;

internal sealed record SalesOrderReservationLineApiRequest(Guid OrderLineId, Guid WarehouseId,
    [property: StringLength(21), RegularExpression("^[0-9]{1,14}(\\.[0-9]{1,6})?$")]
    string RequestedBaseQuantity);

internal sealed record SalesOrderReservationApiResponse(Guid OrderLineId, Guid WarehouseId, Guid RequestId,
    Guid? ReservationId, string RequestedBaseQuantity, string ReservedBaseQuantity, DateTimeOffset RecordedAt);

internal sealed record SalesOrderReleaseApiResponse(Guid ReservationId, Guid EventId,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] long Version,
    string ReleasedBaseQuantity, string ConsumedBaseQuantity, DateTimeOffset RecordedAt);

internal sealed class SalesOrderStockInputException : ArgumentException;

internal static partial class SalesOrderLifecycleEndpoint
{
    internal static SalesStockOrderConfirmationCommand CreateStockConfirmation(
        AuthorizedSalesOrderTransitionCommand command, SalesOrderTransitionApiRequest request)
    {
        if (!command.Scope.HasPermission(command.CompanyId, "inventory.reservation.create"))
            throw new SalesStockOrderAccessException();
        if (request.EffectiveDate is not { } date || request.ReservationLines is not { Count: >= 1 and <= 500 } lines)
            throw new SalesOrderStockInputException();
        var selections = new List<SalesStockOrderLineSelection>(lines.Count);
        foreach (var line in lines)
        {
            if (line is null || !TryReadStockQuantity(line.RequestedBaseQuantity, out decimal quantity))
                throw new SalesOrderStockInputException();
            selections.Add(new(line.OrderLineId, line.WarehouseId, quantity));
        }
        try { return new(command, date, selections); }
        catch (ArgumentException) { throw new SalesOrderStockInputException(); }
    }

    internal static SalesStockOrderCancellationCommand CreateStockCancellation(
        AuthorizedSalesOrderTransitionCommand command, SalesOrderTransitionApiRequest request)
    {
        if (!command.Scope.HasPermission(command.CompanyId, "inventory.reservation.release"))
            throw new SalesStockOrderAccessException();
        if (request.EffectiveDate is not { } date || request.ReservationLines is not null)
            throw new SalesOrderStockInputException();
        try { return new(command, date); }
        catch (ArgumentException) { throw new SalesOrderStockInputException(); }
    }

    // Validate the lexical scale BEFORE decimal.TryParse: it can round excessive input digits.
    // API quantities are invariant strings, never locale-specific or JSON floating-point values.
    internal static bool TryReadStockQuantity(string? text, out decimal quantity)
    {
        quantity = 0m;
        if (text is null || text.Length is < 1 or > 21) return false;
        int dot = text.IndexOf('.');
        int integerDigits = dot < 0 ? text.Length : dot;
        if (integerDigits is < 1 or > 14 || (dot >= 0 && text.Length - dot - 1 is < 1 or > 6))
            return false;
        for (int index = 0; index < text.Length; index++)
            if (index != dot && text[index] is < '0' or > '9') return false;
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out quantity) && quantity > 0m;
    }

    private static async ValueTask<SalesOrderLifecycleApiResponse> ApplyStockTransitionAsync(
        AuthorizedSalesOrderTransitionCommand command, SalesOrderTransitionApiRequest request,
        ISalesStockOrderGateway gateway, RequestAuditContext audit, CancellationToken cancellationToken)
    {
        if (command.Transition == SalesOrderTransition.Confirm)
        {
            var result = await gateway.ConfirmAsync(CreateStockConfirmation(command, request), audit, cancellationToken);
            return CreateResponse(result.Order.State, result.Order.Commitment) with
            {
                Reservations = result.Reservations.Select(item => new SalesOrderReservationApiResponse(
                    item.OrderLineId, item.WarehouseId, item.RequestId, item.ReservationId,
                    FormatStockQuantity(item.RequestedQuantity), FormatStockQuantity(item.ReservedQuantity), item.RecordedAt)).ToArray(),
            };
        }
        var cancelled = await gateway.CancelAsync(CreateStockCancellation(command, request), audit, cancellationToken);
        return CreateResponse(cancelled.Order.State, cancelled.Order.Commitment) with
        {
            Releases = cancelled.Releases.Select(item => new SalesOrderReleaseApiResponse(item.ReservationId,
                item.EventId, item.Version, FormatStockQuantity(item.ReleasedQuantity),
                FormatStockQuantity(item.ConsumedQuantity), item.RecordedAt)).ToArray(),
        };
    }

    private static string FormatStockQuantity(decimal quantity) => quantity.ToString("0.######", CultureInfo.InvariantCulture);
}
