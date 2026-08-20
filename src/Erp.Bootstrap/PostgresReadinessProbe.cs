using KaguERP.BuildingBlocks.Application.Observability;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed class PostgresReadinessProbe(NpgsqlDataSource dataSource) : IReadinessProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async ValueTask<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(timeout.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection)
            {
                CommandTimeout = (int)Timeout.TotalSeconds,
            };
            object? result = await command.ExecuteScalarAsync(timeout.Token);
            return result is int value && value == 1
                ? ReadinessResult.Ready
                : ReadinessResult.NotReady;
        }
        catch (Exception exception) when (
            exception is NpgsqlException or TimeoutException or OperationCanceledException)
        {
            return ReadinessResult.NotReady;
        }
    }
}
