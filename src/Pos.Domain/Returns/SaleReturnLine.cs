using Pos.Domain.Common;

namespace Pos.Domain.Returns;

/// <summary>Línea devuelta de una venta, inmutable. Es parte de <see cref="SaleReturn"/>.</summary>
public sealed class SaleReturnLine
{
    private SaleReturnLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid SaleReturnId { get; private set; }

    public Guid SaleLineId { get; private set; }

    public long QuantityThousandths { get; private set; }

    public long AmountCents { get; private set; }

    /// <summary>Movimiento <c>SALE_CANCEL</c>/<c>SALE_RETURN</c>; nulo si no controlaba inventario o sin licencia.</summary>
    public Guid? ReturnMovementId { get; private set; }

    public static SaleReturnLine Create(Guid saleLineId, long quantityThousandths, long amountCents, Guid? returnMovementId)
    {
        if (saleLineId == Guid.Empty || quantityThousandths <= 0 || amountCents < 0)
        {
            throw new DomainException("La línea devuelta no es válida.");
        }

        return new SaleReturnLine
        {
            Id = Guid.CreateVersion7(),
            SaleLineId = saleLineId,
            QuantityThousandths = quantityThousandths,
            AmountCents = amountCents,
            ReturnMovementId = returnMovementId,
        };
    }
}
