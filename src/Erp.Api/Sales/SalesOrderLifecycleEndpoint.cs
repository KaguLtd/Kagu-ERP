using KaguERP.Api.Errors;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json.Serialization;

namespace KaguERP.Api.Sales;

internal sealed record SalesOrderLineCreateApiRequest(
    Guid OrderLineId,
    Guid ItemId,
    string BaseUomCode,
    string OrderedBaseQuantity);
internal sealed record SalesOrderCreateApiRequest(
    Guid CompanyId,
    IReadOnlyList<SalesOrderLineCreateApiRequest> Lines);
internal sealed record SalesOrderTransitionApiRequest(Guid CompanyId, string? Reason);
internal sealed record SalesOrderTransitionApiResponse(
    Guid EventId,
    string Transition,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)]
    long PreviousVersion,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)]
    long NewVersion,
    DateTimeOffset OccurredAt,
    string? Reason);
internal sealed record SalesOrderLifecycleApiResponse(
    Guid Id,
    Guid CompanyId,
    Guid MakerId,
    string Status,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)]
    long Version,
    IReadOnlyList<SalesOrderLineApiResponse> Lines,
    IReadOnlyList<SalesOrderTransitionApiResponse>? Transitions);
internal sealed record SalesOrderLineApiResponse(
    Guid Id,
    Guid ItemId,
    string BaseUomCode,
    string OrderedBaseQuantity);

internal static partial class SalesOrderLifecycleEndpoint
{
    internal const string CollectionRoute = "/api/v1/sales-orders";
    internal const string DetailRoute = "/api/v1/sales-orders/{orderId:guid}";
    internal const string TransitionRoute = "/api/v1/sales-orders/{orderId:guid}/{action}";
    internal const string IdempotencyHeader = "Idempotency-Key";
    internal const string VersionHeader = "If-Match";
    internal static IReadOnlyList<string> AllowedActions { get; } =
        ["submit", "approve", "reject", "withdraw", "revise", "confirm", "cancel", "close"];

    public static IEndpointRouteBuilder MapSalesOrderLifecycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(CollectionRoute, CreateAsync)
            .WithName("CreateSalesOrderDraft")
            .WithTags("Sales Orders")
            .WithSummary("Create a draft sales order")
            .WithDescription("Uses the canonical UUID Idempotency-Key as the sales-order identity.")
            .Accepts<SalesOrderCreateApiRequest>("application/json")
            .Produces<SalesOrderLifecycleApiResponse>(StatusCodes.Status201Created)
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status503ServiceUnavailable, "application/problem+json");
        endpoints.MapGet(DetailRoute, GetAsync)
            .WithName("GetSalesOrderLifecycle")
            .WithTags("Sales Orders")
            .WithSummary("Get a sales order lifecycle")
            .Produces<SalesOrderLifecycleApiResponse>(StatusCodes.Status200OK)
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status503ServiceUnavailable, "application/problem+json");
        endpoints.MapPost(TransitionRoute, TransitionAsync)
            .WithName("TransitionSalesOrder")
            .WithTags("Sales Orders")
            .WithSummary("Apply a sales order lifecycle transition")
            .WithDescription("Requires a canonical UUID Idempotency-Key and a quoted positive If-Match version.")
            .Accepts<SalesOrderTransitionApiRequest>("application/json")
            .Produces<SalesOrderLifecycleApiResponse>(StatusCodes.Status200OK)
            .Produces<ApiProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status412PreconditionFailed, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")
            .Produces<ApiProblemResponse>(StatusCodes.Status503ServiceUnavailable, "application/problem+json");
        return endpoints;
    }

    internal static bool TryReadIdempotencyKey(IHeaderDictionary headers, out Guid key)
    {
        key = Guid.Empty;
        StringValues values = headers[IdempotencyHeader];
        return values.Count == 1 && Guid.TryParseExact(values[0], "D", out key) && key != Guid.Empty;
    }

    internal static bool TryReadExpectedVersion(IHeaderDictionary headers, out long version)
    {
        version = 0;
        StringValues values = headers[VersionHeader];
        string? value = values.Count == 1 ? values[0] : null;
        return value is { Length: >= 3 } && value[0] == '"' && value[^1] == '"' &&
            long.TryParse(value[1..^1], out version) && version > 0;
    }

    internal static bool TryResolveTransition(string action, out SalesOrderTransition transition)
    {
        transition = action.ToLowerInvariant() switch
        {
            "submit" => SalesOrderTransition.Submit,
            "approve" => SalesOrderTransition.Approve,
            "reject" => SalesOrderTransition.Reject,
            "withdraw" => SalesOrderTransition.Withdraw,
            "revise" => SalesOrderTransition.Revise,
            "confirm" => SalesOrderTransition.Confirm,
            "cancel" => SalesOrderTransition.Cancel,
            "close" => SalesOrderTransition.Close,
            _ => 0,
        };
        return transition != 0;
    }

    private static async Task CreateAsync(
        HttpContext context,
        SalesOrderCreateApiRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        IExecutionScopeAccessor scopeAccessor,
        IRequestAuditContextAccessor auditContextAccessor,
        IAuthorizationAuditWriter auditWriter,
        ISalesOrderLifecycleGateway gateway,
        ILogger<SalesOrderLifecycleLogCategory> logger)
    {
        if (request.CompanyId == Guid.Empty || request.Lines is null ||
            !TryReadIdempotencyKey(idempotencyKey, out Guid orderId))
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status400BadRequest, "INVALID_SALES_ORDER_CREATE");
            return;
        }

        try
        {
            ExecutionScope scope = scopeAccessor.Current;
            SalesOrderCommitment commitment = CreateCommitment(scope, request, orderId);
            AuthorizedSalesOrderCreateCommand command = AuthorizedSalesOrderCreateCommand.Create(
                scope, request.CompanyId, orderId, commitment);
            SalesOrderLifecyclePersistenceOutcome outcome = await gateway.CreateDraftAsync(
                command, auditContextAccessor.Current, context.RequestAborted);
            context.Response.Headers.Location =
                $"{CollectionRoute}/{outcome.State.OrderId:D}?companyId={outcome.State.CompanyId:D}";
            context.Response.Headers.ETag = QuoteVersion(outcome.State.Version);
            await Results.Json(CreateResponse(outcome.State, outcome.Commitment), statusCode: StatusCodes.Status201Created)
                .ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            await HandleFailureAsync(
                context, exception, request.CompanyId, orderId, "sales.order.create",
                auditContextAccessor, auditWriter, logger);
        }
    }

    private static async Task GetAsync(
        HttpContext context,
        Guid orderId,
        Guid companyId,
        IExecutionScopeAccessor scopeAccessor,
        IRequestAuditContextAccessor auditContextAccessor,
        IAuthorizationAuditWriter auditWriter,
        ISalesOrderLifecycleGateway gateway,
        ILogger<SalesOrderLifecycleLogCategory> logger)
    {
        if (companyId == Guid.Empty || orderId == Guid.Empty)
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status400BadRequest, "INVALID_SALES_ORDER_QUERY");
            return;
        }

        try
        {
            AuthorizedSalesOrderLifecycleQuery query = AuthorizedSalesOrderLifecycleQuery.Create(
                scopeAccessor.Current, companyId, orderId);
            SalesOrderLifecycleView view = await gateway.LoadAsync(
                query, auditContextAccessor.Current, context.RequestAborted);
            context.Response.Headers.ETag = QuoteVersion(view.State.Version);
            await Results.Ok(CreateResponse(view.State, view.Commitment, view.Transitions)).ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            await HandleFailureAsync(
                context, exception, companyId, orderId, "sales.order.view",
                auditContextAccessor, auditWriter, logger);
        }
    }

    private static async Task TransitionAsync(
        HttpContext context,
        Guid orderId,
        string action,
        SalesOrderTransitionApiRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        [FromHeader(Name = VersionHeader)] string? ifMatch,
        IExecutionScopeAccessor scopeAccessor,
        IRequestAuditContextAccessor auditContextAccessor,
        IAuthorizationAuditWriter auditWriter,
        ISalesOrderLifecycleGateway gateway,
        ILogger<SalesOrderLifecycleLogCategory> logger)
    {
        if (request.CompanyId == Guid.Empty || !TryResolveTransition(action, out SalesOrderTransition transition) ||
            !TryReadIdempotencyKey(idempotencyKey, out Guid correlationId) ||
            !TryReadExpectedVersion(ifMatch, out long expectedVersion))
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status400BadRequest, "INVALID_SALES_ORDER_TRANSITION");
            return;
        }

        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset occurredAt = new(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
            AuthorizedSalesOrderTransitionCommand command = AuthorizedSalesOrderTransitionCommand.Create(
                scopeAccessor.Current,
                request.CompanyId,
                orderId,
                expectedVersion,
                transition,
                correlationId,
                occurredAt,
                request.Reason);
            SalesOrderLifecyclePersistenceOutcome outcome = await gateway.TransitionAsync(
                command, auditContextAccessor.Current, context.RequestAborted);
            context.Response.Headers.ETag = QuoteVersion(outcome.State.Version);
            await Results.Ok(CreateResponse(outcome.State, outcome.Commitment)).ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            await HandleFailureAsync(
                context, exception, request.CompanyId, orderId, $"sales.order.{action.ToLowerInvariant()}",
                auditContextAccessor, auditWriter, logger);
        }
    }

    internal static SalesOrderLifecycleApiResponse CreateResponse(
        SalesOrderLifecycleState state,
        SalesOrderCommitment commitment,
        IReadOnlyList<SalesOrderTransitionEvent>? transitions = null) =>
        new(
            state.OrderId,
            state.CompanyId,
            state.MakerId,
            FormatStatus(state.Status),
            state.Version,
            commitment.Lines.Select(line => new SalesOrderLineApiResponse(
                line.OrderLineId,
                line.ItemId,
                line.BaseUomCode,
                line.OrderedQuantity.Value.ToString("0.######", CultureInfo.InvariantCulture))).ToArray(),
            transitions?.Select(item => new SalesOrderTransitionApiResponse(
                item.EventId,
                FormatTransition(item.Transition),
                item.PreviousVersion,
                item.NewVersion,
                item.OccurredAt,
                item.Reason)).ToArray());

    private static SalesOrderCommitment CreateCommitment(
        ExecutionScope scope,
        SalesOrderCreateApiRequest request,
        Guid orderId)
    {
        var lines = new List<SalesOrderLineCommitment>(request.Lines.Count);
        foreach (SalesOrderLineCreateApiRequest line in request.Lines)
        {
            if (!decimal.TryParse(
                    line.OrderedBaseQuantity,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal quantity))
            {
                throw new SalesOrderLifecycleException(
                    "SALES_ORDER_QUANTITY_INVALID",
                    "Ordered base quantity must be an invariant decimal string.");
            }

            lines.Add(SalesOrderLineCommitment.Create(
                line.OrderLineId,
                line.ItemId,
                line.BaseUomCode,
                SalesOrderQuantity.Create(quantity)));
        }

        return SalesOrderCommitment.Create(scope.TenantId, request.CompanyId, orderId, lines);
    }

    private static async Task HandleFailureAsync(
        HttpContext context,
        Exception exception,
        Guid companyId,
        Guid orderId,
        string action,
        IRequestAuditContextAccessor auditContextAccessor,
        IAuthorizationAuditWriter auditWriter,
        ILogger logger)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        (int status, string code, bool auditDenied) = exception switch
        {
            ExecutionScopeDeniedException => (StatusCodes.Status404NotFound, "SALES_ORDER_NOT_FOUND", true),
            SalesOrderGatewayNotFoundException => (StatusCodes.Status404NotFound, "SALES_ORDER_NOT_FOUND", false),
            SalesOrderAuthorizationException authorization =>
                (StatusCodes.Status403Forbidden, authorization.Code, true),
            SalesOrderGatewayConflictException persistence when persistence.Code == "SALES_ORDER_VERSION_CONFLICT" =>
                (StatusCodes.Status412PreconditionFailed, persistence.Code, false),
            SalesOrderGatewayConflictException persistence =>
                (StatusCodes.Status409Conflict, persistence.Code, false),
            SalesOrderLifecycleException lifecycle when lifecycle.Code == "SALES_ORDER_VERSION_CONFLICT" =>
                (StatusCodes.Status412PreconditionFailed, lifecycle.Code, false),
            SalesOrderLifecycleException lifecycle => (StatusCodes.Status422UnprocessableEntity, lifecycle.Code, false),
            SalesOrderLifecycleUnavailableException unavailable =>
                (StatusCodes.Status503ServiceUnavailable, unavailable.Code, false),
            _ => (StatusCodes.Status503ServiceUnavailable, "SALES_ORDER_SERVICE_UNAVAILABLE", false),
        };
        if (auditDenied)
        {
            await auditWriter.WriteAsync(
                auditContextAccessor.Current,
                new AuthorizationAuditEvent(
                    action,
                    "sales-order",
                    orderId == Guid.Empty ? null : orderId.ToString("D"),
                    "denied",
                    code),
                context.RequestAborted);
        }
        if (status >= 500)
        {
            LogFailure(logger, exception.GetType().Name, companyId);
        }
        await ApiProblemWriter.WriteAsync(context, status, code);
    }

    private static string QuoteVersion(long version) => $"\"{version}\"";

    private static bool TryReadIdempotencyKey(string? value, out Guid key)
    {
        key = Guid.Empty;
        return Guid.TryParseExact(value, "D", out key) && key != Guid.Empty;
    }

    private static bool TryReadExpectedVersion(string? value, out long version)
    {
        version = 0;
        return value is { Length: >= 3 } && value[0] == '"' && value[^1] == '"' &&
            long.TryParse(value[1..^1], out version) && version > 0;
    }

    private static string FormatStatus(SalesOrderStatus status) => status switch
    {
        SalesOrderStatus.PartiallyFulfilled => "partially_fulfilled",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static string FormatTransition(SalesOrderTransition transition) => transition switch
    {
        SalesOrderTransition.RecordPartialFulfilment => "record_partial_fulfilment",
        SalesOrderTransition.RecordFullFulfilment => "record_full_fulfilment",
        _ => transition.ToString().ToLowerInvariant(),
    };

    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Error,
        Message = "Sales order request failed safely with error type {ErrorType} for company {CompanyId}.")]
    private static partial void LogFailure(ILogger logger, string errorType, Guid companyId);
}

internal sealed class SalesOrderLifecycleLogCategory;
