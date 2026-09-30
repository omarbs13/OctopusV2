namespace Pos.Application.Sales.GetSalesDashboard;

/// <summary>Los últimos 7 días locales, ya convertidos a UTC; el último es hoy.</summary>
public sealed record SalesDashboardQuery(IReadOnlyList<DayWindow> Days);
