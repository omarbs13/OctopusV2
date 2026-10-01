using Pos.Application.Printing.Ticket;
using Pos.Domain.Discounts;

namespace Pos.Application.Discounts;

/// <summary>Textos de bitácora y log de los descuentos: nunca incluyen contraseñas (FR-019).</summary>
public static class DiscountTexts
{
    public static string ScopeText(DiscountScope scope) => scope == DiscountScope.Line ? "línea" : "venta";

    /// <summary>"Descuento de línea 15% ($15.00) sobre $99.99".</summary>
    public static string Describe(DiscountScope scope, DiscountValue value, long amountCents, long baseCents) =>
        $"Descuento de {ScopeText(scope)} {value} ({TicketBuilder.FormatMoney(amountCents)}) sobre {TicketBuilder.FormatMoney(baseCents)}";
}
