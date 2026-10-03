using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Users;

namespace Pos.Desktop.CashShifts;

public static class CashShiftsModule
{
    public const string ShiftsPageId = "sales.shifts";

    /// <summary>
    /// Turnos de caja (008): la pantalla "Turnos" del administrador dentro del grupo Ventas, los diálogos
    /// de apertura, movimiento y cierre, el detalle y la tarjeta de Inicio.
    /// </summary>
    public static IServiceCollection AddCashShiftsModule(this IServiceCollection services)
    {
        services.AddPage<ShiftsViewModel, ShiftsView>(ShiftsPageId, Strings.Nav_Shifts, "Icon.CalendarClock", 20, SalesModule.GroupId, permission: Permission.ManageShifts);

        services.AddScoped<CashShiftDialogs>();
        services.AddTransient<ShiftDetailViewModel>();
        services.AddScoped<Func<ShiftDetailViewModel>>(sp => sp.GetRequiredService<ShiftDetailViewModel>);
        services.AddComponentView<ShiftDetailViewModel, ShiftDetailView>();
        services.AddComponentView<OpenShiftViewModel, OpenShiftView>();
        services.AddComponentView<CashMovementViewModel, CashMovementView>();
        services.AddComponentView<CloseShiftViewModel, CloseShiftView>();

        // Tarjeta de Inicio: métrica de orden 100, antes de las de ventas (110).
        services.AddDashboardCard<CurrentShiftCard>();
        return services;
    }
}
