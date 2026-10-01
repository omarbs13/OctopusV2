namespace Pos.Application.Customers.SearchCustomers;

/// <summary>Busca por nombre, teléfono o RUC (FR-003); sin texto lista todos.</summary>
public sealed record SearchCustomersQuery(string? Text, bool IncludeInactive, int Page = 1);
