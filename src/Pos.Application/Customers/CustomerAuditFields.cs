using Pos.Application.Audit;
using Pos.Domain.Customers;

namespace Pos.Application.Customers;

/// <summary>
/// Instantánea de auditoría del cliente (018, research §6): los campos editables del formulario,
/// incluidos la modalidad y el límite de crédito.
/// </summary>
public static class CustomerAuditFields
{
    public const string State = "Estado";
    public const string CreditMode = "Modalidad de crédito";
    public const string CreditLimit = "Límite de crédito";

    public static IReadOnlyList<AuditField> Snapshot(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return
        [
            new("Nombre", customer.Name),
            new("Teléfono", customer.Phone),
            new("Email", customer.Email),
            new("RFC", customer.TaxId),
            new(CreditMode, customer.CreditMode == Domain.Customers.CreditMode.Credit ? "Crédito disponible" : "Solo efectivo"),
            new(CreditLimit, AuditFormat.Money(customer.CreditLimitCents)),
            new(State, AuditFormat.ActiveState(customer.IsActive)),
        ];
    }
}
