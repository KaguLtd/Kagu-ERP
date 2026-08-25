using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Accounts;
using KaguERP.Modules.Accounting.Domain.Journals;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public static class PostgresAuthoritativeJournalAccountLoader
{
    public static async ValueTask<ValidatedJournalAccountSet> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        ValidatedJournalDraft draft,
        Guid chartOfAccountsVersionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        scope.EnsureAllowed(draft.TenantId, draft.CompanyId);
        if (chartOfAccountsVersionId == Guid.Empty)
        {
            throw new ArgumentException("Chart-of-accounts version ID cannot be empty.", nameof(chartOfAccountsVersionId));
        }

        const string chartSql = """
            SELECT version
            FROM accounting.chart_of_accounts_version
            WHERE tenant_id = $1 AND company_id = $2 AND chart_version_id = $3
            """;
        await using (var chartCommand = new NpgsqlCommand(chartSql, connection, transaction))
        {
            chartCommand.Parameters.AddWithValue(draft.TenantId);
            chartCommand.Parameters.AddWithValue(draft.CompanyId);
            chartCommand.Parameters.AddWithValue(chartOfAccountsVersionId);
            if (await chartCommand.ExecuteScalarAsync(cancellationToken) is not long)
            {
                throw new AuthoritativeAccountEvidenceException(
                    "ACCOUNT_CHART_VERSION_NOT_FOUND",
                    "The selected chart-of-accounts version is unavailable in the active company scope.");
            }
        }

        Guid[] requiredAccountIds = draft.Lines
            .Select(line => line.AccountId)
            .Distinct()
            .Order()
            .ToArray();
        const string accountSql = """
            SELECT account_id, account_kind, is_active, version
            FROM accounting.account_posting_snapshot
            WHERE tenant_id = $1 AND company_id = $2 AND chart_version_id = $3
              AND account_id = ANY($4)
            ORDER BY account_id
            """;
        var snapshots = new List<AccountPostingSnapshot>(requiredAccountIds.Length);
        await using (var accountCommand = new NpgsqlCommand(accountSql, connection, transaction))
        {
            accountCommand.Parameters.AddWithValue(draft.TenantId);
            accountCommand.Parameters.AddWithValue(draft.CompanyId);
            accountCommand.Parameters.AddWithValue(chartOfAccountsVersionId);
            accountCommand.Parameters.AddWithValue(requiredAccountIds);
            await using NpgsqlDataReader reader = await accountCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                snapshots.Add(AccountPostingSnapshot.Create(
                    draft.TenantId,
                    draft.CompanyId,
                    reader.GetGuid(0),
                    chartOfAccountsVersionId,
                    (AccountKind)reader.GetInt16(1),
                    reader.GetBoolean(2),
                    reader.GetInt64(3)));
            }
        }

        if (snapshots.Count != requiredAccountIds.Length)
        {
            throw new AuthoritativeAccountEvidenceException(
                "ACCOUNT_EVIDENCE_INCOMPLETE",
                "Authoritative posting evidence is missing for one or more journal accounts.");
        }

        return ValidatedJournalAccountSet.Create(draft, chartOfAccountsVersionId, snapshots);
    }
}

public sealed class AuthoritativeAccountEvidenceException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
