namespace AquaTrack.Domain.Common;

/// <summary>Shared precision rules so every monetary/quantity column is declared the same way.</summary>
public static class Precision
{
    public const int MoneyPrecision = 18;
    public const int MoneyScale = 2;
    public const int QuantityPrecision = 18;
    public const int QuantityScale = 3;
}
