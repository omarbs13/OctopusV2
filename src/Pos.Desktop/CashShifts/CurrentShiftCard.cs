using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.GetCurrentShift;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Users;

namespace Pos.Desktop.CashShifts;

/// <summary>
/// Tarjeta de Inicio con el turno actual (Historia 6): usuario, hora de apertura y total vendido, o
/// "No hay turno abierto". Con turno lleva al Punto de venta. Usa el mismo resumen que el cajero, que
/// nunca trae datos de efectivo (FR-022).
/// </summary>
public sealed class CurrentShiftCard(OperationRunner runner, UseCases useCases) : DashboardCard(runner)
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    public override string Title => Strings.Card_Shift;

    public override string Icon => "Icon.Sales";

    public override int Order => 100;

    public override Permission? RequiredPermission => Permission.OperateShift;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    public override string? NavigateTo => SalesModule.PointOfSalePageId;

    protected override async Task LoadCoreAsync()
    {
        var result = await useCases.RunAsync<GetCurrentShiftHandler, Result<CurrentShiftSummary?>>(
            h => h.HandleAsync(CancellationToken.None));
        if (result.Value is not { } shift)
        {
            SetEmpty(Strings.Card_NoShift);
            return;
        }

        var since = shift.OpenedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        SetReady(
            MoneyConverter.Format(shift.TotalSoldCents),
            string.Format(Display, Strings.Card_ShiftOf, shift.OpenedByName)
                + " · "
                + string.Format(Display, Strings.Card_ShiftSince, since));
    }
}
