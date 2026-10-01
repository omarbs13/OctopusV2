namespace Pos.Application.Receivables;

/// <summary>
/// Plazo de pago de las ventas a crédito, en días, para todo el negocio (research §9). Sin archivo o
/// dañado devuelve el valor predeterminado.
/// </summary>
public sealed record ReceivablesSettings
{
    public const int DefaultPaymentTermDays = 30;
    public const int MinPaymentTermDays = 1;
    public const int MaxPaymentTermDays = 3650;

    public int PaymentTermDays { get; init; } = DefaultPaymentTermDays;
}
