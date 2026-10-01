using Pos.Domain.Discounts;

namespace Pos.Application.Discounts.ApproveDiscount;

/// <summary>
/// Aprobación de un descuento que supera el límite, al aplicarlo (015, research §7). <c>BaseCents</c> es el
/// importe de la línea o el subtotal que vio el operador. <c>GrantId</c> es la concesión de
/// <c>ApproveDiscounts</c> que obtuvo un Cajero; nulo si quien opera es Administrador.
/// </summary>
public sealed record ApproveDiscountCommand(
    Guid DraftId,
    DiscountScope Scope,
    Guid? ProductId,
    DiscountMode Mode,
    long Value,
    long BaseCents,
    Guid? GrantId);
