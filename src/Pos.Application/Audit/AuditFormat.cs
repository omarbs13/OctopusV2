using Pos.Application.Inventory;
using Pos.Application.Printing.Ticket;
using Pos.Domain.Common;

namespace Pos.Application.Audit;

/// <summary>Formato común de los valores de las instantáneas de auditoría (018, research §2).</summary>
public static class AuditFormat
{
    public const string NoCategory = "Sin categoría";

    public static string Money(Money money) => TicketBuilder.FormatMoney(money.Cents);

    public static string Money(long cents) => TicketBuilder.FormatMoney(cents);

    public static string YesNo(bool value) => value ? "Sí" : "No";

    public static string ActiveState(bool isActive) => isActive ? "Activo" : "Inactivo";

    /// <summary>Cantidad con los decimales de su unidad; nulo si no hay cantidad.</summary>
    public static string? Quantity(Quantity? quantity, int decimalPlaces) =>
        quantity is { } value ? InventoryMessages.Format(value.Thousandths, decimalPlaces) : null;

    public static string Category(string? categoryName) =>
        string.IsNullOrWhiteSpace(categoryName) ? NoCategory : categoryName;
}
