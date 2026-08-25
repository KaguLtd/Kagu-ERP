using KaguERP.Modules.Accounting.Domain.Journals;

namespace KaguERP.Modules.Accounting.Domain.Dimensions;

public sealed class ValidatedJournalDimensions
{
    private ValidatedJournalDimensions(
        ValidatedJournalDraft journalDraft,
        PostingDimensionRequirementSnapshot requirementSnapshot)
    {
        JournalDraft = journalDraft;
        RequirementSnapshot = requirementSnapshot;
    }

    public ValidatedJournalDraft JournalDraft { get; }
    public PostingDimensionRequirementSnapshot RequirementSnapshot { get; }

    public static ValidatedJournalDimensions Create(
        ValidatedJournalDraft? journalDraft,
        PostingDimensionRequirementSnapshot? requirementSnapshot)
    {
        ArgumentNullException.ThrowIfNull(journalDraft);
        ArgumentNullException.ThrowIfNull(requirementSnapshot);

        if (journalDraft.TenantId != requirementSnapshot.TenantId)
        {
            throw new DimensionInvariantException(
                "JOURNAL_DIMENSION_TENANT_MISMATCH",
                "Journal and dimension requirements must belong to the same tenant.");
        }

        if (journalDraft.CompanyId != requirementSnapshot.CompanyId)
        {
            throw new DimensionInvariantException(
                "JOURNAL_DIMENSION_COMPANY_MISMATCH",
                "Journal and dimension requirements must belong to the same company.");
        }

        if (journalDraft.PostingRuleVersionId != requirementSnapshot.PostingRuleVersionId)
        {
            throw new DimensionInvariantException(
                "JOURNAL_DIMENSION_RULE_VERSION_MISMATCH",
                "Journal and dimension requirements must use the same posting-rule version.");
        }

        for (var lineIndex = 0; lineIndex < journalDraft.Lines.Count; lineIndex++)
        {
            var line = journalDraft.Lines[lineIndex];
            var assignedDimensionIds = line.Dimensions.Select(assignment => assignment.DimensionId).ToHashSet();
            foreach (var requiredDimensionId in requirementSnapshot.RequiredDimensionIds)
            {
                if (!assignedDimensionIds.Contains(requiredDimensionId))
                {
                    throw new DimensionInvariantException(
                        "JOURNAL_DIMENSION_REQUIRED",
                        $"Journal line {lineIndex} is missing a required dimension assignment.");
                }
            }
        }

        return new ValidatedJournalDimensions(journalDraft, requirementSnapshot);
    }
}
