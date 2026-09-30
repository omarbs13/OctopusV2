using System.Globalization;
using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Sales.GetSalesDashboard;
using Pos.Desktop.Common;
using Pos.Desktop.Home;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Sales;

/// <summary>
/// Una sola consulta a <see cref="GetSalesDashboardHandler"/> por activación de Inicio, compartida por
/// las tres tarjetas de ventas (contracts/ui.md). La primera tarjeta (<see cref="SalesTodayChart"/>)
/// la refresca; las otras esperan ese mismo resultado.
/// </summary>
public sealed class SalesDashboardSource
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private Task<SalesDashboard>? _current;

    public SalesDashboardSource(UseCases useCases) => _useCases = useCases;

    /// <summary>Inicia una consulta nueva.</summary>
    public Task<SalesDashboard> RefreshAsync() => _current = FetchAsync();

    /// <summary>La consulta de esta activación, o una nueva si aún no hay ninguna.</summary>
    public Task<SalesDashboard> CurrentOrRefreshAsync() => _current ?? RefreshAsync();

    /// <summary>Los últimos 7 días locales convertidos a UTC: <c>[00:00 local, 00:00 del día siguiente)</c>; el último es hoy.</summary>
    public static IReadOnlyList<DayWindow> LastSevenDays(DateTime nowLocal)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        return [.. Enumerable.Range(0, 7).Select(i =>
        {
            var date = today.AddDays(i - 6);
            return new DayWindow(date, LocalMidnightUtc(date), LocalMidnightUtc(date.AddDays(1)));
        })];
    }

    /// <summary>Etiqueta corta del día, por ejemplo "lun 28".</summary>
    public static string DayLabel(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("ddd d", Culture).Replace(".", string.Empty, StringComparison.Ordinal);

    private static DateTime LocalMidnightUtc(DateOnly date) =>
        new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();

    private async Task<SalesDashboard> FetchAsync()
    {
        var days = LastSevenDays(DateTime.Now);
        var result = await _useCases.RunAsync<GetSalesDashboardHandler, Result<SalesDashboard>>(
            h => h.HandleAsync(new SalesDashboardQuery(days), CancellationToken.None));
        return result.Value;
    }
}

/// <summary>Ventas del día: importe y número de ventas completadas; lleva a "Ventas realizadas" de hoy.</summary>
public sealed class SalesTodayChart(OperationRunner runner, SalesDashboardSource source) : ChartCard(runner)
{
    public override string Title => Strings.Chart_SalesToday;

    public override string Icon => "Icon.Sales";

    public override int Order => 110;

    public override string? NavigateTo => SalesModule.HistoryPageId;

    public override object? NavigationArgument => new SalesDateFilter(DateTime.Today, DateTime.Today);

    protected override async Task LoadCoreAsync()
    {
        var dashboard = await source.RefreshAsync();
        var today = dashboard.Days[^1];

        // Con cero ventas se muestra "$0.00 · 0 ventas", no el estado vacío.
        SetBars([]);
        SetReady(string.Format(
            CultureInfo.GetCultureInfo("es-MX"),
            today.Count == 1 ? Strings.Card_SalesTodayOne : Strings.Card_SalesToday,
            MoneyConverter.Format(today.TotalCents),
            today.Count));
    }
}

/// <summary>Ventas de los últimos 7 días: una barra por día.</summary>
public sealed class SalesLast7DaysChart(OperationRunner runner, SalesDashboardSource source) : ChartCard(runner)
{
    public override string Title => Strings.Chart_SalesLast7Days;

    public override string Icon => "Icon.Chart";

    public override int Order => 120;

    protected override async Task LoadCoreAsync()
    {
        var dashboard = await source.CurrentOrRefreshAsync();
        if (dashboard.Days.All(d => d.TotalCents == 0 && d.Count == 0))
        {
            SetBars([]);
            SetEmpty(Strings.Card_SalesWeekEmpty);
            return;
        }

        var max = dashboard.Days.Max(d => d.TotalCents);
        SetBars([.. dashboard.Days.Select(d => new ChartBar(
            SalesDashboardSource.DayLabel(d.LocalDate),
            MoneyConverter.Format(d.TotalCents),
            RatioOf(d.TotalCents, max)))]);
        SetReady(null);
    }
}

/// <summary>Productos más vendidos de los últimos 7 días: hasta 5 barras horizontales.</summary>
public sealed class TopProductsChart(OperationRunner runner, SalesDashboardSource source) : ChartCard(runner)
{
    public override string Title => Strings.Chart_TopProducts;

    public override string Icon => "Icon.Product";

    public override int Order => 130;

    public override bool IsHorizontal => true;

    protected override async Task LoadCoreAsync()
    {
        var dashboard = await source.CurrentOrRefreshAsync();
        if (dashboard.TopProducts.Count == 0)
        {
            SetBars([]);
            SetEmpty(Strings.Card_TopProductsEmpty);
            return;
        }

        var max = dashboard.TopProducts.Max(p => p.QuantityThousandths);
        SetBars([.. dashboard.TopProducts.Select(p => new ChartBar(
            p.Name,
            QuantityConverter.Format(p.QuantityThousandths, p.DecimalPlaces),
            RatioOf(p.QuantityThousandths, max)))]);
        SetReady(null);
    }
}
