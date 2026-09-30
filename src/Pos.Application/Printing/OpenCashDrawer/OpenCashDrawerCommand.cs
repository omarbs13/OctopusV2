namespace Pos.Application.Printing.OpenCashDrawer;

/// <summary>
/// Apertura del cajón: <see cref="ForSale"/> al cobrar (sin auditoría) o <see cref="Manual"/> sin
/// venta, con motivo obligatorio que queda en la bitácora.
/// </summary>
public sealed record OpenCashDrawerCommand
{
    public const int ReasonMaxLength = 200;

    private OpenCashDrawerCommand(Guid? saleId, string? reason, Guid? authorizationGrantId)
    {
        SaleId = saleId;
        Reason = reason;
        AuthorizationGrantId = authorizationGrantId;
    }

    public Guid? SaleId { get; }

    public string? Reason { get; }

    /// <summary>Concesión de un administrador cuando el usuario no puede abrir el cajón sin venta (007, FR-013).</summary>
    public Guid? AuthorizationGrantId { get; }

    public bool IsManual => SaleId is null;

    public static OpenCashDrawerCommand ForSale(Guid saleId) => new(saleId, null, null);

    public static OpenCashDrawerCommand Manual(string? reason, Guid? authorizationGrantId = null) =>
        new(null, reason, authorizationGrantId);
}
