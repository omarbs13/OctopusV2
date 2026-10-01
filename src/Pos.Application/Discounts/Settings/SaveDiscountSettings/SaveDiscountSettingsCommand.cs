namespace Pos.Application.Discounts.Settings.SaveDiscountSettings;

/// <summary>Límite de descuento sin autorización, en puntos base (0 a 10 000).</summary>
public sealed record SaveDiscountSettingsCommand(int LimitBasisPoints);
