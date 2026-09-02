using KaguERP.Bootstrap;
using KaguERP.Modules.Reporting.Application.PartyReports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal sealed partial class PartyReportRefreshBackgroundService(
    IServiceProvider serviceProvider,
    PartyReportRefreshWorkerRuntimeOptions options,
    ILogger<PartyReportRefreshBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IPartyReportRefreshCycle? cycle = serviceProvider.GetService<IPartyReportRefreshCycle>();
        if (!options.Enabled || cycle is null)
        {
            WorkerDisabled(logger);
            return;
        }

        WorkerStarted(logger, options.PollInterval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                PartyReportRefreshCycleResult result = await cycle.ProcessNextAsync(stoppingToken);
                if (result.Disposition == PartyReportRefreshCycleDisposition.Idle)
                {
                    await Task.Delay(options.PollInterval, stoppingToken);
                    continue;
                }
                WorkProcessed(
                    logger,
                    result.WorkItemId,
                    result.AttemptNumber,
                    result.Disposition,
                    result.ErrorCode);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (PartyReportWorkerIdentityException exception)
            {
                IdentityDenied(logger, exception.Code);
                await Task.Delay(options.PollInterval, stoppingToken);
            }
            catch (Exception exception)
            {
                CycleFailed(logger, exception);
                await Task.Delay(options.PollInterval, stoppingToken);
            }
        }
    }

    [LoggerMessage(LogLevel.Information, "Party report refresh Worker is disabled because no service identity scope is configured.")]
    private static partial void WorkerDisabled(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Party report refresh Worker started with a {PollSeconds}-second poll interval.")]
    private static partial void WorkerStarted(ILogger logger, double pollSeconds);

    [LoggerMessage(
        LogLevel.Information,
        "Party report refresh work {WorkItemId} attempt {AttemptNumber} ended with {Disposition}; error code {ErrorCode}.")]
    private static partial void WorkProcessed(
        ILogger logger,
        Guid? workItemId,
        int? attemptNumber,
        PartyReportRefreshCycleDisposition disposition,
        string? errorCode);

    [LoggerMessage(LogLevel.Error, "Party report refresh service identity was denied with {ErrorCode}.")]
    private static partial void IdentityDenied(ILogger logger, string errorCode);

    [LoggerMessage(LogLevel.Error, "Party report refresh cycle failed before a safe work result was recorded.")]
    private static partial void CycleFailed(ILogger logger, Exception exception);
}
