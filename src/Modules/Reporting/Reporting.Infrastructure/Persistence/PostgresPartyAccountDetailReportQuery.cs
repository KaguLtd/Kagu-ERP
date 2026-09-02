using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Application.PartyReports;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;
using ApplicationPartyAgingBucket = KaguERP.Modules.Reporting.Application.PartyReports.PartyAgingBucket;
using ApplicationPartyAgingItem = KaguERP.Modules.Reporting.Application.PartyReports.PartyAgingItem;
using ApplicationPartyStatementLine = KaguERP.Modules.Reporting.Application.PartyReports.PartyStatementLine;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed class PostgresPartyAccountDetailReportQuery(
    NpgsqlDataSource dataSource,
    IExecutionScopeAccessor scopeAccessor,
    IRequestAuditContextAccessor auditContextAccessor,
    AppendPartyReportAudit appendAudit) : IPartyAccountDetailReportQuery
{
    public async ValueTask<PartyAccountDetailReportQueryResult> ExecuteAsync(
        PartyAccountDetailReportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executor = new PostgresAuditedPartyReportQueryExecutor(
            dataSource,
            scopeAccessor.Current,
            auditContextAccessor.Current,
            appendAudit);
        var query = new PostgresPartyReportQuery(
            request.CompanyId,
            PartyAccountDetailReportDefinition.ReportCode,
            PartyAccountDetailReportDefinition.Version,
            PartyAccountDetailReportDefinition.ViewPermission,
            Guid.CreateVersion7(),
            request.StatementId,
            request.AgingReportId,
            Guid.CreateVersion7());
        try
        {
            PartyStatementAgingCrossFoot? result = await executor.ExecuteAsync(query, cancellationToken);
            return result is null
                ? new PartyAccountDetailReportQueryResult(
                    PartyAccountDetailReportQueryOutcome.NotFound,
                    null)
                : new PartyAccountDetailReportQueryResult(
                    PartyAccountDetailReportQueryOutcome.Allowed,
                    CreateReport(result));
        }
        catch (PartyReportQueryDeniedException)
        {
            return new PartyAccountDetailReportQueryResult(
                PartyAccountDetailReportQueryOutcome.Denied,
                null);
        }
    }

    private static PartyAccountDetailReport CreateReport(PartyStatementAgingCrossFoot result)
    {
        var statement = result.Statement;
        var aging = result.Aging;
        var slice = statement.ReportSlice;
        PartyReportDimension[] dimensions = slice.Dimensions.Assignments
            .Select(item => new PartyReportDimension(item.DimensionCode, item.ValueCode))
            .ToArray();
        ApplicationPartyStatementLine[] statementLines = statement.Lines
            .Select(line => new ApplicationPartyStatementLine(
                line.EventSnapshot.EventId,
                MapStatementKind(line.EventSnapshot.Kind),
                line.EventSnapshot.SourceType,
                line.EventSnapshot.SourceEventId,
                line.EventSnapshot.DueScheduleLineId,
                line.EventSnapshot.PaymentId,
                line.EventSnapshot.ExposureEffect,
                line.EventSnapshot.EffectiveDate,
                line.EventSnapshot.RecordedAt,
                line.RunningExposure))
            .ToArray();
        ApplicationPartyAgingBucket[] buckets = aging.Policy.Buckets
            .Zip(aging.BucketSummaries)
            .Select(pair => new ApplicationPartyAgingBucket(
                pair.First.Code,
                pair.First.MinimumDaysOverdue,
                pair.First.MaximumDaysOverdue,
                pair.Second.ItemCount,
                pair.Second.RemainingAmount))
            .ToArray();
        ApplicationPartyAgingItem[] items = aging.Items
            .Select(item => new ApplicationPartyAgingItem(
                item.OpenItemId,
                item.SourceEventId,
                item.DueScheduleLineId,
                item.OriginalAmount,
                item.RemainingAmount,
                item.DueDate,
                item.DaysOverdue,
                aging.Policy.Resolve(item.DaysOverdue).Code,
                item.IsDisputed,
                item.IsBlocked))
            .ToArray();
        return new PartyAccountDetailReport(
            result.CrossFootId,
            slice.ReportCode,
            slice.ReportDefinitionVersion,
            slice.ProjectionGenerationId,
            slice.CompanyId,
            statement.PartyAccountId,
            statement.ControlAccountId,
            MapBalanceSide(statement.BalanceSide),
            slice.Currency.Value,
            slice.EffectiveAsOf,
            slice.DataCutoffAt,
            slice.GeneratedAt,
            dimensions,
            statement.StatementId,
            statement.OpeningExposure,
            statement.ClosingExposure,
            statementLines,
            aging.AgingReportId,
            aging.Policy.PolicyId,
            aging.Policy.Version,
            aging.TotalRemaining,
            buckets,
            items);
    }

    private static string MapStatementKind(PartyStatementEventKind kind) => kind switch
    {
        PartyStatementEventKind.OpenItem => "openItem",
        PartyStatementEventKind.Allocation => "allocation",
        PartyStatementEventKind.Unallocation => "unallocation",
        PartyStatementEventKind.WriteOff => "writeOff",
        PartyStatementEventKind.WriteOffReversal => "writeOffReversal",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string MapBalanceSide(PartyBalanceSide balanceSide) => balanceSide switch
    {
        PartyBalanceSide.Receivable => "receivable",
        PartyBalanceSide.Payable => "payable",
        _ => throw new ArgumentOutOfRangeException(nameof(balanceSide)),
    };
}
