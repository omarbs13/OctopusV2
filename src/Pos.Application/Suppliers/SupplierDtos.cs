using Pos.Domain.Suppliers;

namespace Pos.Application.Suppliers;

/// <summary>Ficha del proveedor para el formulario de edición.</summary>
public sealed record SupplierDto(
    Guid Id,
    int Version,
    string Name,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    PaymentTerms PaymentTerms,
    int? CreditDays,
    bool IsActive);

/// <summary>Criterios de búsqueda; <c>Text</c> ya viene normalizado (sin acentos y en minúsculas) o nulo.</summary>
public sealed record SupplierSearch(string? Text, bool IncludeInactive, int Page, int PageSize);

public sealed record SupplierListItemDto(
    Guid Id,
    string Name,
    string? TaxId,
    string? Phone,
    PaymentTerms PaymentTerms,
    int? CreditDays,
    bool IsActive,
    int Version);

public sealed record SupplierPage(IReadOnlyList<SupplierListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Proveedor activo que se puede elegir al registrar una compra (FR-007).</summary>
public sealed record SupplierOption(Guid Id, string Name, string? TaxId);

/// <summary>Proveedor del filtro del reporte, incluidos los inactivos (Historia 3, escenario 5).</summary>
public sealed record SupplierFilterOption(Guid Id, string Name, bool IsActive);
