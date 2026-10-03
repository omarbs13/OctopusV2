namespace Pos.Domain.Licensing;

/// <summary>
/// Catálogo de los 9 módulos (025, FR-001 a FR-006). Debe coincidir exactamente con
/// <c>contracts/module-catalog.json</c>; una prueba de consistencia lo verifica en cada compilación.
/// Los identificadores son permanentes: los 6 de 012/015 no cambian (FR-003) y un identificador
/// desconocido se ignora (FR-022).
/// </summary>
public static class ModuleCatalog
{
    private static readonly Entry[] Entries =
    [
        new(LicensedModule.Pos, new Guid("4c2517f5-096a-460e-8ee7-b09e46572952"), "pos"),
        new(LicensedModule.Inventory, new Guid("7a99f06e-6c58-43fb-bc34-20bec30f8060"), "inventory"),
        new(LicensedModule.AdvancedReports, new Guid("d6c0dca2-2400-481b-94fc-2d90688eedfc"), "advanced_reports"),
        new(LicensedModule.CreditAndCustomers, new Guid("be39a38f-0371-4b15-9bb6-fcb9c1bb1b7a"), "credit_customers"),
        new(LicensedModule.CashShifts, new Guid("b2145dea-fd36-48bc-85ba-773875b4a298"), "cash_shifts"),
        new(LicensedModule.Returns, new Guid("2b1ab797-3339-43ef-a135-022f998177ca"), "returns"),
        new(LicensedModule.Suppliers, new Guid("1e6111ca-514b-497e-b553-154960c946f3"), "suppliers"),
        new(LicensedModule.Discounts, new Guid("01a0f957-082e-72cb-9c5a-9cc8fbee6772"), "discounts"),
        new(LicensedModule.Categories, new Guid("7b4ae9e2-6e3f-4006-b259-b7d4dcd06933"), "categories"),
    ];

    /// <summary>Los 9 módulos en el orden del catálogo.</summary>
    public static IReadOnlyList<LicensedModule> All { get; } = Array.ConvertAll(Entries, e => e.Module);

    /// <summary>Módulo base (<c>isBase</c> en el contrato).</summary>
    public static LicensedModule Base => LicensedModule.Pos;

    public static Guid IdOf(LicensedModule module) => Find(module).Id;

    public static string KeyOf(LicensedModule module) => Find(module).Key;

    /// <summary>Orden del catálogo, empezando en 1.</summary>
    public static int OrderOf(LicensedModule module) => Array.FindIndex(Entries, e => e.Module == module) + 1;

    public static bool TryGetModule(Guid id, out LicensedModule module)
    {
        foreach (var entry in Entries)
        {
            if (entry.Id == id)
            {
                module = entry.Module;
                return true;
            }
        }

        module = default;
        return false;
    }

    private static Entry Find(LicensedModule module) =>
        Array.Find(Entries, e => e.Module == module) ?? throw new ArgumentOutOfRangeException(nameof(module), module, null);

    private sealed record Entry(LicensedModule Module, Guid Id, string Key);
}
