using System.Globalization;
using Pos.Desktop.Resources;
using Pos.Domain.Suppliers;

namespace Pos.Desktop.Purchases;

/// <summary>Textos comunes de las pantallas de proveedores.</summary>
public static class SupplierTexts
{
    /// <summary>"Contado" o "Crédito 30 días".</summary>
    public static string Terms(PaymentTerms terms, int? creditDays) =>
        terms == PaymentTerms.Credit
            ? string.Format(CultureInfo.CurrentCulture, Strings.Supplier_TermsCreditDays, creditDays)
            : Strings.Supplier_TermsCash;
}
