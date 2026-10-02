using System.Globalization;

namespace Pos.Application.Purchases;

/// <summary>Nombres de campo de los errores de compras; los de línea llevan el índice (escenario 7).</summary>
public static class PurchaseFields
{
    public const string SupplierId = "SupplierId";
    public const string InvoiceNumber = "InvoiceNumber";
    public const string InvoiceDate = "InvoiceDate";
    public const string Tax = "Tax";
    public const string Lines = "Lines";
    public const string Subtotal = "Subtotal";
    public const string Reason = "Reason";

    public static string LineProduct(int index) => Line(index, "Product");

    public static string LineQuantity(int index) => Line(index, "Quantity");

    public static string LineUnitCost(int index) => Line(index, "UnitCost");

    private static string Line(int index, string field) => string.Create(CultureInfo.InvariantCulture, $"Lines[{index}].{field}");
}
