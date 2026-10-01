namespace Pos.Application.Reports;

/// <summary>Dibuja gráficas como imagen PNG; la implementa Infrastructure.</summary>
public interface IChartRenderer
{
    byte[] RenderPng(ChartSpec chart, int widthPx, int heightPx);
}
