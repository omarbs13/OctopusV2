using Pos.Application.Audit;
using Pos.Application.Inventory;
using Pos.Domain.Audit;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

/// <summary>
/// Cambios de campo de una cancelación o devolución (018, research §8): un cambio por producto afectado
/// y uno final de "Importe". Van en <c>Changes</c> y no en <c>Details</c> para no recortarse.
/// </summary>
public static class SaleAuditChanges
{
    public const string Cancelled = "Cancelado";

    public static string EntityName(Sale sale)
    {
        ArgumentNullException.ThrowIfNull(sale);
        return $"Venta {sale.Folio}";
    }

    /// <summary>
    /// Productos afectados con la cantidad de cada uno; <paramref name="cancellation"/> los marca como
    /// "Cancelado" y si no, como "Devuelto: {cantidad}". El importe pasa de <paramref name="amountBeforeCents"/>
    /// a <paramref name="amountAfterCents"/>.
    /// </summary>
    public static IReadOnlyList<AuditFieldChange> Affected(
        Sale sale,
        IEnumerable<(Guid SaleLineId, long QuantityThousandths)> lines,
        bool cancellation,
        long amountBeforeCents,
        long amountAfterCents)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(lines);

        var changes = new List<AuditFieldChange>();
        foreach (var (lineId, quantity) in lines)
        {
            var line = sale.Lines.Single(l => l.Id == lineId);
            var sold = $"{InventoryMessages.Format(line.QuantityThousandths, line.DecimalPlaces)} × {AuditFormat.Money(line.UnitPriceCents)}";
            var after = cancellation ? Cancelled : $"Devuelto: {InventoryMessages.Format(quantity, line.DecimalPlaces)}";
            changes.Add(new AuditFieldChange($"Producto {line.ProductName}", sold, after));
        }

        if (amountBeforeCents != amountAfterCents)
        {
            changes.Add(new AuditFieldChange("Importe", AuditFormat.Money(amountBeforeCents), AuditFormat.Money(amountAfterCents)));
        }

        return changes;
    }
}
