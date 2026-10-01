using Pos.Domain.Customers;

namespace Pos.Application.Customers.CreateCustomer;

/// <summary>
/// Alta de cliente (014, FR-001). Límite y modalidad solo cuentan con <c>ManageCustomerCredit</c>; sin
/// él el cliente se crea "solo efectivo" con límite 0 (FR-020).
/// </summary>
public sealed record CreateCustomerCommand(
    string Name,
    string Phone,
    string? Email,
    string? TaxId,
    long? CreditLimitCents = null,
    CreditMode? CreditMode = null);
