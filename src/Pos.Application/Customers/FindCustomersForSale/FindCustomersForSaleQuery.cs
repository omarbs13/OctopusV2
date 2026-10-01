namespace Pos.Application.Customers.FindCustomersForSale;

/// <summary>Buscador del punto de venta: nombre, teléfono o RUC; sin texto lista los primeros.</summary>
public sealed record FindCustomersForSaleQuery(string? Text);
