using Pos.Domain.Customers;

namespace Pos.Application.Customers;

/// <summary>Criterios de búsqueda; <c>Text</c> ya viene normalizado (sin acentos y en minúsculas) o nulo.</summary>
public sealed record CustomerSearch(string? Text, bool IncludeInactive, int Page, int PageSize);

public sealed record CustomerListItemDto(
    Guid Id,
    string Name,
    string Phone,
    string? TaxId,
    CreditMode CreditMode,
    long LimitCents,
    long BalanceCents,
    bool IsActive);

public sealed record CustomerPage(IReadOnlyList<CustomerListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
}

/// <summary>Ficha del cliente: datos, saldo, disponible = máx(0, límite − saldo) y días vencido de la cuenta más antigua.</summary>
public sealed record CustomerDetailDto(
    Guid Id,
    int Version,
    string Name,
    string Phone,
    string? Email,
    string? TaxId,
    CreditMode CreditMode,
    long LimitCents,
    bool IsActive,
    long BalanceCents,
    long AvailableCents,
    int DaysOverdue);

/// <summary>Cliente que se puede elegir para una venta a crédito.</summary>
public sealed record CustomerForSaleDto(Guid Id, string Name, string Phone, string? TaxId);

/// <summary>Estado del crédito para el cobro; la interfaz solo lo muestra (Principio III).</summary>
public sealed record CustomerCreditStatusDto(
    long BalanceCents,
    long LimitCents,
    long AvailableCents,
    long WouldExceedByCents,
    bool HasOverdue);
