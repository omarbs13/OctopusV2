namespace Pos.Domain.Licensing;

/// <summary>
/// Módulos del catálogo compartido con OctopusAdmin (025, contracts/module-catalog.json). El valor
/// numérico no se persiste en ningún lado; la identidad es el GUID de <see cref="ModuleCatalog"/>.
/// </summary>
public enum LicensedModule
{
    /// <summary>Módulo base: sin él el sistema se bloquea (025, FR-027).</summary>
    Pos,

    Inventory,
    AdvancedReports,
    CreditAndCustomers,
    CashShifts,
    Returns,

    /// <summary>Proveedores y compras a proveedores (025, FR-007).</summary>
    Suppliers,

    /// <summary>Descuentos por línea, globales, cupones y reporte de descuentos (015).</summary>
    Discounts,

    /// <summary>Categorías de productos (025, FR-007).</summary>
    Categories,
}
