namespace Pos.Application.Sales;

/// <summary>Motivo por el que un producto ya no se puede vender.</summary>
public enum NotSellableReason
{
    Inactive,
    Deleted,
}

/// <summary>Revisión de una línea antes de cobrar o al confirmar (research §5).</summary>
public sealed record SaleLineReview(
    Guid ProductId,
    long CurrentPriceCents,
    NotSellableReason? NotSellableReason,
    bool InsufficientStock,
    long? OnHandThousandths);
