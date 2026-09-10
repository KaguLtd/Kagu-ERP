using System.Text.Json;
using KaguERP.Api.Sales;
using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class SalesStockOrderHttpContractCheck
{
    public static async Task RunAsync()
    {
        foreach (string? connectionString in new[] { null, "Host=localhost;Database=not_connected_fixture" })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["KAGU_ERP_APP_CONNECTION_STRING"] = connectionString }).Build();
            using var registration = new ServiceCollection().AddKaguErpBootstrap(configuration).BuildServiceProvider();
            using var registrationScope = registration.CreateScope();
            Assert(registrationScope.ServiceProvider.GetRequiredService<ISalesStockOrderGateway>() is UnavailableSalesStockOrderGateway,
                "A connection string silently opened the unverified compound writer.");
        }
        foreach (string invalid in new[] { "", "0", "-1", "+1", " 1", "1 ", "1,5", "1e2", ".5", "1.",
            "1.0000001", "100000000000000", "1.2.3", "١", "0.00000000000000000000000000001" })
            Assert(!SalesOrderLifecycleEndpoint.TryReadStockQuantity(invalid, out _), "Unsafe decimal accepted.");
        Assert(SalesOrderLifecycleEndpoint.TryReadStockQuantity("99999999999999.999999", out var maximum) &&
            maximum == 99999999999999.999999m, "Maximum exact quantity was lost.");
        foreach (string invalid in new[] { "\"+1\"", "\" 1\"", "\"01\"", "W/\"1\"", "\"0\"", "\"1\",\"2\"" })
        {
            var headers = new HeaderDictionary { [SalesOrderLifecycleEndpoint.VersionHeader] = invalid };
            Assert(!SalesOrderLifecycleEndpoint.TryReadExpectedVersion(headers, out _), "Noncanonical ETag accepted.");
        }

        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), actor = Guid.NewGuid(), order = Guid.NewGuid();
        Guid line = Guid.NewGuid(), warehouse = Guid.NewGuid();
        var scope = new Scope(new ExecutionScope(tenant, actor, [new CompanyAccess(company,
            ["sales.order.confirm", "sales.order.cancel", "sales.order.submit", "inventory.reservation.create", "inventory.reservation.release"])]));
        var audit = new Audit(new(Guid.NewGuid(), "stock-http", tenant, actor, new HashSet<Guid> { company }, null));
        var state = SalesOrderLifecycleState.Rehydrate(tenant, company, order, actor, 4, SalesOrderStatus.Confirmed);
        var commitment = SalesOrderCommitment.Create(tenant, company, order,
            [SalesOrderLineCommitment.Create(line, Guid.NewGuid(), "EA", SalesOrderQuantity.Create(3m))]);
        var outcome = new SalesOrderLifecyclePersistenceOutcome(state, commitment, null, true);
        var stock = new StockGateway(outcome, line, warehouse);
        var legacy = new LifecycleGateway(outcome);
        var auditWriter = new AuditWriter();
        var request = new SalesOrderTransitionApiRequest(company, null, new(2026, 9, 10), [new(line, warehouse, "3")]);
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();

        async Task<DefaultHttpContext> Invoke(string action, SalesOrderTransitionApiRequest body,
            ISalesStockOrderGateway? gateway = null, Scope? actorScope = null, string version = "\"3\"")
        {
            var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "http-contract" };
            context.Response.Body = new MemoryStream();
            await SalesOrderLifecycleEndpoint.TransitionAsync(context, order, action, body, Guid.NewGuid().ToString("D"),
                version, actorScope ?? scope, audit, auditWriter, legacy, gateway ?? stock,
                NullLogger<SalesOrderLifecycleLogCategory>.Instance);
            return context;
        }

        var confirmed = await Invoke("confirm", request);
        Assert(confirmed.Response.StatusCode == 200 && confirmed.Response.Headers.ETag == "\"4\"" &&
            stock.ConfirmCalls == 1 && legacy.Calls == 0, "Confirmation bypassed the compound gateway.");
        using (var json = Body(confirmed))
        {
            var reservation = json.RootElement.GetProperty("reservations")[0];
            Assert(reservation.GetProperty("reservedBaseQuantity").ValueKind == JsonValueKind.String &&
                reservation.GetProperty("reservedBaseQuantity").GetString() == "1.25" &&
                reservation.GetProperty("requestedBaseQuantity").GetString() == "3", "HTTP quantity precision/shape changed.");
        }
        var cancelled = await Invoke("cancel", new(company, "fixture cancel", request.EffectiveDate));
        Assert(cancelled.Response.StatusCode == 200 && stock.CancelCalls == 1 && legacy.Calls == 0,
            "Cancellation bypassed the compound gateway.");
        using (var json = Body(cancelled))
            Assert(json.RootElement.GetProperty("releases")[0].GetProperty("releasedBaseQuantity").GetString() == "1.25",
                "Release response lost exact quantity.");

        await Problem(Invoke("confirm", request with { EffectiveDate = null }), 422, "INVALID_SALES_STOCK_ORDER_INPUT");
        await Problem(Invoke("confirm", request with { ReservationLines = [new(line, warehouse, "0.0000001")] }),
            422, "INVALID_SALES_STOCK_ORDER_INPUT");
        await Problem(Invoke("confirm", request with { ReservationLines = [new(line, warehouse, "1"), new(line, warehouse, "2")] }),
            422, "INVALID_SALES_STOCK_ORDER_INPUT");
        await Problem(Invoke("cancel", request with { Reason = "reason" }), 422, "INVALID_SALES_STOCK_ORDER_INPUT");
        await Problem(Invoke("cancel", new(company, " ", request.EffectiveDate)), 422, "INVALID_SALES_STOCK_ORDER_INPUT");
        await Problem(Invoke("submit", request), 422, "INVALID_SALES_STOCK_ORDER_INPUT");
        Assert(stock.ConfirmCalls == 1 && stock.CancelCalls == 1 && legacy.Calls == 0, "Invalid input reached persistence.");

        var deniedScope = new Scope(new ExecutionScope(tenant, actor,
            [new CompanyAccess(company, ["sales.order.confirm", "sales.order.cancel"])]));
        await Problem(Invoke("confirm", request, actorScope: deniedScope), 403, "SALES_STOCK_ORDER_ACCESS_DENIED");
        await Problem(Invoke("cancel", new(company, "reason", request.EffectiveDate), actorScope: deniedScope),
            403, "SALES_STOCK_ORDER_ACCESS_DENIED");
        Assert(auditWriter.Denied == 2, "Missing inventory permissions were not audited.");
        await Problem(Invoke("confirm", request, new UnavailableSalesStockOrderGateway()),
            503, "SALES_STOCK_ORDER_SERVICE_UNAVAILABLE");
        Assert(legacy.Calls == 0, "Unavailable stock gateway fell back to legacy confirmation.");
        stock.Failure = new SalesOrderGatewayConflictException("SALES_ORDER_VERSION_CONFLICT", "private detail");
        await Problem(Invoke("confirm", request), 412, "SALES_ORDER_VERSION_CONFLICT");
        stock.Failure = new SalesOrderGatewayConflictException("INVENTORY_RESERVATION_REQUEST_CONFLICT", "private detail");
        await Problem(Invoke("confirm", request), 409, "INVENTORY_RESERVATION_REQUEST_CONFLICT");
        stock.Failure = new SalesOrderGatewayConflictException("INVENTORY_RESERVATION_VERSION_CONFLICT", "private detail");
        await Problem(Invoke("cancel", new(company, "reason", request.EffectiveDate)), 412, "INVENTORY_RESERVATION_VERSION_CONFLICT");
        stock.Failure = new SalesOrderGatewayNotFoundException();
        await Problem(Invoke("confirm", request), 404, "SALES_ORDER_NOT_FOUND");
        stock.Failure = null;
        var submitted = await Invoke("submit", new(company, null));
        Assert(submitted.Response.StatusCode == 200 && legacy.Calls == 1, "Ordinary transition routing changed.");
    }

    private static async Task Problem(Task<DefaultHttpContext> task, int status, string code)
    {
        var context = await task;
        using var json = Body(context);
        Assert(context.Response.StatusCode == status && json.RootElement.GetProperty("code").GetString() == code &&
            !json.RootElement.GetRawText().Contains("private detail", StringComparison.Ordinal), "Unsafe or incorrect Problem Details.");
    }

    private static JsonDocument Body(DefaultHttpContext context) =>
        JsonDocument.Parse(((MemoryStream)context.Response.Body).ToArray());
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed record Scope(ExecutionScope Current) : IExecutionScopeAccessor;
    private sealed record Audit(RequestAuditContext Current) : IRequestAuditContextAccessor;
    private sealed class AuditWriter : IAuthorizationAuditWriter
    {
        public int Denied { get; private set; }
        public Task WriteAsync(RequestAuditContext context, AuthorizationAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Denied++;
            return Task.CompletedTask;
        }
    }
    private sealed class LifecycleGateway(SalesOrderLifecyclePersistenceOutcome outcome) : ISalesOrderLifecycleGateway
    {
        public int Calls { get; private set; }
        public ValueTask<SalesOrderLifecyclePersistenceOutcome> TransitionAsync(AuthorizedSalesOrderTransitionCommand command,
            RequestAuditContext auditContext, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(outcome);
        }
        public ValueTask<SalesOrderLifecyclePersistenceOutcome> CreateDraftAsync(AuthorizedSalesOrderCreateCommand command,
            RequestAuditContext auditContext, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SalesOrderLifecycleView> LoadAsync(AuthorizedSalesOrderLifecycleQuery query,
            RequestAuditContext auditContext, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StockGateway(SalesOrderLifecyclePersistenceOutcome outcome, Guid line, Guid warehouse) : ISalesStockOrderGateway
    {
        public int ConfirmCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public Exception? Failure { get; set; }
        public ValueTask<SalesStockOrderConfirmationOutcome> ConfirmAsync(SalesStockOrderConfirmationCommand command,
            RequestAuditContext auditContext, CancellationToken cancellationToken = default)
        {
            ConfirmCalls++;
            if (Failure is not null) return ValueTask.FromException<SalesStockOrderConfirmationOutcome>(Failure);
            return ValueTask.FromResult(new SalesStockOrderConfirmationOutcome(outcome,
                [new(line, warehouse, Guid.NewGuid(), Guid.NewGuid(), 3m, 1.25m, DateTimeOffset.UtcNow)]));
        }
        public ValueTask<SalesStockOrderCancellationOutcome> CancelAsync(SalesStockOrderCancellationCommand command,
            RequestAuditContext auditContext, CancellationToken cancellationToken = default)
        {
            CancelCalls++;
            if (Failure is not null) return ValueTask.FromException<SalesStockOrderCancellationOutcome>(Failure);
            return ValueTask.FromResult(new SalesStockOrderCancellationOutcome(outcome,
                [new(Guid.NewGuid(), Guid.NewGuid(), 2, 1.25m, 0m, DateTimeOffset.UtcNow)]));
        }
    }
}
