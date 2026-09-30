namespace Pos.Application.Sales.CancelSale;

/// <summary><c>AuthorizationGrantId</c>: concesión de un administrador cuando el usuario no tiene el permiso (007, FR-013).</summary>
public sealed record CancelSaleCommand(Guid SaleId, int ExpectedVersion, string Reason, Guid? AuthorizationGrantId = null);
