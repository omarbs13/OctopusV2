using Pos.Application.Discounts;
using Pos.Application.Returns;
using Pos.Domain.Discounts;
using Pos.Domain.Receivables;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

public enum LookupKind
{
    ExactMatch,
    NameMatches,
    None,

    /// <summary>El código no es de un producto pero sí de un cupón (015, FR-011); viene en <see cref="ProductLookup.Coupon"/>.</summary>
    Coupon,
}

/// <summary>Producto para vender, con precio, unidad y existencia (research §8).</summary>
public sealed record SaleProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    long PriceCents,
    string UnitCode,
    string UnitName,
    int DecimalPlaces,
    bool TracksInventory,
    long? OnHandThousandths,
    NotSellableReason? NotSellableReason);

public sealed record ProductLookup(LookupKind Kind, IReadOnlyList<SaleProductDto> Items, CouponLookupDto? Coupon = null);

/// <summary>Descuento de una línea en el borrador (015, research §11): valor capturado y aprobación, si la hubo.</summary>
public sealed record DraftDiscountDto(DiscountMode Mode, long Value, Guid? ApprovalId = null);

/// <summary>
/// Descuento de venta en el borrador (015): <see cref="Kind"/> es <c>MANUAL</c> (modalidad, valor y
/// aprobación) o <c>COUPON</c> (código, que se revalida al cobrar).
/// </summary>
public sealed record DraftOrderDiscountDto(string Kind, DiscountMode? Mode = null, long? Value = null, Guid? ApprovalId = null, string? Code = null)
{
    public const string ManualKind = "MANUAL";
    public const string CouponKind = "COUPON";

    public bool IsCoupon => Kind == CouponKind;

    public static DraftOrderDiscountDto Manual(DiscountMode mode, long value, Guid? approvalId) => new(ManualKind, mode, value, approvalId);

    public static DraftOrderDiscountDto ForCoupon(string code) => new(CouponKind, Code: code);
}

/// <summary>Línea del borrador tal como se guarda: producto, cantidad, precio que vio el operador y su descuento (015).</summary>
public sealed record DraftLineDto(Guid ProductId, long QuantityThousandths, long UnitPriceCents, DraftDiscountDto? Discount = null);

public sealed record StoredDraft(Guid DraftId, IReadOnlyList<DraftLineDto> Lines, DraftOrderDiscountDto? OrderDiscount = null);

/// <summary>Línea recuperada del borrador, con los datos vigentes del producto.</summary>
public sealed record RecoveredLineDto(
    Guid ProductId,
    string Name,
    string Sku,
    string UnitCode,
    int DecimalPlaces,
    bool TracksInventory,
    long CurrentPriceCents,
    long QuantityThousandths,
    NotSellableReason? NotSellableReason,
    DraftDiscountDto? Discount = null);

/// <summary><c>DiscountsDropped</c>: el módulo Descuentos no está activo y se quitaron los descuentos (015, casos límite).</summary>
public sealed record RecoveredDraft(
    Guid DraftId,
    IReadOnlyList<RecoveredLineDto> Lines,
    DraftOrderDiscountDto? OrderDiscount = null,
    bool DiscountsDropped = false);

public sealed record SaleReview(IReadOnlyList<SaleLineReview> Lines);

public sealed record ConfirmedSale(Guid SaleId, string Folio, long TotalCents, long ChangeCents);

/// <summary>Criterios de "Ventas realizadas" ya normalizados; el límite superior de fecha es exclusivo.</summary>
public sealed record SaleSearch(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    long? FolioNumber,
    SaleStatus? Status,
    int Page,
    int PageSize,
    Guid? CashierId = null,
    Guid? CustomerId = null);

/// <summary><c>CreditStatus</c> solo en las ventas a crédito (014): estado de su cuenta por cobrar.</summary>
public sealed record SaleListItemDto(
    Guid Id,
    string Folio,
    DateTime CreatedAtUtc,
    long TotalCents,
    IReadOnlyList<PaymentMethod> Methods,
    SaleStatus Status,
    string CashierName,
    ReceivableStatus? CreditStatus = null);

public sealed record SalePage(IReadOnlyList<SaleListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

public sealed record SaleLineDto(
    int Position,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    string UnitCode,
    int DecimalPlaces,
    long UnitPriceCents,
    long QuantityThousandths,
    long AmountCents,
    Guid Id = default,
    long ReturnedThousandths = 0,
    long? OriginalAmountCents = null,
    long LineDiscountCents = 0)
{
    /// <summary>Milésimas que aún se pueden devolver de la línea.</summary>
    public long AvailableThousandths => QuantityThousandths - ReturnedThousandths;

    /// <summary>Cantidad × precio (015); en las líneas sin descuento es el importe.</summary>
    public long OriginalCents => OriginalAmountCents ?? AmountCents;

    /// <summary>Importe final de la línea que se muestra: original − descuento de línea (no incluye la parte del descuento de venta).</summary>
    public long AmountAfterLineDiscountCents => OriginalCents - LineDiscountCents;

    public bool HasLineDiscount => LineDiscountCents > 0;
}

/// <summary>Descuento de una venta registrada para "Consultar ventas" (015, FR-017), con los nombres de quién aplicó y autorizó.</summary>
public sealed record SaleDiscountDto(
    DiscountKind Kind,
    DiscountMode Mode,
    long Value,
    long AmountCents,
    Guid? SaleLineId,
    string? ProductName,
    string? CouponCode,
    string AppliedByName,
    string? AuthorizedByName)
{
    public DiscountValue Discount => DiscountValue.Create(Mode, Value);
}

public sealed record SalePaymentDto(
    PaymentMethod Method,
    long AmountCents,
    long? ReceivedCents,
    long? ChangeCents,
    string? Reference);

public sealed record SaleDetailDto(
    Guid Id,
    string Folio,
    DateTime CreatedAtUtc,
    string CreatedByName,
    long TotalCents,
    SaleStatus Status,
    int Version,
    string? CancellationReason,
    DateTime? CancelledAtUtc,
    string? CancelledByName,
    IReadOnlyList<SaleLineDto> Lines,
    IReadOnlyList<SalePaymentDto> Payments,
    Guid CreatedById,
    long ReturnedCents = 0,
    IReadOnlyList<ReturnSummaryDto>? Returns = null,
    bool WithinReturnWindow = true,
    CreditInfo? Credit = null,
    long DiscountCents = 0,
    IReadOnlyList<SaleDiscountDto>? Discounts = null)
{
    /// <summary>Σ de los importes de las líneas después de su descuento (015): el SUBTOTAL del ticket.</summary>
    public long SubtotalCents => Lines.Sum(l => l.AmountAfterLineDiscountCents);

    /// <summary>Descuentos de la venta (015); vacío en las ventas sin descuentos.</summary>
    public IReadOnlyList<SaleDiscountDto> DiscountList => Discounts ?? [];

    /// <summary>Descuento global manual o de cupón, si la venta lo tuvo.</summary>
    public SaleDiscountDto? OrderDiscount => DiscountList.FirstOrDefault(d => d.Kind != DiscountKind.Line);

    public bool HasDiscounts => DiscountCents > 0;

    /// <summary>Venta a crédito (014): se paga con un único pago <c>ACCOUNT</c>.</summary>
    public bool IsOnAccount => Credit is not null;

    /// <summary>Venta vigente con parte de lo vendido devuelto.</summary>
    public bool IsPartiallyReturned => Status == SaleStatus.Completed && ReturnedCents > 0 && ReturnedCents < TotalCents;

    /// <summary>Venta vigente con todo lo vendido devuelto.</summary>
    public bool IsFullyReturned => Status == SaleStatus.Completed && ReturnedCents >= TotalCents;

    public IReadOnlyList<ReturnSummaryDto> ReturnHistory => Returns ?? [];
}

/// <summary>Cuenta por cobrar de una venta a crédito (014): cliente, saldo de esa venta y estado.</summary>
public sealed record CreditInfo(Guid CustomerId, string CustomerName, long BalanceCents, ReceivableStatus Status);

/// <summary>Un día local convertido a UTC: <c>[FromUtc, ToUtcExclusive)</c>.</summary>
public sealed record DayWindow(DateOnly LocalDate, DateTime FromUtc, DateTime ToUtcExclusive);

public sealed record DayTotal(DateOnly LocalDate, long TotalCents, int Count);

public sealed record TopProduct(Guid ProductId, string Name, long QuantityThousandths, int DecimalPlaces);

public sealed record SalesDashboard(IReadOnlyList<DayTotal> Days, IReadOnlyList<TopProduct> TopProducts);
