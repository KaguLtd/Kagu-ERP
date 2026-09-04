namespace KaguERP.Modules.Sales.Domain.Orders;

public readonly record struct SalesOrderQuantity
{
    private const decimal MaximumMagnitude = 99999999999999.999999m;

    private SalesOrderQuantity(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }

    public static SalesOrderQuantity Create(decimal value)
    {
        if (value <= decimal.Zero || value > MaximumMagnitude || decimal.Round(value, 6) != value)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_QUANTITY_INVALID",
                "Sales order quantity must be positive and fit numeric(20,6) without rounding.");
        }

        return new SalesOrderQuantity(value);
    }
}
