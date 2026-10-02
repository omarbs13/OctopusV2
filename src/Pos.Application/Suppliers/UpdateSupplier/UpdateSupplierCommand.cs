using Pos.Domain.Suppliers;

namespace Pos.Application.Suppliers.UpdateSupplier;

/// <summary>Edición de proveedor con concurrencia optimista (020).</summary>
public sealed record UpdateSupplierCommand(
    Guid Id,
    int ExpectedVersion,
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    PaymentTerms PaymentTerms,
    string? CreditDaysText);
