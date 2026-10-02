namespace Pos.Application.Inventory.CheckStockAlerts;

/// <summary>
/// Resultado de una revisión de alertas (022): totales actuales de cada nivel y qué notificaciones mostrar.
/// </summary>
public sealed record StockAlertCheck(int UrgentCount, int AlertCount, bool NotifyUrgent, bool NotifyAlert);
