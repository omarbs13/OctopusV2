using Pos.Domain.Customers;

namespace Pos.Application.Customers.UpdateCustomer;

/// <summary>
/// Edición de cliente. <c>CreditLimitCents</c> y <c>CreditMode</c> nulos conservan los actuales; cambiarlos
/// exige <c>ManageCustomerCredit</c> (FR-020).
/// </summary>
public sealed record UpdateCustomerCommand(
    Guid Id,
    int ExpectedVersion,
    string Name,
    string Phone,
    string? Email,
    string? TaxId,
    long? CreditLimitCents = null,
    CreditMode? CreditMode = null);
