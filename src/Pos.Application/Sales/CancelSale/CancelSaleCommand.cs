namespace Pos.Application.Sales.CancelSale;

public sealed record CancelSaleCommand(Guid SaleId, int ExpectedVersion, string Reason);
