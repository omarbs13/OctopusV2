using Pos.Domain.Returns;

namespace Pos.Application.Sales.CancelSale;

/// <summary>
/// <c>Compensation</c>: reintegro o nota de crédito (013). <c>AuthorizationGrantId</c>: concesión de
/// <c>ApproveReturns</c> de un Administrador, siempre exigida con el módulo Devoluciones activo; sin el
/// módulo, concesión de <c>CancelSales</c> cuando el usuario no tiene el permiso (007, FR-013).
/// </summary>
public sealed record CancelSaleCommand(
    Guid SaleId,
    int ExpectedVersion,
    string Reason,
    Guid? AuthorizationGrantId = null,
    ReturnCompensation Compensation = ReturnCompensation.Refund);
