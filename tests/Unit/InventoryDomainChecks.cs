using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Queries;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;

internal static class InventoryDomainChecks
{
    public static void ReservationLifecyclePreservesDemandAndQuantity()
    {
        Guid tenantId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        DateTimeOffset expiresAt = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        InventoryDemandSourceIdentity source = InventoryDemandSourceIdentity.Create(
            "sales.order",
            Guid.NewGuid(),
            Guid.NewGuid(),
            4);
        InventoryReservationState active = InventoryReservationState.CreateActive(
            Guid.NewGuid(),
            tenantId,
            companyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            InventoryUomCode.Create(" ea "),
            source,
            InventoryQuantity.Create(10m),
            expiresAt);
        InventoryReservationTransitionResult partial = InventoryReservationLifecycle.Apply(
            active,
            InventoryReservationTransition.Consume,
            1,
            InventoryQuantity.Create(4m),
            actorId,
            Guid.NewGuid(),
            expiresAt.AddHours(-1));
        InventoryReservationTransitionResult consumed = InventoryReservationLifecycle.Apply(
            partial.State,
            InventoryReservationTransition.Consume,
            2,
            InventoryQuantity.Create(6m),
            actorId,
            Guid.NewGuid(),
            expiresAt);

        Equal(InventoryReservationStatus.PartiallyConsumed, partial.State.Status,
            "Reservation did not become partially consumed.");
        Equal(6m, partial.State.RemainingQuantity.Value,
            "Reservation remaining quantity was not derived exactly.");
        Equal(InventoryReservationStatus.Consumed, consumed.State.Status,
            "Reservation did not become consumed at exact quantity.");
        Equal(decimal.Zero, consumed.State.RemainingQuantity.Value,
            "Terminal reservation retained active quantity.");
        Equal(source, consumed.State.Source, "Reservation lost its versioned demand source.");

        Expect(
            "INVENTORY_RESERVATION_CONSUMPTION_INVALID",
            () => InventoryReservationLifecycle.Apply(
                active,
                InventoryReservationTransition.Consume,
                1,
                InventoryQuantity.Create(11m),
                actorId,
                Guid.NewGuid(),
                expiresAt));
        Expect(
            "INVENTORY_RESERVATION_REASON_REQUIRED",
            () => InventoryReservationLifecycle.Apply(
                active,
                InventoryReservationTransition.Release,
                1,
                InventoryQuantity.Create(decimal.Zero),
                actorId,
                Guid.NewGuid(),
                expiresAt));
        Expect(
            "INVENTORY_RESERVATION_EXPIRY_NOT_REACHED",
            () => InventoryReservationLifecycle.Apply(
                active,
                InventoryReservationTransition.Expire,
                1,
                InventoryQuantity.Create(decimal.Zero),
                actorId,
                Guid.NewGuid(),
                expiresAt.AddTicks(-10)));
    }

    public static void ReservationAuthorizationRequiresWarehouseScope()
    {
        Guid tenantId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid warehouseId = Guid.NewGuid();
        InventoryReservationState reservation = InventoryReservationState.CreateActive(
            Guid.NewGuid(),
            tenantId,
            companyId,
            Guid.NewGuid(),
            warehouseId,
            InventoryUomCode.Create("EA"),
            InventoryDemandSourceIdentity.Create(
                "sales.order", Guid.NewGuid(), Guid.NewGuid(), 4),
            InventoryQuantity.Create(10m));
        var allowed = new ExecutionScope(
            tenantId,
            actorId,
            [new CompanyAccess(
                companyId,
                [AuthorizedInventoryReservationCandidate.RequiredPermission])]);
        InventoryWarehouseScopeEvidence evidence = InventoryWarehouseScopeEvidence.Create(
            tenantId,
            companyId,
            actorId,
            [warehouseId]);
        InventoryReservationDemandEvidence demandEvidence = InventoryReservationDemandEvidence.Create(
            tenantId,
            companyId,
            reservation.Source,
            reservation.ItemId,
            reservation.BaseUom,
            InventoryQuantity.Create(10m));
        AuthorizedInventoryReservationCandidate candidate =
            AuthorizedInventoryReservationCandidate.Create(
                allowed, evidence, demandEvidence, reservation);

        Equal(reservation, candidate.Reservation,
            "Reservation authorization changed the validated reservation intent.");
        ExpectReservationAuthorization(
            "INVENTORY_RESERVATION_PERMISSION_REQUIRED",
            () => AuthorizedInventoryReservationCandidate.Create(
                new ExecutionScope(tenantId, actorId, [companyId]),
                evidence,
                demandEvidence,
                reservation));
        ExpectReservationAuthorization(
            "INVENTORY_RESERVATION_WAREHOUSE_SCOPE_REQUIRED",
            () => AuthorizedInventoryReservationCandidate.Create(
                allowed,
                InventoryWarehouseScopeEvidence.Create(tenantId, companyId, actorId, []),
                demandEvidence,
                reservation));
        ExpectReservationAuthorization(
            "INVENTORY_RESERVATION_WAREHOUSE_EVIDENCE_MISMATCH",
            () => AuthorizedInventoryReservationCandidate.Create(
                allowed,
                InventoryWarehouseScopeEvidence.Create(
                    tenantId, companyId, Guid.NewGuid(), [warehouseId]),
                demandEvidence,
                reservation));
        ExpectReservationAuthorization(
            "INVENTORY_RESERVATION_DEMAND_EVIDENCE_MISMATCH",
            () => AuthorizedInventoryReservationCandidate.Create(
                allowed,
                evidence,
                InventoryReservationDemandEvidence.Create(
                    tenantId,
                    companyId,
                    InventoryDemandSourceIdentity.Create(
                        "sales.order", reservation.Source.SourceId, reservation.Source.SourceLineId, 3),
                    reservation.ItemId,
                    reservation.BaseUom,
                    InventoryQuantity.Create(10m)),
                reservation));
        ExpectReservationAuthorization(
            "INVENTORY_RESERVATION_EXCEEDS_DEMAND",
            () => AuthorizedInventoryReservationCandidate.Create(
                allowed,
                evidence,
                InventoryReservationDemandEvidence.Create(
                    tenantId,
                    companyId,
                    reservation.Source,
                    reservation.ItemId,
                    reservation.BaseUom,
                    InventoryQuantity.Create(9m)),
                reservation));
    }

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

        Guid reversalTransferId = Guid.NewGuid();
        InventoryFixture reversalFixture = fixture with
        {
            Source = StockMovementSourceIdentity.Create(
                fixture.TenantId,
                fixture.CompanyId,
                "inventory.transfer-reversal",
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "stock-transfer-reversal"),
        };
        StockMovementDraft reversalIssue = CreateMovement(
            reversalFixture with { WarehouseId = fixture.DestinationWarehouseId },
            StockMovementKind.TransferIssue,
            -10.125m,
            reversalTransferId,
            fixture.WarehouseId,
            sequenceKey: 3,
            reversalOfMovementId: receipt.MovementId);
        StockMovementDraft reversalReceipt = CreateMovement(
            reversalFixture,
            StockMovementKind.TransferReceipt,
            10.125m,
            reversalTransferId,
            fixture.DestinationWarehouseId,
            sequenceKey: 4,
            reversalOfMovementId: issue.MovementId);
        ValidatedImmediateStockTransferDraft reversal =
            ValidatedImmediateStockTransferDraft.Create(
                reversalTransferId,
                reversalIssue,
                reversalReceipt);
        Equal(receipt.MovementId, reversal.SourceIssue.ReversalOfMovementId,
            "Transfer reversal lost the original destination receipt link.");
        Expect(
            "INVENTORY_TRANSFER_REVERSAL_PAIR_INCOMPLETE",
            () => ValidatedImmediateStockTransferDraft.Create(
                reversalTransferId,
                reversalIssue,
                CreateMovement(
                    reversalFixture,
                    StockMovementKind.TransferReceipt,
                    10.125m,
                    reversalTransferId,
                    fixture.DestinationWarehouseId,
                    sequenceKey: 4)));
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

    public static void TransferAuthorizationBoundariesAreEnforced()
    {
        InventoryFixture fixture = CreateFixture();
        Guid transferId = Guid.NewGuid();
        ValidatedImmediateStockTransferDraft transfer = ValidatedImmediateStockTransferDraft.Create(
            transferId,
            CreateMovement(
                fixture,
                StockMovementKind.TransferIssue,
                -4m,
                transferId,
                fixture.DestinationWarehouseId),
            CreateMovement(
                fixture with { WarehouseId = fixture.DestinationWarehouseId },
                StockMovementKind.TransferReceipt,
                4m,
                transferId,
                fixture.WarehouseId,
                sequenceKey: 2));
        var permittedScope = new ExecutionScope(
            fixture.TenantId,
            Guid.NewGuid(),
            [new CompanyAccess(
                fixture.CompanyId,
                [AuthorizedImmediateStockTransferCandidate.RequiredPermission])]);
        InventoryWarehouseScopeEvidence warehouseEvidence = InventoryWarehouseScopeEvidence.Create(
            fixture.TenantId,
            fixture.CompanyId,
            permittedScope.ActorId,
            [fixture.WarehouseId, fixture.DestinationWarehouseId]);
        AuthorizedImmediateStockTransferCandidate candidate =
            AuthorizedImmediateStockTransferCandidate.Create(
                permittedScope,
                warehouseEvidence,
                transfer);

        Equal(transfer, candidate.Transfer, "Authorized candidate changed the validated transfer.");
        ExpectAuthorization(
            "INVENTORY_TRANSFER_PERMISSION_REQUIRED",
            () => AuthorizedImmediateStockTransferCandidate.Create(
                new ExecutionScope(fixture.TenantId, Guid.NewGuid(), [fixture.CompanyId]),
                warehouseEvidence,
                transfer));
        ExpectAuthorization(
            "INVENTORY_TRANSFER_WAREHOUSE_SCOPE_REQUIRED",
            () => AuthorizedImmediateStockTransferCandidate.Create(
                permittedScope,
                InventoryWarehouseScopeEvidence.Create(
                    fixture.TenantId,
                    fixture.CompanyId,
                    permittedScope.ActorId,
                    [fixture.WarehouseId]),
                transfer));
        ExpectAuthorization(
            "INVENTORY_TRANSFER_WAREHOUSE_EVIDENCE_MISMATCH",
            () => AuthorizedImmediateStockTransferCandidate.Create(
                permittedScope,
                InventoryWarehouseScopeEvidence.Create(
                    fixture.TenantId,
                    fixture.CompanyId,
                    Guid.NewGuid(),
                    [fixture.WarehouseId, fixture.DestinationWarehouseId]),
                transfer));
        try
        {
            _ = AuthorizedImmediateStockTransferCandidate.Create(
                new ExecutionScope(
                    fixture.TenantId,
                    Guid.NewGuid(),
                    [new CompanyAccess(
                        Guid.NewGuid(),
                        [AuthorizedImmediateStockTransferCandidate.RequiredPermission])]),
                warehouseEvidence,
                transfer);
        }
        catch (ExecutionScopeDeniedException)
        {
            return;
        }

        throw new InvalidOperationException("Cross-company transfer authorization was not rejected.");
    }

    public static void OnHandQueryBoundariesAreEnforced()
    {
        InventoryFixture fixture = CreateFixture();
        Guid actorId = Guid.NewGuid();
        var permittedScope = new ExecutionScope(
            fixture.TenantId,
            actorId,
            [new CompanyAccess(fixture.CompanyId, [AuthorizedInventoryOnHandQuery.RequiredPermission])]);
        InventoryWarehouseScopeEvidence evidence = InventoryWarehouseScopeEvidence.Create(
            fixture.TenantId,
            fixture.CompanyId,
            actorId,
            [fixture.WarehouseId]);
        DateTimeOffset cutoff = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        AuthorizedInventoryOnHandQuery query = AuthorizedInventoryOnHandQuery.Create(
            permittedScope,
            evidence,
            fixture.CompanyId,
            new DateOnly(2026, 9, 4),
            cutoff,
            fixture.ItemId);
        Equal(cutoff, query.RecordedCutoff, "On-hand query changed its recorded cutoff.");

        ExpectOnHandAuthorization(
            "INVENTORY_ON_HAND_PERMISSION_REQUIRED",
            () => AuthorizedInventoryOnHandQuery.Create(
                new ExecutionScope(fixture.TenantId, actorId, [fixture.CompanyId]),
                evidence,
                fixture.CompanyId,
                new DateOnly(2026, 9, 4),
                cutoff));
        ExpectOnHandAuthorization(
            "INVENTORY_ON_HAND_RECORDED_CUTOFF_NOT_UTC",
            () => AuthorizedInventoryOnHandQuery.Create(
                permittedScope,
                evidence,
                fixture.CompanyId,
                new DateOnly(2026, 9, 4),
                cutoff.ToOffset(TimeSpan.FromHours(3))));
        ExpectOnHandAuthorization(
            "INVENTORY_ON_HAND_WAREHOUSE_SCOPE_REQUIRED",
            () => AuthorizedInventoryOnHandQuery.Create(
                permittedScope,
                InventoryWarehouseScopeEvidence.Create(
                    fixture.TenantId,
                    fixture.CompanyId,
                    actorId,
                    []),
                fixture.CompanyId,
                new DateOnly(2026, 9, 4),
                cutoff));

        var lines = new List<InventoryOnHandLine>
        {
            new(fixture.ItemId, fixture.WarehouseId, InventoryUomCode.Create("EA"), InventoryQuantity.Create(5m)),
        };
        var snapshot = new InventoryOnHandSnapshot(
            fixture.TenantId,
            fixture.CompanyId,
            new DateOnly(2026, 9, 4),
            cutoff,
            lines);
        lines.Clear();
        Equal(1, snapshot.Lines.Count, "On-hand snapshot did not defensively copy its lines.");
    }

    public static void MovementQueryBoundariesAreEnforced()
    {
        InventoryFixture fixture = CreateFixture();
        Guid actorId = Guid.NewGuid();
        var permittedScope = new ExecutionScope(
            fixture.TenantId,
            actorId,
            [new CompanyAccess(fixture.CompanyId, [AuthorizedInventoryMovementQuery.RequiredPermission])]);
        InventoryWarehouseScopeEvidence evidence = InventoryWarehouseScopeEvidence.Create(
            fixture.TenantId,
            fixture.CompanyId,
            actorId,
            [fixture.WarehouseId]);
        DateTimeOffset cutoff = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        AuthorizedInventoryMovementQuery query = AuthorizedInventoryMovementQuery.Create(
            permittedScope,
            evidence,
            fixture.CompanyId,
            fixture.ItemId,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 4),
            cutoff,
            200);
        Equal(200, query.PageSize, "Movement query changed its page size.");

        ExpectMovementQuery(
            "INVENTORY_MOVEMENT_PERMISSION_REQUIRED",
            () => AuthorizedInventoryMovementQuery.Create(
                new ExecutionScope(fixture.TenantId, actorId, [fixture.CompanyId]),
                evidence,
                fixture.CompanyId,
                fixture.ItemId,
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 4),
                cutoff,
                20));
        ExpectMovementQuery(
            "INVENTORY_MOVEMENT_QUERY_INVALID",
            () => AuthorizedInventoryMovementQuery.Create(
                permittedScope,
                evidence,
                fixture.CompanyId,
                fixture.ItemId,
                new DateOnly(2026, 9, 4),
                new DateOnly(2026, 9, 1),
                cutoff,
                201));
        ExpectMovementQuery(
            "INVENTORY_MOVEMENT_CURSOR_INVALID",
            () => InventoryMovementCursor.Create(
                new DateOnly(2026, 9, 4),
                cutoff.ToOffset(TimeSpan.FromHours(3)),
                fixture.WarehouseId,
                1,
                Guid.NewGuid()));
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
        long sequenceKey = 1,
        Guid? reversalOfMovementId = null) =>
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
            counterpartWarehouseId,
            reversalOfMovementId);

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

    private static void ExpectAuthorization(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (InventoryTransferAuthorizationException exception)
        {
            Equal(expectedCode, exception.Code, "Unexpected inventory authorization code.");
            return;
        }

        throw new InvalidOperationException($"Expected inventory authorization {expectedCode} was not thrown.");
    }

    private static void ExpectReservationAuthorization(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (InventoryReservationAuthorizationException exception) when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"Expected inventory reservation authorization error {expectedCode}.");
    }

    private static void ExpectOnHandAuthorization(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (InventoryOnHandAuthorizationException exception) when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"Expected inventory on-hand authorization error {expectedCode}.");
    }

    private static void ExpectMovementQuery(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (InventoryMovementQueryException exception) when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"Expected inventory movement query error {expectedCode}.");
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
