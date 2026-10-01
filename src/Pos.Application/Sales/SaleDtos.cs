using Pos.Application.Returns;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

public enum LookupKind
{
    ExactMatch,
    NameMatches,
    None,
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

public sealed record ProductLookup(LookupKind Kind, IReadOnlyList<SaleProductDto> Items);

/// <summary>Línea del borrador tal como se guarda: producto, cantidad y precio que vio el operador.</summary>
public sealed record DraftLineDto(Guid ProductId, long QuantityThousandths, long UnitPriceCents);

public sealed record StoredDraft(Guid DraftId, IReadOnlyList<DraftLineDto> Lines);

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
    NotSellableReason? NotSellableReason);

public sealed record RecoveredDraft(Guid DraftId, IReadOnlyList<RecoveredLineDto> Lines);

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
    Guid? CashierId = null);

public sealed record SaleListItemDto(
    Guid Id,
    string Folio,
    DateTime CreatedAtUtc,
    long TotalCents,
    IReadOnlyList<PaymentMethod> Methods,
    SaleStatus Status,
    string CashierName);

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
    long ReturnedThousandths = 0)
{
    /// <summary>Milésimas que aún se pueden devolver de la línea.</summary>
    public long AvailableThousandths => QuantityThousandths - ReturnedThousandths;
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
    bool WithinReturnWindow = true)
{
    /// <summary>Venta vigente con parte de lo vendido devuelto.</summary>
    public bool IsPartiallyReturned => Status == SaleStatus.Completed && ReturnedCents > 0 && ReturnedCents < TotalCents;

    /// <summary>Venta vigente con todo lo vendido devuelto.</summary>
    public bool IsFullyReturned => Status == SaleStatus.Completed && ReturnedCents >= TotalCents;

    public IReadOnlyList<ReturnSummaryDto> ReturnHistory => Returns ?? [];
}

/// <summary>Un día local convertido a UTC: <c>[FromUtc, ToUtcExclusive)</c>.</summary>
public sealed record DayWindow(DateOnly LocalDate, DateTime FromUtc, DateTime ToUtcExclusive);

public sealed record DayTotal(DateOnly LocalDate, long TotalCents, int Count);

public sealed record TopProduct(Guid ProductId, string Name, long QuantityThousandths, int DecimalPlaces);

public sealed record SalesDashboard(IReadOnlyList<DayTotal> Days, IReadOnlyList<TopProduct> TopProducts);
