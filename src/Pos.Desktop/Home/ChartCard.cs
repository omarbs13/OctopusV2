using Pos.Desktop.Common;

namespace Pos.Desktop.Home;

/// <summary>Una barra de una gráfica: etiqueta, valor ya formateado y proporción de 0 a 1.</summary>
public sealed record ChartBar(string Label, string ValueText, double Ratio);

/// <summary>
/// Tarjeta de Inicio con gráfica de barras. Las barras se dibujan con controles de Avalonia, sin
/// librería de gráficas (research §11).
/// </summary>
public abstract class ChartCard : DashboardCard
{
    private IReadOnlyList<ChartBar> _bars = [];

    protected ChartCard(OperationRunner runner)
        : base(runner)
    {
    }

    public override DashboardCardKind Kind => DashboardCardKind.Chart;

    public override IReadOnlyList<ChartBar> Bars => _bars;

    protected void SetBars(IReadOnlyList<ChartBar> bars)
    {
        _bars = bars;
        OnPropertyChanged(nameof(Bars));
    }

    /// <summary>Proporción de una barra respecto de la mayor; cero si no hay ventas.</summary>
    protected static double RatioOf(long value, long max) => max <= 0 ? 0 : Math.Clamp((double)value / max, 0, 1);
}
