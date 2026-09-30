namespace Pos.Application.Printing.OpenCashDrawer;

/// <summary>
/// Apertura del cajón: <see cref="ForSale"/> al cobrar (sin auditoría) o <see cref="Manual"/> sin
/// venta, con motivo obligatorio que queda en la bitácora.
/// </summary>
public sealed record OpenCashDrawerCommand
{
    public const int ReasonMaxLength = 200;

    private OpenCashDrawerCommand(Guid? saleId, string? reason)
    {
        SaleId = saleId;
        Reason = reason;
    }

    public Guid? SaleId { get; }

    public string? Reason { get; }

    public bool IsManual => SaleId is null;

    public static OpenCashDrawerCommand ForSale(Guid saleId) => new(saleId, null);

    public static OpenCashDrawerCommand Manual(string? reason) => new(null, reason);
}
