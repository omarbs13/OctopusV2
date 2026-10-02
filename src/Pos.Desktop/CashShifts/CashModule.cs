using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.CashShifts;

public static class CashModule
{
    public const string GroupId = "cash";
    public const string ReadoutPageId = "cash.readout";
    public const string ClosingPageId = "cash.close";
    public const string CutsPageId = "cash.cuts";

    /// <summary>
    /// Grupo "Caja" (017): Corte X, Corte Z e Histórico de cortes. Con el mismo orden que Clientes, el id
    /// <c>cash</c> lo deja justo después de Ventas. Sus permisos son del módulo "Turnos y arqueo", así que
    /// desaparece del menú sin licencia (FR-020).
    /// </summary>
    public static IServiceCollection AddCashModule(this IServiceCollection services)
    {
        services.AddNavigationGroup(GroupId, Strings.Nav_Cash, "Icon.Sales", 6);
        services.AddPage<ShiftReadoutViewModel, ShiftReadoutView>(ReadoutPageId, Strings.Nav_CashReadout, "Icon.Chart", 0, GroupId, permission: Permission.OperateShift);
        services.AddPage<ShiftClosingViewModel, ShiftClosingView>(ClosingPageId, Strings.Nav_CashClosing, "Icon.Lock", 10, GroupId, permission: Permission.OperateShift);
        services.AddPage<ShiftCutsViewModel, ShiftCutsView>(CutsPageId, Strings.Nav_CashCuts, "Icon.Movements", 20, GroupId, permission: Permission.ManageShifts);

        services.AddTransient<ShiftCutDetailViewModel>();
        services.AddScoped<Func<ShiftCutDetailViewModel>>(sp => sp.GetRequiredService<ShiftCutDetailViewModel>);
        services.AddComponentView<ShiftCutDetailViewModel, ShiftCutDetailView>();
        return services;
    }
}
