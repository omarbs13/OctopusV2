using Pos.Application.Audit;
using Pos.Domain.Suppliers;

namespace Pos.Application.Suppliers;

/// <summary>Instantánea de auditoría del proveedor (020, research §14): los campos del formulario y el estado.</summary>
public static class SupplierAuditFields
{
    public const string State = "Estado";

    public static IReadOnlyList<AuditField> Snapshot(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        return
        [
            new("Nombre", supplier.Name),
            new("RFC", supplier.TaxId),
            new("Teléfono", supplier.Phone),
            new("Email", supplier.Email),
            new("Dirección", supplier.Address),
            new("Condiciones de pago", DescribeTerms(supplier.PaymentTerms, supplier.CreditDays)),
            new(State, AuditFormat.ActiveState(supplier.IsActive)),
        ];
    }

    /// <summary>"Contado" o "Crédito a {n} días".</summary>
    public static string DescribeTerms(PaymentTerms terms, int? creditDays) =>
        terms == PaymentTerms.Credit ? $"Crédito a {creditDays} días" : "Contado";
}
