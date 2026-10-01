namespace Pos.Domain.Licensing;

/// <summary>
/// Mapeo fijo entre cada módulo y su identificador opaco (012, FR-007). Los valores nunca se muestran
/// ni se configuran; un identificador desconocido se ignora (FR-008).
/// </summary>
public static class ModuleCatalog
{
    private static readonly Guid InventoryId = new("7a99f06e-6c58-43fb-bc34-20bec30f8060");

    private static readonly Guid AdvancedReportsId = new("d6c0dca2-2400-481b-94fc-2d90688eedfc");

    private static readonly Guid CreditAndCustomersId = new("be39a38f-0371-4b15-9bb6-fcb9c1bb1b7a");

    private static readonly Guid CashShiftsId = new("b2145dea-fd36-48bc-85ba-773875b4a298");

    private static readonly Guid ReturnsId = new("2b1ab797-3339-43ef-a135-022f998177ca");

    /// <summary>Descuentos y promociones (015). La herramienta de licencias del proveedor debe conocerlo (docs/descuentos.md).</summary>
    private static readonly Guid DiscountsId = new("01a0f957-082e-72cb-9c5a-9cc8fbee6772");

    private static readonly Dictionary<LicensedModule, Guid> ById = new Dictionary<LicensedModule, Guid>
    {
        [LicensedModule.Inventory] = InventoryId,
        [LicensedModule.AdvancedReports] = AdvancedReportsId,
        [LicensedModule.CreditAndCustomers] = CreditAndCustomersId,
        [LicensedModule.CashShifts] = CashShiftsId,
        [LicensedModule.Returns] = ReturnsId,
        [LicensedModule.Discounts] = DiscountsId,
    };

    public static IReadOnlyList<LicensedModule> All { get; } = Enum.GetValues<LicensedModule>();

    public static Guid IdOf(LicensedModule module) => ById[module];

    public static bool TryGetModule(Guid id, out LicensedModule module)
    {
        foreach (var (key, value) in ById)
        {
            if (value == id)
            {
                module = key;
                return true;
            }
        }

        module = default;
        return false;
    }
}
