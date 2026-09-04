using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertSalesOrderLifecycleFoundationAsync(
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid makerId)
    {
        Guid approverId = Guid.CreateVersion7();
        Guid orderId = Guid.CreateVersion7();
        var makerScope = SalesScope(
            tenantId,
            companyId,
            makerId,
            AuthorizedSalesOrderCreateCommand.RequiredPermission,
            "sales.order.submit");
        var approverScope = SalesScope(
            tenantId,
            companyId,
            approverId,
            "sales.order.approve",
            "sales.order.confirm",
            AuthorizedSalesOrderLifecycleQuery.RequiredPermission);
        DateTimeOffset submittedAt = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset approvedAt = submittedAt.AddMinutes(1);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            SalesOrderLifecyclePersistenceResult created =
                await PostgresSalesOrderLifecycleWriter.CreateDraftAsync(
                    connection,
                    transaction,
                    AuthorizedSalesOrderCreateCommand.Create(makerScope, companyId, orderId));
            SalesOrderLifecyclePersistenceResult createReplay =
                await PostgresSalesOrderLifecycleWriter.CreateDraftAsync(
                    connection,
                    transaction,
                    AuthorizedSalesOrderCreateCommand.Create(makerScope, companyId, orderId));
            Assert(created.Created && !createReplay.Created && createReplay.State == created.State,
                "Sales order draft retry did not return its immutable first state.");

            SalesOrderLifecyclePersistenceResult submitted =
                await PostgresSalesOrderLifecycleWriter.TransitionAsync(
                    connection,
                    transaction,
                    AuthorizedSalesOrderTransitionCommand.Create(
                        makerScope,
                        companyId,
                        orderId,
                        1,
                        SalesOrderTransition.Submit,
                        Guid.CreateVersion7(),
                        submittedAt));
            Guid approveCorrelation = Guid.CreateVersion7();
            AuthorizedSalesOrderTransitionCommand approve = AuthorizedSalesOrderTransitionCommand.Create(
                approverScope,
                companyId,
                orderId,
                submitted.State.Version,
                SalesOrderTransition.Approve,
                approveCorrelation,
                approvedAt);
            SalesOrderLifecyclePersistenceResult approved =
                await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction, approve);
            SalesOrderLifecyclePersistenceResult approveReplay =
                await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction, approve);
            Assert(approved.Created && !approveReplay.Created && approveReplay.Event == approved.Event,
                "Sales order transition correlation retry was not idempotent.");

            SalesOrderPersistenceConflictException conflict = await ThrowsAsync<SalesOrderPersistenceConflictException>(
                async () => await PostgresSalesOrderLifecycleWriter.TransitionAsync(
                    connection,
                    transaction,
                    AuthorizedSalesOrderTransitionCommand.Create(
                        approverScope,
                        companyId,
                        orderId,
                        submitted.State.Version,
                        SalesOrderTransition.Approve,
                        approveCorrelation,
                        approvedAt.AddMinutes(1))));
            Assert(conflict.Code == "SALES_ORDER_IDEMPOTENCY_CONFLICT",
                "Sales order correlation reuse with different content was not rejected.");

            SalesOrderLifecyclePersistenceResult confirmed =
                await PostgresSalesOrderLifecycleWriter.TransitionAsync(
                    connection,
                    transaction,
                    AuthorizedSalesOrderTransitionCommand.Create(
                        approverScope,
                        companyId,
                        orderId,
                        approved.State.Version,
                        SalesOrderTransition.Confirm,
                        Guid.CreateVersion7(),
                        approvedAt.AddMinutes(1)));
            Assert(confirmed.State.Status == SalesOrderStatus.Confirmed && confirmed.State.Version == 4,
                "Sales order lifecycle did not persist draft-submit-approve-confirm exactly once.");
            SalesOrderLifecycleView lifecycleView = await PostgresSalesOrderLifecycleLoader.LoadAsync(
                connection,
                transaction,
                AuthorizedSalesOrderLifecycleQuery.Create(approverScope, companyId, orderId));
            Assert(lifecycleView.State == confirmed.State && lifecycleView.Transitions.Count == 3 &&
                   lifecycleView.Transitions[0].Transition == SalesOrderTransition.Submit &&
                   lifecycleView.Transitions[2].Transition == SalesOrderTransition.Confirm,
                "Sales order lifecycle query did not reconstruct its exact ordered transition history.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection hiddenConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction hiddenTransaction = await hiddenConnection.BeginTransactionAsync())
        {
            await SetScopeAsync(hiddenConnection, hiddenTransaction, tenantId, otherCompanyId);
            Assert(await CountAsync(
                    hiddenConnection,
                    hiddenTransaction,
                    "SELECT count(*) FROM sales.sales_order WHERE order_id=$1",
                    orderId) == 0,
                "Sales order was visible outside its company RLS scope.");
            var hiddenScope = SalesScope(
                tenantId,
                otherCompanyId,
                approverId,
                AuthorizedSalesOrderLifecycleQuery.RequiredPermission);
            await ThrowsAsync<SalesOrderNotFoundException>(
                async () => await PostgresSalesOrderLifecycleLoader.LoadAsync(
                    hiddenConnection,
                    hiddenTransaction,
                    AuthorizedSalesOrderLifecycleQuery.Create(hiddenScope, otherCompanyId, orderId)));
            await hiddenTransaction.CommitAsync();
        }

        await AssertSalesAppendOnlyPrivilegesAsync(appDataSource, tenantId, companyId, orderId);
    }

    private static ExecutionScope SalesScope(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        params string[] permissions) =>
        new(tenantId, actorId, [new CompanyAccess(companyId, permissions)]);

    private static async Task AssertSalesAppendOnlyPrivilegesAsync(
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid orderId)
    {
        await using NpgsqlConnection connection = await appDataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetScopeAsync(connection, transaction, tenantId, companyId);
        await using var command = new NpgsqlCommand(
            "DELETE FROM sales.sales_order WHERE order_id=$1",
            connection,
            transaction);
        command.Parameters.AddWithValue(orderId);
        PostgresException exception = await ThrowsAsync<PostgresException>(
            async () => await command.ExecuteNonQueryAsync());
        Assert(exception.SqlState == PostgresErrorCodes.InsufficientPrivilege,
            "Runtime role unexpectedly received sales-order DELETE privilege.");
        await transaction.RollbackAsync();
    }
}
