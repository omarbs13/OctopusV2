namespace Pos.Application.Discounts;

/// <summary>
/// Configuración de descuentos por instalación (015, research §10): porcentaje máximo, en puntos base, que
/// un Cajero aplica sin autorización. Sin archivo o dañado devuelve el valor predeterminado (10 %).
/// </summary>
public sealed record DiscountSettings
{
    public const int DefaultLimitBasisPoints = 1_000;
    public const int MinLimitBasisPoints = 0;
    public const int MaxLimitBasisPoints = 10_000;

    public int LimitBasisPoints { get; init; } = DefaultLimitBasisPoints;
}
