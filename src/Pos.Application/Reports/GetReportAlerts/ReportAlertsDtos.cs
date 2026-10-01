using Pos.Domain.Inventory;

namespace Pos.Application.Reports.GetReportAlerts;

/// <summary>Turno cerrado reciente cuya diferencia supera el umbral.</summary>
public sealed record CashAlert(Guid ShiftId, string FolioText, string CashierName, DateTime OpenedAtUtc, long DifferenceCents, long? DifferenceBasisPoints);

/// <summary>Producto crítico con existencia baja o agotada, con la existencia actual.</summary>
public sealed record CriticalStockAlert(
    Guid ProductId,
    string Name,
    string Sku,
    long OnHandThousandths,
    long? MinimumThousandths,
    int DecimalPlaces,
    string UnitName,
    StockStatus Status);

/// <summary>Alertas de Inicio: hasta 10 de cada tipo más el total de cada uno (Historia 7).</summary>
public sealed record ReportAlerts(
    IReadOnlyList<CashAlert> CashAlerts,
    int CashAlertTotal,
    IReadOnlyList<CriticalStockAlert> CriticalLowStock,
    int CriticalLowStockTotal)
{
    public bool IsEmpty => CashAlertTotal == 0 && CriticalLowStockTotal == 0;
}

/// <summary>Producto crítico activo que controla inventario, con su existencia actual, tal como lo lee Infrastructure.</summary>
public sealed record CriticalProductRow(
    Guid ProductId,
    string Name,
    string Sku,
    long OnHandThousandths,
    long? MinimumThousandths,
    string UnitName,
    int DecimalPlaces);
