using Pos.Domain.Sales;

namespace Pos.Application.Sales.ReviewSale;

public sealed record ReviewLineInput(Guid ProductId, long QuantityThousandths, SaleMode? Mode = null);

/// <summary>Revisión previa al cobro: precios vigentes, líneas no vendibles y existencia insuficiente.</summary>
public sealed record ReviewSaleQuery(IReadOnlyList<ReviewLineInput> Lines);
