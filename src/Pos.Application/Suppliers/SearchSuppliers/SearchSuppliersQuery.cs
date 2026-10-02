namespace Pos.Application.Suppliers.SearchSuppliers;

/// <summary>Busca por nombre o RUC (FR-004); sin texto lista todos.</summary>
public sealed record SearchSuppliersQuery(string? Text, bool IncludeInactive, int Page = 1);
