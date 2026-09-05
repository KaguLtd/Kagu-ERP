using System.Security.Claims;
using System.Text.Json;
using KaguERP.Api.Observability;
using KaguERP.Api.Reports;
using KaguERP.Api.Security;
using KaguERP.Api.Sales;
using KaguERP.BuildingBlocks.Application.Observability;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Application.PartyReports;
using KaguERP.Modules.Sales.Domain.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;

internal static class ApiContractCheck
{
    private static readonly string[] ExpectedSalesOrderActions =
        ["submit", "approve", "reject", "withdraw", "revise", "confirm", "cancel", "close"];

    public static async Task RunAsync()
    {
        await AssertCorrelationIsGeneratedAsync();
        await AssertValidCorrelationIsPreservedAsync();
        await AssertTelemetryUsesRouteTemplateAsync();
        await AssertInvalidCorrelationIsRejectedAsync();
        await AssertProblemCarriesCorrelationAsync();
        await AssertUnauthenticatedRequestIsRejectedAsync();
        await AssertClientScopeHeaderIsRejectedAsync();
        await AssertMissingApplicationScopeIsRejectedAsync();
        await AssertTrustedApplicationScopeContinuesAsync();
        AssertCrossScopeResourceIsRejected();
        AssertPartyReportQueryContract();
        AssertSalesOrderLifecycleContract();

        Console.WriteLine("API application-scope, correlation and safe telemetry contract checks passed.");
    }

    private static async Task AssertCorrelationIsGeneratedAsync()
    {
        var context = CreateContext(authenticated: false);
        Guid captured = Guid.Empty;
        var middleware = new CorrelationMiddleware(httpContext =>
        {
            captured = httpContext.Features.Get<CorrelationContext>()?.Id ?? Guid.Empty;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert(captured != Guid.Empty, "Server did not generate a correlation ID.");
        Assert(context.Response.Headers[CorrelationMiddleware.HeaderName] == captured.ToString("D"), "Generated correlation ID was not returned.");
    }

    private static async Task AssertValidCorrelationIsPreservedAsync()
    {
        Guid supplied = Guid.CreateVersion7();
        var context = CreateContext(authenticated: false);
        context.Request.Headers[CorrelationMiddleware.HeaderName] = supplied.ToString("D");
        Guid captured = Guid.Empty;
        var middleware = new CorrelationMiddleware(httpContext =>
        {
            captured = httpContext.Features.Get<CorrelationContext>()?.Id ?? Guid.Empty;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert(captured == supplied, "Valid client correlation ID was not preserved.");
        Assert(context.Response.Headers[CorrelationMiddleware.HeaderName] == supplied.ToString("D"), "Client correlation ID was not returned.");
    }

    private static async Task AssertTelemetryUsesRouteTemplateAsync()
    {
        var context = CreateContext(authenticated: false);
        context.Request.Path = "/api/v1/companies/sensitive-business-id";
        context.Features.Set(new CorrelationContext(Guid.CreateVersion7()));
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/v1/companies/{id}"),
            0,
            EndpointMetadataCollection.Empty,
            "company-detail"));
        var logger = new CapturingLogger();
        var middleware = new RequestTelemetryMiddleware(
            httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(context);

        Assert(logger.Message?.Contains("/api/v1/companies/{id}", StringComparison.Ordinal) == true,
            "Structured request telemetry did not use the route template.");
        Assert(logger.Message?.Contains("sensitive-business-id", StringComparison.Ordinal) == false,
            "Structured request telemetry included a raw resource path.");
    }

    private static async Task AssertInvalidCorrelationIsRejectedAsync()
    {
        var context = CreateContext(authenticated: false);
        context.Request.Headers[CorrelationMiddleware.HeaderName] = "not-a-guid";
        bool continued = false;
        var middleware = new CorrelationMiddleware(_ =>
        {
            continued = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert(context.Response.StatusCode == StatusCodes.Status400BadRequest, "Invalid correlation ID was not rejected.");
        Assert(!continued, "Request with invalid correlation ID reached the endpoint.");
        Assert(await ReadCodeAsync(context) == "INVALID_CORRELATION_ID", "Invalid correlation rejection code is unstable.");
    }

    private static async Task AssertProblemCarriesCorrelationAsync()
    {
        Guid supplied = Guid.CreateVersion7();
        var context = CreateContext(authenticated: false);
        context.Request.Headers[CorrelationMiddleware.HeaderName] = supplied.ToString("D");
        var applicationScope = new ApplicationScopeMiddleware(_ => Task.CompletedTask);
        var correlation = new CorrelationMiddleware(httpContext =>
            applicationScope.InvokeAsync(httpContext, new FixedScopeResolver(null)));

        await correlation.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body);
        string? responseCorrelation = document.RootElement.GetProperty("correlationId").GetString();
        Assert(responseCorrelation == supplied.ToString("D"), "Problem Details did not carry the request correlation ID.");
        Assert(context.Response.Headers[CorrelationMiddleware.HeaderName] == supplied.ToString("D"), "Problem response did not echo correlation ID.");
    }

    private static async Task AssertUnauthenticatedRequestIsRejectedAsync()
    {
        var context = CreateContext(authenticated: false);
        bool continued = false;
        var middleware = new ApplicationScopeMiddleware(
            _ => { continued = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FixedScopeResolver(null));

        Assert(context.Response.StatusCode == StatusCodes.Status401Unauthorized, "Anonymous API request was not rejected with 401.");
        Assert(!continued, "Anonymous API request reached the endpoint.");
        Assert(await ReadCodeAsync(context) == "AUTHENTICATION_REQUIRED", "Anonymous rejection code is unstable.");
    }

    private static async Task AssertClientScopeHeaderIsRejectedAsync()
    {
        var context = CreateContext(authenticated: true);
        context.Request.Headers["X-Tenant-Id"] = Guid.CreateVersion7().ToString();
        bool continued = false;
        var middleware = new ApplicationScopeMiddleware(
            _ => { continued = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FixedScopeResolver(CreateScope()));

        Assert(context.Response.StatusCode == StatusCodes.Status400BadRequest, "Client tenant header was not rejected.");
        Assert(!continued, "Request with a client tenant header reached the endpoint.");
        Assert(await ReadCodeAsync(context) == "UNTRUSTED_SCOPE_HEADER", "Untrusted scope header code is unstable.");
    }

    private static async Task AssertMissingApplicationScopeIsRejectedAsync()
    {
        var context = CreateContext(authenticated: true);
        bool continued = false;
        var middleware = new ApplicationScopeMiddleware(
            _ => { continued = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FixedScopeResolver(null));

        Assert(context.Response.StatusCode == StatusCodes.Status403Forbidden, "Missing ERP application scope was not rejected with 403.");
        Assert(!continued, "Request without ERP application scope reached the endpoint.");
        Assert(await ReadCodeAsync(context) == "APPLICATION_SCOPE_REQUIRED", "Missing scope rejection code is unstable.");
    }

    private static async Task AssertTrustedApplicationScopeContinuesAsync()
    {
        ExecutionScope expected = CreateScope();
        var context = CreateContext(authenticated: true);
        bool continued = false;
        var middleware = new ApplicationScopeMiddleware(
            httpContext =>
            {
                continued = ReferenceEquals(httpContext.Features.Get<ExecutionScope>(), expected);
                return Task.CompletedTask;
            });

        await middleware.InvokeAsync(context, new FixedScopeResolver(expected));

        Assert(continued, "Trusted ERP application scope was not forwarded to the endpoint.");
    }

    private static void AssertCrossScopeResourceIsRejected()
    {
        ExecutionScope scope = CreateScope();
        Guid allowedCompany = scope.CompanyIds.Single();

        Assert(scope.Allows(scope.TenantId, allowedCompany), "Authorized company was rejected.");
        Assert(!scope.Allows(Guid.CreateVersion7(), allowedCompany), "Cross-tenant resource was allowed.");
        Assert(!scope.Allows(scope.TenantId, Guid.CreateVersion7()), "Unauthorized company was allowed.");

        try
        {
            scope.EnsureAllowed(scope.TenantId, Guid.CreateVersion7());
            throw new InvalidOperationException("Cross-company guard did not throw.");
        }
        catch (ExecutionScopeDeniedException)
        {
        }
    }

    private static void AssertPartyReportQueryContract()
    {
        Assert(PartyReportQueryEndpoint.Route == "/api/v1/reports/{code}/queries",
            "Party report query route changed unexpectedly.");
        Assert(PartyAccountDetailReportDefinition.ReportCode == "party.account.detail" &&
               PartyAccountDetailReportDefinition.Version == 1 &&
               PartyAccountDetailReportDefinition.ViewPermission == "reporting.party-account.view",
            "Production Party report definition or permission contract changed unexpectedly.");
        Assert(PartyReportQueryEndpoint.IsValidRequest(new PartyReportQueryApiRequest(
                Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7())),
            "Valid Party report API request was rejected.");
        Assert(!PartyReportQueryEndpoint.IsValidRequest(new PartyReportQueryApiRequest(
                Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7())),
            "Party report API accepted an empty company ID.");
        Assert(PartyReportQueryEndpoint.FormatAmount(1234.5m) == "1234.5000" &&
               PartyReportQueryEndpoint.FormatAmount(-0.125m) == "-0.1250",
            "Party report monetary JSON contract is not an invariant four-decimal string.");
    }

    private static void AssertSalesOrderLifecycleContract()
    {
        Assert(SalesOrderLifecycleEndpoint.CollectionRoute == "/api/v1/sales-orders" &&
               SalesOrderLifecycleEndpoint.DetailRoute == "/api/v1/sales-orders/{orderId:guid}" &&
               SalesOrderLifecycleEndpoint.TransitionRoute == "/api/v1/sales-orders/{orderId:guid}/{action}",
            "Sales order lifecycle API routes changed unexpectedly.");

        Guid idempotencyKey = Guid.CreateVersion7();
        var context = new DefaultHttpContext();
        context.Request.Headers[SalesOrderLifecycleEndpoint.IdempotencyHeader] = idempotencyKey.ToString("D");
        context.Request.Headers[SalesOrderLifecycleEndpoint.VersionHeader] = "\"7\"";
        Assert(SalesOrderLifecycleEndpoint.TryReadIdempotencyKey(context.Request.Headers, out Guid parsedKey) &&
               parsedKey == idempotencyKey,
            "Sales order API rejected a canonical UUID idempotency key.");
        Assert(SalesOrderLifecycleEndpoint.TryReadExpectedVersion(context.Request.Headers, out long version) &&
               version == 7,
            "Sales order API rejected a quoted positive If-Match version.");
        Assert(SalesOrderLifecycleEndpoint.TryResolveTransition("confirm", out SalesOrderTransition transition) &&
               transition == SalesOrderTransition.Confirm &&
               !SalesOrderLifecycleEndpoint.TryResolveTransition("post", out _) &&
               SalesOrderLifecycleEndpoint.AllowedActions.SequenceEqual(ExpectedSalesOrderActions),
            "Sales order API action allowlist changed unexpectedly.");

        Guid tenantId = Guid.CreateVersion7();
        Guid companyId = Guid.CreateVersion7();
        Guid orderId = Guid.CreateVersion7();
        Guid makerId = Guid.CreateVersion7();
        SalesOrderLifecycleState state = SalesOrderLifecycleState.CreateDraft(
            tenantId, companyId, orderId, makerId);
        Guid orderLineId = Guid.CreateVersion7();
        Guid itemId = Guid.CreateVersion7();
        SalesOrderCommitment commitment = SalesOrderCommitment.Create(
            tenantId,
            companyId,
            orderId,
            [SalesOrderLineCommitment.Create(
                orderLineId,
                itemId,
                " ea ",
                SalesOrderQuantity.Create(10.125m))]);
        SalesOrderLifecycleApiResponse response = SalesOrderLifecycleEndpoint.CreateResponse(state, commitment);
        Assert(response.Id == orderId && response.CompanyId == companyId && response.Status == "draft" &&
               response.Version == 1 && response.Transitions is null && response.Lines.Count == 1 &&
               response.Lines[0].Id == orderLineId && response.Lines[0].ItemId == itemId &&
               response.Lines[0].BaseUomCode == "EA" &&
               response.Lines[0].OrderedBaseQuantity == "10.125",
            "Sales order API response lost aggregate identity or version semantics.");

        context.Request.Headers[SalesOrderLifecycleEndpoint.IdempotencyHeader] = "not-a-guid";
        context.Request.Headers[SalesOrderLifecycleEndpoint.VersionHeader] = "7";
        Assert(!SalesOrderLifecycleEndpoint.TryReadIdempotencyKey(context.Request.Headers, out _) &&
               !SalesOrderLifecycleEndpoint.TryReadExpectedVersion(context.Request.Headers, out _),
            "Sales order API accepted malformed idempotency or If-Match headers.");
    }

    private static DefaultHttpContext CreateContext(bool authenticated)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/companies";
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "contract-trace";
        context.User = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-subject")], "contract-test"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        return context;
    }

    private static ExecutionScope CreateScope() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), [Guid.CreateVersion7()]);

    private static async Task<string?> ReadCodeAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedScopeResolver(ExecutionScope? scope) : IExecutionScopeResolver
    {
        public ValueTask<ExecutionScope?> ResolveAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(scope);
    }

    private sealed class CapturingLogger : ILogger<RequestTelemetryMiddleware>
    {
        public string? Message { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Message = formatter(state, exception);

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
