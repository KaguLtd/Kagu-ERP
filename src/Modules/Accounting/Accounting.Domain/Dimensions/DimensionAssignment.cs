namespace KaguERP.Modules.Accounting.Domain.Dimensions;

public sealed record DimensionAssignment
{
    private DimensionAssignment(Guid dimensionId, Guid dimensionValueId)
    {
        DimensionId = dimensionId;
        DimensionValueId = dimensionValueId;
    }

    public Guid DimensionId { get; }
    public Guid DimensionValueId { get; }

    public static DimensionAssignment Create(Guid dimensionId, Guid dimensionValueId)
    {
        if (dimensionId == Guid.Empty)
        {
            throw new DimensionInvariantException("DIMENSION_ID_REQUIRED", "Dimension ID is required.");
        }

        if (dimensionValueId == Guid.Empty)
        {
            throw new DimensionInvariantException(
                "DIMENSION_VALUE_ID_REQUIRED",
                "Dimension value ID is required.");
        }

        return new DimensionAssignment(dimensionId, dimensionValueId);
    }
}
