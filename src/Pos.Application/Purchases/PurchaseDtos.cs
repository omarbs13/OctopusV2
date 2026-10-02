using Pos.Application.Abstractions;
using Pos.Domain.Purchases;

namespace Pos.Application.Purchases;

/// <summary>Línea capturada de una compra: producto y textos de cantidad y costo.</summary>
public sealed record PurchaseLineInput(Guid ProductId, string QuantityText, string UnitCostText);

/// <summary>Importe calculado de una línea; nulo si la captura tiene errores.</summary>
public sealed record PurchaseLineTotalsDto(long? AmountCents, bool IsBonus, IReadOnlyList<FieldError> Errors);

/// <summary>Totales en vivo de la captura (research §4); la interfaz solo los muestra (Principio III).</summary>
public sealed record PurchaseTotalsDto(
    IReadOnlyList<PurchaseLineTotalsDto> Lines,
    long SubtotalCents,
    long TaxCents,
    long TotalCents,
    IReadOnlyList<FieldError> Errors);

public sealed record PurchaseRegisteredDto(Guid PurchaseId, long SubtotalCents, long TaxCents, long TotalCents);

/// <summary>Línea del detalle con los datos congelados al registrar (FR-016, FR-022).</summary>
public sealed record PurchaseLineDetailDto(
    int LineNumber,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    long QuantityThousandths,
    string UnitCode,
    string UnitName,
    int DecimalPlaces,
    long UnitCostCents,
    long AmountCents,
    bool IsBonus);

/// <summary>Detalle de una compra: encabezado, importes, estado, anulación y líneas (FR-022).</summary>
public sealed record PurchaseDetailDto(
    Guid Id,
    int Version,
    Guid SupplierId,
    string SupplierName,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateTime RegisteredAtUtc,
    string RegisteredByName,
    long SubtotalCents,
    long TaxCents,
    long TotalCents,
    PurchaseStatus Status,
    DateTime? VoidedAtUtc,
    string? VoidedByName,
    string? VoidReason,
    IReadOnlyList<PurchaseLineDetailDto> Lines)
{
    public bool IsVoided => Status == PurchaseStatus.Voided;
}
