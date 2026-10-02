namespace Pos.Domain.Inventory;

/// <summary>Decide si un producto debe notificarse hoy a un usuario (022, research §5).</summary>
public static class StockAlertDedup
{
    /// <summary>
    /// Urgente es pendiente si hoy no se registró urgente para el producto (el escalamiento desde
    /// alerta sí notifica); alerta es pendiente si hoy no se registró ningún nivel (el descenso desde
    /// urgente no notifica); sin alerta nunca.
    /// </summary>
    public static bool IsPending(StockAlertLevel level, IReadOnlySet<StockAlertLevel> acknowledgedToday)
    {
        ArgumentNullException.ThrowIfNull(acknowledgedToday);
        return level switch
        {
            StockAlertLevel.Urgent => !acknowledgedToday.Contains(StockAlertLevel.Urgent),
            StockAlertLevel.Alert => acknowledgedToday.Count == 0,
            _ => false,
        };
    }
}
