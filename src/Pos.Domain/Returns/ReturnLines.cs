namespace Pos.Domain.Returns;

/// <summary>Línea y cantidad (milésimas) que se quiere devolver.</summary>
public sealed record ReturnLineRequest(Guid SaleLineId, long QuantityThousandths);

/// <summary>Línea a devolver con su monto calculado.</summary>
public sealed record ReturnLineAmount(Guid SaleLineId, long QuantityThousandths, long AmountCents);
