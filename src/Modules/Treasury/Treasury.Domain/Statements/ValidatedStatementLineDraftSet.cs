using System.Collections.ObjectModel;

namespace KaguERP.Modules.Treasury.Domain.Statements;

public sealed class ValidatedStatementLineDraftSet
{
    private ValidatedStatementLineDraftSet(ReadOnlyCollection<ValidatedStatementLineDraft> lines)
    {
        Lines = lines;
    }

    public IReadOnlyList<ValidatedStatementLineDraft> Lines { get; }

    public static ValidatedStatementLineDraftSet Create(IEnumerable<ValidatedStatementLineDraft?>? lines)
    {
        if (lines is null)
        {
            throw new StatementInvariantException("STATEMENT_LINES_REQUIRED", "Statement-line collection is required.");
        }

        var copiedLines = lines.ToArray();
        if (copiedLines.Length == 0)
        {
            throw new StatementInvariantException("STATEMENT_LINES_REQUIRED", "Statement-line collection is required.");
        }

        if (copiedLines.Any(line => line is null))
        {
            throw new StatementInvariantException("STATEMENT_LINE_REQUIRED", "Statement-line collection cannot contain null values.");
        }

        var validatedLines = copiedLines.Cast<ValidatedStatementLineDraft>().ToArray();
        var lineIds = new HashSet<(Guid TenantId, Guid StatementLineId)>();
        var externalIdentities = new HashSet<StatementLineExternalIdentity>();
        foreach (var line in validatedLines)
        {
            if (!lineIds.Add((line.TenantId, line.StatementLineId)))
            {
                throw new StatementInvariantException(
                    "STATEMENT_LINE_DUPLICATE",
                    "A statement-line ID can occur only once in a tenant.");
            }

            if (!externalIdentities.Add(line.ExternalIdentity))
            {
                throw new StatementInvariantException(
                    "STATEMENT_EXTERNAL_IDENTITY_DUPLICATE",
                    "A canonical external statement-line identity can occur only once.");
            }
        }

        Array.Sort(validatedLines, CompareLines);
        return new ValidatedStatementLineDraftSet(Array.AsReadOnly(validatedLines));
    }

    private static int CompareLines(ValidatedStatementLineDraft left, ValidatedStatementLineDraft right)
    {
        var tenantComparison = left.TenantId.CompareTo(right.TenantId);
        if (tenantComparison != 0)
        {
            return tenantComparison;
        }

        var companyComparison = left.CompanyId.CompareTo(right.CompanyId);
        return companyComparison != 0
            ? companyComparison
            : left.StatementLineId.CompareTo(right.StatementLineId);
    }
}
