using System.Globalization;
using System.Text;
using Pos.Application.Abstractions;
using Pos.Application.Reports.GetReportAlerts;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Reports;

/// <summary>
/// Tarjeta "Alertas" de Inicio para el Administrador (Historia 7): diferencias de arqueo sobre el umbral en
/// los últimos 7 días y productos críticos con existencia baja o agotada, hasta 10 de cada una con el total.
/// Sin alertas dice "No hay alertas". Lleva al reporte de arqueo.
/// </summary>
public sealed class AlertsCard(OperationRunner runner, UseCases useCases) : DashboardCard(runner)
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    public override string Title => Strings.Card_Alerts;

    public override string Icon => "Icon.Warning";

    public override int Order => 120;

    public override Permission? RequiredPermission => Permission.ViewReports;

    public override DashboardCardKind Kind => DashboardCardKind.Metric;

    public override string? NavigateTo => ReportsModule.CashCountPageId;

    protected override async Task LoadCoreAsync()
    {
        var result = await useCases.RunAsync<GetReportAlertsHandler, Result<ReportAlerts>>(h => h.HandleAsync(CancellationToken.None));
        if (result.Error is ModuleNotLicensed)
        {
            // Reportes sin licencia (012): la tarjeta se oculta sin mostrar error.
            SetEmpty(Strings.Card_Unavailable);
            return;
        }

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("No se pudieron leer las alertas.");
        }

        var alerts = result.Value;
        if (alerts.IsEmpty)
        {
            SetEmpty(Strings.Card_NoAlerts);
            return;
        }

        SetReady((alerts.CashAlertTotal + alerts.CriticalLowStockTotal).ToString("N0", Display), Describe(alerts));
    }

    internal static string Describe(ReportAlerts alerts)
    {
        var text = new StringBuilder();
        if (alerts.CashAlertTotal > 0)
        {
            text.AppendLine(string.Format(Display, Strings.Card_AlertsCash, alerts.CashAlertTotal));
            foreach (var alert in alerts.CashAlerts)
            {
                text.AppendLine(string.Format(
                    Display,
                    Strings.Card_AlertsCashLine,
                    alert.FolioText,
                    alert.CashierName,
                    alert.OpenedAtUtc.ToLocalTime(),
                    CashCountRowItem.Signed(alert.DifferenceCents)));
            }

            AppendMore(text, alerts.CashAlertTotal - alerts.CashAlerts.Count);
        }

        if (alerts.CriticalLowStockTotal > 0)
        {
            text.AppendLine(string.Format(Display, Strings.Card_AlertsCritical, alerts.CriticalLowStockTotal));
            foreach (var product in alerts.CriticalLowStock)
            {
                text.AppendLine(string.Format(
                    Display,
                    Strings.Card_AlertsCriticalLine,
                    product.Name,
                    product.Sku,
                    QuantityConverter.Format(product.OnHandThousandths, product.DecimalPlaces),
                    product.UnitName));
            }

            AppendMore(text, alerts.CriticalLowStockTotal - alerts.CriticalLowStock.Count);
        }

        return text.ToString().TrimEnd();
    }

    private static void AppendMore(StringBuilder text, int remaining)
    {
        if (remaining > 0)
        {
            text.AppendLine(string.Format(Display, Strings.Card_AlertsMore, remaining));
        }
    }
}
