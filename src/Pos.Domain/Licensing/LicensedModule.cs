namespace Pos.Domain.Licensing;

/// <summary>Módulos que se licencian por separado (012).</summary>
public enum LicensedModule
{
    Inventory,
    AdvancedReports,
    CreditAndCustomers,
    CashShifts,
    Returns,

    /// <summary>Descuentos por línea, globales, cupones y reporte de descuentos (015).</summary>
    Discounts,
}
