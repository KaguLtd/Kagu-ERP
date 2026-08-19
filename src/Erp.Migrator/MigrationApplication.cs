namespace KaguERP.Migrator;

internal static class MigrationApplication
{
    private const string ConnectionStringVariable = "KAGU_ERP_MIGRATOR_CONNECTION_STRING";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 0)
        {
            Console.Error.WriteLine("The migrator accepts no command-line arguments. Supply the connection string through the documented environment variable.");
            return 2;
        }

        string? connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine($"Required environment variable {ConnectionStringVariable} is missing.");
            return 2;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            var runner = new MigrationRunner(connectionString);
            int appliedCount = await runner.RunAsync(cancellation.Token);
            Console.WriteLine($"Migration check completed; {appliedCount} migration(s) applied.");
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("Migration cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Migration failed ({exception.GetType().Name}): {exception.Message}");
            return 1;
        }
    }
}
