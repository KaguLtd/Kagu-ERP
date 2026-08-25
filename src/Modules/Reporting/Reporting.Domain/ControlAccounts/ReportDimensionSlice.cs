using System.Collections.ObjectModel;

namespace KaguERP.Modules.Reporting.Domain.ControlAccounts;

public sealed class ReportDimensionSlice
{
    private ReportDimensionSlice(ReadOnlyCollection<ReportDimensionAssignment> assignments)
    {
        Assignments = assignments;
    }

    public IReadOnlyList<ReportDimensionAssignment> Assignments { get; }

    public static ReportDimensionSlice Create(IEnumerable<ReportDimensionAssignment?>? assignments)
    {
        if (assignments is null)
        {
            throw new ReportingInvariantException(
                "REPORT_DIMENSIONS_REQUIRED",
                "Report dimension selection is required; use an empty collection for an unsegmented total.");
        }

        var copiedAssignments = assignments.ToArray();
        if (copiedAssignments.Any(assignment => assignment is null))
        {
            throw new ReportingInvariantException(
                "REPORT_DIMENSION_REQUIRED",
                "Report dimension selection cannot contain null values.");
        }

        var validatedAssignments = copiedAssignments.Cast<ReportDimensionAssignment>().ToArray();
        var dimensionCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in validatedAssignments)
        {
            if (!dimensionCodes.Add(assignment.DimensionCode))
            {
                throw new ReportingInvariantException(
                    "REPORT_DIMENSION_DUPLICATE",
                    "A report slice can select at most one value for each dimension code.");
            }
        }

        Array.Sort(
            validatedAssignments,
            (left, right) => string.Compare(left.DimensionCode, right.DimensionCode, StringComparison.Ordinal));
        return new ReportDimensionSlice(Array.AsReadOnly(validatedAssignments));
    }

    public bool HasSameSelection(ReportDimensionSlice? other) =>
        other is not null && Assignments.SequenceEqual(other.Assignments);
}
