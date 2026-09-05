using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Sales.Application.Orders;
using Npgsql;

namespace KaguERP.Modules.Sales.Infrastructure.Persistence;

public delegate ValueTask AppendSalesOrderAudit(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    RequestAuditContext context,
    Guid auditEventId,
    AuthorizationAuditEvent auditEvent,
    CancellationToken cancellationToken);

public sealed class PostgresSalesOrderLifecycleGateway(
    NpgsqlDataSource dataSource,
    AppendSalesOrderAudit appendAudit)
    : ISalesOrderLifecycleGateway
{
    public async ValueTask<SalesOrderLifecyclePersistenceOutcome> CreateDraftAsync(
        AuthorizedSalesOrderCreateCommand command,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateAuditContext(command.Scope, auditContext);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        SalesOrderLifecyclePersistenceResult persisted;
        try
        {
            persisted = await PostgresSalesOrderLifecycleWriter.CreateDraftAsync(
                connection, transaction, command, cancellationToken);
        }
        catch (SalesOrderPersistenceConflictException exception)
        {
            throw new SalesOrderGatewayConflictException(exception.Code, exception.Message);
        }
        await AppendAuditAsync(
            connection, transaction, auditContext, command.CompanyId, "sales.order.create", command.OrderId,
            persisted.Created ? "SALES_ORDER_CREATED" : "SALES_ORDER_CREATE_REPLAYED", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SalesOrderLifecyclePersistenceOutcome(
            persisted.State,
            persisted.Commitment,
            persisted.Event,
            persisted.Created);
    }

    public async ValueTask<SalesOrderLifecyclePersistenceOutcome> TransitionAsync(
        AuthorizedSalesOrderTransitionCommand command,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateAuditContext(command.Scope, auditContext);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        SalesOrderLifecyclePersistenceResult persisted;
        try
        {
            persisted = await PostgresSalesOrderLifecycleWriter.TransitionAsync(
                connection, transaction, command, cancellationToken);
        }
        catch (SalesOrderNotFoundException)
        {
            throw new SalesOrderGatewayNotFoundException();
        }
        catch (SalesOrderPersistenceConflictException exception)
        {
            throw new SalesOrderGatewayConflictException(exception.Code, exception.Message);
        }
        await AppendAuditAsync(
            connection, transaction, auditContext, command.CompanyId,
            $"sales.order.{command.Transition.ToString().ToLowerInvariant()}",
            command.OrderId, persisted.Created ? "SALES_ORDER_TRANSITION_APPLIED" : "SALES_ORDER_TRANSITION_REPLAYED",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SalesOrderLifecyclePersistenceOutcome(
            persisted.State,
            persisted.Commitment,
            persisted.Event,
            persisted.Created);
    }

    public async ValueTask<SalesOrderLifecycleView> LoadAsync(
        AuthorizedSalesOrderLifecycleQuery query,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateAuditContext(query.Scope, auditContext);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        SalesOrderLifecycleView view;
        try
        {
            view = await PostgresSalesOrderLifecycleLoader.LoadAsync(
                connection, transaction, query, cancellationToken);
        }
        catch (SalesOrderNotFoundException)
        {
            throw new SalesOrderGatewayNotFoundException();
        }
        await AppendAuditAsync(
            connection, transaction, auditContext, query.CompanyId, "sales.order.view", query.OrderId,
            "SALES_ORDER_VIEW_ALLOWED", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    private async ValueTask AppendAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RequestAuditContext auditContext,
        Guid companyId,
        string action,
        Guid orderId,
        string reasonCode,
        CancellationToken cancellationToken) =>
        await appendAudit(
            connection,
            transaction,
            auditContext with { CompanyIds = new HashSet<Guid> { companyId } },
            Guid.CreateVersion7(),
            new AuthorizationAuditEvent(
                action,
                "sales-order",
                orderId.ToString("D"),
                "allowed",
                reasonCode),
            cancellationToken);

    private static void ValidateAuditContext(
        KaguERP.BuildingBlocks.Application.Security.ExecutionScope scope,
        RequestAuditContext auditContext)
    {
        ArgumentNullException.ThrowIfNull(auditContext);
        if (auditContext.TenantId != scope.TenantId || auditContext.ActorId != scope.ActorId ||
            !auditContext.CompanyIds.SetEquals(scope.CompanyIds))
        {
            throw new ArgumentException("Sales order audit context must match the trusted execution scope.", nameof(auditContext));
        }
    }
}
