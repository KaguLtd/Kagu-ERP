using System.Numerics;

namespace KaguERP.Modules.Inventory.Domain;

internal static class InventoryCostArithmetic
{
    internal static void EnsurePolicy(Guid policy, int scale)
    {
        if (policy == Guid.Empty || scale is < 0 or > 28)
            throw new InventoryInvariantException("INVENTORY_COST_ROUNDING_REQUIRED", "An explicit rounding policy snapshot and scale are required.");
    }

    internal static decimal Divide(decimal amount, decimal quantity, int scale)
    {
        var a = Parts(amount);
        var q = Parts(quantity);
        return Round(a.Coefficient * BigInteger.Pow(10, q.Scale + scale),
            q.Coefficient * BigInteger.Pow(10, a.Scale), scale, "INVENTORY_UNIT_COST_OVERFLOW");
    }

    internal static decimal Multiply(decimal cost, decimal quantity, int scale)
    {
        var c = Parts(cost);
        var q = Parts(quantity);
        return Round(c.Coefficient * q.Coefficient * BigInteger.Pow(10, scale),
            BigInteger.Pow(10, c.Scale + q.Scale), scale, "INVENTORY_VALUE_OVERFLOW");
    }

    private static (BigInteger Coefficient, int Scale) Parts(decimal value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 0m);
        int[] bits = decimal.GetBits(value);
        return (((BigInteger)(uint)bits[2] << 64) + ((BigInteger)(uint)bits[1] << 32) + (uint)bits[0],
            (bits[3] >> 16) & 0xff);
    }

    private static decimal Round(BigInteger numerator, BigInteger denominator, int scale, string overflowCode)
    {
        BigInteger rounded = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        if (remainder * 2 >= denominator) rounded++;
        while (scale > 0 && rounded % 10 == 0) { rounded /= 10; scale--; }
        if (rounded > (BigInteger.One << 96) - 1)
            throw new InventoryInvariantException(overflowCode, "Cost calculation exceeds the exact decimal range.");
        return new decimal(unchecked((int)(uint)(rounded & uint.MaxValue)),
            unchecked((int)(uint)((rounded >> 32) & uint.MaxValue)),
            unchecked((int)(uint)(rounded >> 64)), false, (byte)scale);
    }

    internal static decimal Money(decimal value)
    {
        if (value is < -9999999999999999.9999m or > 9999999999999999.9999m || decimal.Round(value, 4) != value)
            throw new InventoryInvariantException("INVENTORY_VALUE_OVERFLOW", "Inventory value must fit numeric(20,4) exactly.");
        return value;
    }
}
