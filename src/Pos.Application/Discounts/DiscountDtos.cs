using Pos.Domain.Discounts;
using Pos.Domain.Reports;

namespace Pos.Application.Discounts;

/// <summary>Aprobación guardada por <c>ApproveDiscount</c>; el <c>Cart</c> y el borrador conservan <see cref="ApprovalId"/>.</summary>
public sealed record DiscountApprovalDto(Guid ApprovalId, Guid AuthorizedBy, string AuthorizedByName);

/// <summary>Cupón encontrado por su código, con su estado de hoy; las fechas sirven para los mensajes.</summary>
public sealed record CouponLookupDto(
    Guid CouponId,
    string Code,
    DiscountMode Mode,
    long Value,
    CouponStatus Status,
    DateOnly StartsOn,
    DateOnly EndsOn)
{
    public DiscountValue Discount => DiscountValue.Create(Mode, Value);
}

public sealed record CouponSearch(string? Text, CouponStatus? Status, int Page, int PageSize);

public sealed record CouponListItemDto(
    Guid Id,
    string Code,
    DiscountMode Mode,
    long Value,
    DateOnly StartsOn,
    DateOnly EndsOn,
    CouponStatus Status,
    int UsesCount,
    int? UsageLimit,
    bool IsActive,
    int Version)
{
    /// <summary>Usos restantes; nulo si no hay límite.</summary>
    public int? RemainingUses => UsageLimit is { } limit ? Math.Max(0, limit - UsesCount) : null;

    public DiscountValue Discount => DiscountValue.Create(Mode, Value);
}

public sealed record CouponPage(IReadOnlyList<CouponListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

public sealed record CouponDetailDto(
    Guid Id,
    string Code,
    DiscountMode Mode,
    long Value,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int? UsageLimit,
    int UsesCount,
    bool IsActive,
    CouponStatus Status,
    int Version)
{
    /// <summary>Con usos registrados, código, modalidad y valor quedan en solo lectura (FR-010).</summary>
    public bool HasUses => UsesCount > 0;
}

/// <summary>Filtro del reporte de descuentos (FR-018).</summary>
public sealed record DiscountReportQuery(ReportPeriod Period, Guid? CashierId, DiscountKind? Kind, int Page = 1, int PageSize = 100);

public sealed record DiscountReport(long TotalDiscountCents, int Count, IReadOnlyList<DiscountReportRow> Rows, long TotalRows, int Page, int PageSize)
{
    public int TotalPages => TotalRows <= 0 ? 1 : (int)((TotalRows + PageSize - 1) / PageSize);
}

public sealed record DiscountReportRow(
    Guid SaleId,
    string Folio,
    DateTime CreatedAtUtc,
    string CashierName,
    DiscountKind Kind,
    DiscountMode Mode,
    long Value,
    long AmountCents,
    string? AuthorizedByName,
    string? CouponCode);
