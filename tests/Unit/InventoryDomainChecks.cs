using KaguERP.Modules.Inventory.Domain;

internal static class InventoryDomainChecks
{
    public static void QuantityBoundariesAreEnforced()
    {
        InventoryQuantity maximum = InventoryQuantity.Create(99999999999999.999999m);
        InventoryQuantity minimum = InventoryQuantity.Create(-99999999999999.999999m);
        Equal(decimal.Zero, (maximum + minimum).Value, "Inventory quantity did not preserve exact decimal arithmetic.");

        Expect("INVENTORY_QUANTITY_OUT_OF_RANGE", () => InventoryQuantity.Create(100000000000000m));
        Expect("INVENTORY_QUANTITY_OUT_OF_RANGE", () => InventoryQuantity.Create(0.0000001m));
    }

    public static void StockMovementBoundariesAreEnforced()
    {
        InventoryFixture fixture = CreateFixture();
        StockMovementDraft receipt = CreateMovement(fixture, StockMovementKind.Receipt, 12.345678m);
        Equal("EA", receipt.BaseUomCode, "Base UOM was not canonicalized.");
        Equal(fixture.Source, receipt.Source, "Movement source identity changed.");

        Expect(
            "INVENTORY_MOVEMENT_QUANTITY_SIGN_INVALID",
            () => CreateMovement(fixture, StockMovementKind.Issue, 1m));
        Expect(
            "INVENTORY_MOVEMENT_SOURCE_SCOPE_MISMATCH",
            () => CreateMovement(
                fixture with
                {
                    Source = StockMovementSourceIdentity.Create(
                        fixture.TenantId,
                        Guid.NewGuid(),
                        "sales.dispatch",
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        1,
                        "stock-issue"),
                },
                StockMovementKind.Receipt,
                1m));
        Expect(
            "INVENTORY_NON_TRANSFER_CONTEXT_INVALID",
            () => CreateMovement(
                fixture,
                StockMovementKind.Receipt,
                1m,
                transferId: Guid.NewGuid(),
                counterpartWarehouseId: fixture.DestinationWarehouseId));
    }

    public static void ImmediateTransferConservesQuantity()
    {
        InventoryFixture fixture = CreateFixture();
        Guid transferId = Guid.NewGuid();
        StockMovementDraft issue = CreateMovement(
            fixture,
            StockMovementKind.TransferIssue,
            -10.125m,
            transferId,
            fixture.DestinationWarehouseId);
        StockMovementDraft receipt = CreateMovement(
            fixture with { WarehouseId = fixture.DestinationWarehouseId },
            StockMovementKind.TransferReceipt,
            10.125m,
            transferId,
            fixture.WarehouseId,
            sequenceKey: 2);
        ValidatedImmediateStockTransferDraft transfer =
            ValidatedImmediateStockTransferDraft.Create(transferId, issue, receipt);

        Equal(decimal.Zero, (transfer.SourceIssue.BaseQuantity + transfer.DestinationReceipt.BaseQuantity).Value,
            "Immediate transfer did not conserve base quantity.");
        Expect(
            "INVENTORY_TRANSFER_QUANTITY_MISMATCH",
            () => ValidatedImmediateStockTransferDraft.Create(
                transferId,
                issue,
                CreateMovement(
                    fixture with { WarehouseId = fixture.DestinationWarehouseId },
                    StockMovementKind.TransferReceipt,
                    10m,
                    transferId,
                    fixture.WarehouseId,
                    sequenceKey: 2)));
        Expect(
            "INVENTORY_TRANSFER_CONTEXT_MISMATCH",
            () => ValidatedImmediateStockTransferDraft.Create(
                transferId,
                issue,
                CreateMovement(
                    fixture with { WarehouseId = fixture.DestinationWarehouseId, ItemId = Guid.NewGuid() },
                    StockMovementKind.TransferReceipt,
                    10.125m,
                    transferId,
                    fixture.WarehouseId,
                    sequenceKey: 2)));
    }

    public static void BackdateImpactPreviewIsComplete()
    {
        InventoryFixture fixture = CreateFixture();
        StockMovementDraft proposed = CreateMovement(fixture, StockMovementKind.Receipt, 5m, sequenceKey: 1);
        InventoryValuationWatermark watermark = InventoryValuationWatermark.Create(
            fixture.TenantId,
            fixture.CompanyId,
            fixture.ItemId,
            fixture.WarehouseId,
            InventoryPosition.Create(new DateOnly(2026, 9, 2), 9),
            4,
            new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero),
            new string('a', 64));
        BackdatedStockMovementImpactRequest request = BackdatedStockMovementImpactRequest.Create(proposed, watermark);
        InventoryPeriodLockImpact[] locks = Enum.GetValues<InventoryLockScope>()
            .Select(scope => InventoryPeriodLockImpact.Create(
                fixture.TenantId,
                fixture.CompanyId,
                Guid.NewGuid(),
                scope,
                scope == InventoryLockScope.InventoryValuation
                    ? InventoryPeriodState.SoftClosed
                    : InventoryPeriodState.Open,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)))
            .ToArray();
        InventoryBackdateImpactPreview preview = InventoryBackdateImpactPreview.Create(
            Guid.NewGuid(),
            request,
            new DateTimeOffset(2026, 9, 2, 13, 0, 0, TimeSpan.Zero),
            locks,
            affectedCostLayerCount: 8,
            affectedReportGenerationCount: 2,
            affectsExternalDeclaration: true,
            new string('b', 64));

        Equal(
            InventoryPeriodState.SoftClosed,
            preview.PeriodLocks.Single(item => item.Scope == InventoryLockScope.InventoryValuation).State,
            "Impact preview lost the inventory lock state.");
        Equal(8, preview.AffectedCostLayerCount, "Impact preview changed the affected cost-layer count.");
        locks[0] = InventoryPeriodLockImpact.Create(
            fixture.TenantId,
            fixture.CompanyId,
            Guid.NewGuid(),
            InventoryLockScope.Operational,
            InventoryPeriodState.HardClosed,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));
        Equal(
            InventoryPeriodState.Open,
            preview.PeriodLocks.Single(item => item.Scope == InventoryLockScope.Operational).State,
            "Impact preview retained a mutable lock collection.");

        Expect(
            "INVENTORY_MOVEMENT_NOT_BACKDATED",
            () => BackdatedStockMovementImpactRequest.Create(
                CreateMovement(fixture, StockMovementKind.Receipt, 1m, sequenceKey: 10),
                watermark));
        Expect(
            "INVENTORY_IMPACT_LOCK_COVERAGE_INCOMPLETE",
            () => InventoryBackdateImpactPreview.Create(
                Guid.NewGuid(),
                request,
                new DateTimeOffset(2026, 9, 2, 13, 0, 0, TimeSpan.Zero),
                locks.Where(item => item.Scope != InventoryLockScope.Tax),
                0,
                0,
                false,
                new string('c', 64)));
    }

    public static void ItemMasterBoundariesAreEnforced()
    {
        Guid tenantId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        ItemDefinitionDraft item = ItemDefinitionDraft.Create(
            Guid.NewGuid(),
            tenantId,
            " raw.material_01 ",
            "Hammadde 01",
            ItemKind.Stock,
            InventoryUomCode.Create(" kg "),
            ItemTrackingPolicy.Lot,
            allowsFractionalQuantity: true,
            quantityScale: 3);
        ItemCompanyActivationDraft activation = ItemCompanyActivationDraft.Create(
            tenantId,
            companyId,
            item,
            isActive: true,
            expectedVersion: 1);

        Equal("RAW.MATERIAL_01", item.Code, "Item code was not canonicalized.");
        Equal("KG", item.BaseUom.Value, "Item base UOM was not canonicalized.");
        Equal(companyId, activation.CompanyId, "Item company activation changed company scope.");
        Expect(
            "INVENTORY_ITEM_TRACKING_NOT_APPLICABLE",
            () => ItemDefinitionDraft.Create(
                Guid.NewGuid(),
                tenantId,
                "SERVICE-01",
                "Service",
                ItemKind.Service,
                InventoryUomCode.Create("EA"),
                ItemTrackingPolicy.Lot,
                false,
                0));
        Expect(
            "INVENTORY_SERIAL_ITEM_FRACTIONAL",
            () => ItemDefinitionDraft.Create(
                Guid.NewGuid(),
                tenantId,
                "SERIAL-01",
                "Serial item",
                ItemKind.Stock,
                InventoryUomCode.Create("EA"),
                ItemTrackingPolicy.Serial,
                true,
                1));
        Expect(
            "INVENTORY_ITEM_COMPANY_TENANT_MISMATCH",
            () => ItemCompanyActivationDraft.Create(
                Guid.NewGuid(),
                companyId,
                item,
                true,
                1));
    }

    private static InventoryFixture CreateFixture()
    {
        Guid tenantId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        return new InventoryFixture(
            tenantId,
            companyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            StockMovementSourceIdentity.Create(
                tenantId,
                companyId,
                " sales.dispatch ",
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                " stock-issue "));
    }

    private static StockMovementDraft CreateMovement(
        InventoryFixture fixture,
        StockMovementKind kind,
        decimal quantity,
        Guid? transferId = null,
        Guid? counterpartWarehouseId = null,
        long sequenceKey = 1) =>
        StockMovementDraft.Create(
            Guid.NewGuid(),
            fixture.TenantId,
            fixture.CompanyId,
            fixture.ItemId,
            fixture.WarehouseId,
            InventoryUomCode.Create(" ea "),
            kind,
            InventoryQuantity.Create(quantity),
            new DateOnly(2026, 9, 2),
            new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            sequenceKey,
            fixture.Source,
            transferId,
            counterpartWarehouseId);

    private static void Expect(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (InventoryInvariantException exception)
        {
            Equal(expectedCode, exception.Code, "Unexpected inventory invariant code.");
            return;
        }

        throw new InvalidOperationException($"Expected inventory invariant {expectedCode} was not thrown.");
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
        }
    }

    private sealed record InventoryFixture(
        Guid TenantId,
        Guid CompanyId,
        Guid ItemId,
        Guid WarehouseId,
        Guid DestinationWarehouseId,
        StockMovementSourceIdentity Source);
}
