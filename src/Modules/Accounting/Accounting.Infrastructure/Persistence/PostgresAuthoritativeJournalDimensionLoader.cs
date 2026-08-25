using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Dimensions;
using KaguERP.Modules.Accounting.Domain.Journals;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public static class PostgresAuthoritativeJournalDimensionLoader
{
    public static async ValueTask<ValidatedJournalDimensions> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        ValidatedJournalDraft draft,
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
        const string sql = """
            SELECT requirement_set.version, requirement.dimension_id
            FROM accounting.posting_dimension_requirement_set AS requirement_set
            LEFT JOIN accounting.posting_dimension_requirement AS requirement
              ON requirement.tenant_id = requirement_set.tenant_id
             AND requirement.company_id = requirement_set.company_id
             AND requirement.posting_rule_version_id = requirement_set.posting_rule_version_id
            WHERE requirement_set.tenant_id = $1 AND requirement_set.company_id = $2
              AND requirement_set.posting_rule_version_id = $3
            ORDER BY requirement.dimension_id
            """;
        long? version = null;
        var dimensionIds = new List<Guid>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(draft.TenantId);
            command.Parameters.AddWithValue(draft.CompanyId);
            command.Parameters.AddWithValue(draft.PostingRuleVersionId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                version = reader.GetInt64(0);
                if (!reader.IsDBNull(1))
                {
                    dimensionIds.Add(reader.GetGuid(1));
                }
            }
        }

        if (version is null)
        {
            throw new AuthoritativeDimensionEvidenceException(
                "DIMENSION_REQUIREMENT_SET_NOT_FOUND",
                "The posting-rule dimension requirement set is unavailable in the active company scope.");
        }

        PostingDimensionRequirementSnapshot requirement = PostingDimensionRequirementSnapshot.Create(
            draft.TenantId, draft.CompanyId, draft.PostingRuleVersionId, version.Value, dimensionIds);
        return ValidatedJournalDimensions.Create(draft, requirement);
    }
}

public sealed class AuthoritativeDimensionEvidenceException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
