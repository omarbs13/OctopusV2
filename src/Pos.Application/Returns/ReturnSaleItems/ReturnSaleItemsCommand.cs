using Pos.Domain.Returns;

namespace Pos.Application.Returns.ReturnSaleItems;

/// <summary><c>AuthorizationGrantId</c>: concesión de <c>ApproveReturns</c> de un Administrador (007).</summary>
public sealed record ReturnSaleItemsCommand(
    Guid SaleId,
    int ExpectedVersion,
    IReadOnlyList<ReturnLineRequest> Lines,
    string Reason,
    ReturnCompensation Compensation,
    Guid? AuthorizationGrantId);
