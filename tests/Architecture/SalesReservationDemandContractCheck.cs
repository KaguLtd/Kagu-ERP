using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Contracts.Reservations;

internal static class SalesReservationDemandContractCheck
{
    public static async Task RunAsync()
    {
        Guid tenantId = Guid.CreateVersion7();
        Guid companyId = Guid.CreateVersion7();
        Guid orderId = Guid.CreateVersion7();
        Guid lineId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();
        SalesOrderReservationDemandSnapshot snapshot = SalesOrderReservationDemandSnapshot.Create(
            tenantId,
            companyId,
            orderId,
            4,
            [SalesOrderReservationDemandLine.Create(lineId, itemId, " ea ", 10.125m)]);

        Assert(snapshot.TenantId == tenantId && snapshot.CompanyId == companyId &&
               snapshot.OrderId == orderId && snapshot.ConfirmedVersion == 4 &&
               snapshot.Lines.Count == 1 && snapshot.Lines[0].OrderLineId == lineId &&
               snapshot.Lines[0].ItemId == itemId && snapshot.Lines[0].BaseUomCode == "EA" &&
               snapshot.Lines[0].MaximumReservableQuantity == 10.125m,
            "Sales reservation demand contract lost scope, version, item, UOM or decimal quantity.");
        Expect(
            "SALES_RESERVATION_DEMAND_LINES_INVALID",
            () => SalesOrderReservationDemandSnapshot.Create(
                tenantId, companyId, orderId, 4, []));
        Expect(
            "SALES_RESERVATION_DEMAND_LINE_INVALID",
            () => SalesOrderReservationDemandLine.Create(
                lineId, itemId, "EA", 0.0000001m));

        Guid actorId = Guid.CreateVersion7();
        Guid warehouseId = Guid.CreateVersion7();
        var adapter = new SalesOrderReservationDemandEvidenceAdapter(
            new FixedDemandSource(snapshot));
        AuthorizedInventoryReservationCandidate candidate = await adapter.BuildCandidateAsync(
            SalesOrderReservationDemandQuery.Create(tenantId, companyId, orderId, 4),
            lineId,
            Guid.CreateVersion7(),
            warehouseId,
            4.125m,
            null,
            new ExecutionScope(
                tenantId,
                actorId,
                [new CompanyAccess(companyId, [AuthorizedInventoryReservationCandidate.RequiredPermission])]),
            InventoryWarehouseScopeEvidence.Create(
                tenantId, companyId, actorId, [warehouseId]));
        Assert(candidate.Reservation.Source.SourceLineId == lineId &&
               candidate.Reservation.ItemId == itemId &&
               candidate.Reservation.BaseUom.Value == "EA" &&
               candidate.Reservation.ReservedQuantity.Value == 4.125m,
            "Sales demand did not produce an exact authorized Inventory reservation candidate.");
        await ExpectDemandLineUnavailableAsync(
            "INVENTORY_RESERVATION_DEMAND_LINE_UNAVAILABLE",
            async () => await adapter.BuildCandidateAsync(
                SalesOrderReservationDemandQuery.Create(tenantId, companyId, orderId, 4),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                warehouseId,
                1m,
                null,
                new ExecutionScope(
                    tenantId,
                    actorId,
                    [new CompanyAccess(companyId, [AuthorizedInventoryReservationCandidate.RequiredPermission])]),
                InventoryWarehouseScopeEvidence.Create(
                    tenantId, companyId, actorId, [warehouseId])));
    }

    private static async Task ExpectDemandLineUnavailableAsync(
        string expectedCode,
        Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (InventoryReservationDemandLineUnavailableException exception)
            when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"Expected inventory reservation error {expectedCode}.");
    }

    private static void Expect(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (SalesOrderReservationDemandContractException exception)
            when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected sales reservation demand contract error {expectedCode}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedDemandSource(SalesOrderReservationDemandSnapshot snapshot)
        : ISalesOrderReservationDemandSource
    {
        public ValueTask<SalesOrderReservationDemandSnapshot?> LoadAsync(
            SalesOrderReservationDemandQuery query,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SalesOrderReservationDemandSnapshot?>(snapshot);
    }
}
