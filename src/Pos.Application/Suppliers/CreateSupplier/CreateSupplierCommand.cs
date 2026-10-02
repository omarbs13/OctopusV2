using Pos.Domain.Suppliers;

namespace Pos.Application.Suppliers.CreateSupplier;

/// <summary>Alta de proveedor (020, FR-001–FR-003). <c>CreditDaysText</c> solo cuenta con crédito.</summary>
public sealed record CreateSupplierCommand(
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    PaymentTerms PaymentTerms,
    string? CreditDaysText);
