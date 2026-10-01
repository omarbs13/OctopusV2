using System.Globalization;
using Pos.Application.Reports;
using SkiaSharp;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// Dibuja línea, barras con signo y pastel con SkiaSharp (research §8). La misma rutina
/// (<see cref="Draw"/>) pinta la gráfica en pantalla, en el PDF (vectores) y en el XLSX (PNG).
/// </summary>
public sealed class ChartRenderer : IChartRenderer
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");

    private static readonly SKColor Ink = new(0x21, 0x25, 0x29);
    private static readonly SKColor Muted = new(0x6C, 0x75, 0x7D);
    private static readonly SKColor Grid = new(0xDE, 0xE2, 0xE6);
    private static readonly SKColor Positive = new(0x2E, 0x7D, 0x32);
    private static readonly SKColor Negative = new(0xC6, 0x28, 0x28);
    private static readonly SKColor LineColor = new(0x15, 0x65, 0xC0);

    private static readonly SKColor[] Palette =
    [
        new(0x15, 0x65, 0xC0), new(0xEF, 0x6C, 0x00), new(0x2E, 0x7D, 0x32), new(0x8E, 0x24, 0xAA),
        new(0x00, 0x83, 0x8F), new(0xC6, 0x28, 0x28), new(0x6D, 0x4C, 0x41), new(0x54, 0x6E, 0x7A),
    ];

    public byte[] RenderPng(ChartSpec chart, int widthPx, int heightPx)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentOutOfRangeException.ThrowIfLessThan(widthPx, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(heightPx, 1);

        using var surface = SKSurface.Create(new SKImageInfo(widthPx, heightPx, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        Draw(canvas, chart, new SKRect(0, 0, widthPx, heightPx));

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Dibuja la gráfica dentro del rectángulo; no limpia el fondo.</summary>
    internal static void Draw(SKCanvas canvas, ChartSpec chart, SKRect area)
    {
        using var titleFont = ReportFonts.Font(Math.Clamp(area.Height / 16f, 10f, 15f), bold: true);
        using var paint = new SKPaint { IsAntialias = true, Color = Ink };
        var titleHeight = titleFont.Size + 10;
        canvas.DrawText(chart.Title, area.Left + 4, area.Top + titleFont.Size + 2, SKTextAlign.Left, titleFont, paint);

        var plot = new SKRect(area.Left, area.Top + titleHeight, area.Right, area.Bottom);
        if (chart.Points.Count == 0)
        {
            return;
        }

        switch (chart.Kind)
        {
            case ChartKind.Line:
                DrawLine(canvas, chart, plot);
                break;
            case ChartKind.Bars:
                DrawBars(canvas, chart, plot);
                break;
            default:
                DrawPie(canvas, chart, plot);
                break;
        }
    }

    internal static string FormatValue(long value, ChartValueFormat format, bool withSign = false)
    {
        var text = format == ChartValueFormat.Money
            ? (Math.Abs(value) / 100m).ToString("C2", Culture)
            : Math.Abs(value).ToString("N0", Culture);
        if (value < 0)
        {
            return "-" + text;
        }

        return withSign && value > 0 ? "+" + text : text;
    }

    private static void DrawLine(SKCanvas canvas, ChartSpec chart, SKRect plot)
    {
        var points = chart.Points;
        var (axisMin, axisMax, step) = NiceAxis(0, Math.Max(points.Max(p => p.Value), 1));
        var (left, top, right, bottom) = Frame(plot, chart, axisMin, axisMax);
        DrawGrid(canvas, chart, left, top, right, bottom, axisMin, axisMax, step);

        float X(int i) => points.Count == 1 ? (left + right) / 2 : left + (right - left) * i / (points.Count - 1);
        float Y(long v) => bottom - (bottom - top) * (v - axisMin) / (axisMax - axisMin);

        using var stroke = new SKPaint { IsAntialias = true, Color = LineColor, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, StrokeJoin = SKStrokeJoin.Round };
        using var path = new SKPath();
        for (var i = 0; i < points.Count; i++)
        {
            if (i == 0)
            {
                path.MoveTo(X(i), Y(points[i].Value));
            }
            else
            {
                path.LineTo(X(i), Y(points[i].Value));
            }
        }

        canvas.DrawPath(path, stroke);

        using var dot = new SKPaint { IsAntialias = true, Color = LineColor };
        if (points.Count <= 62)
        {
            for (var i = 0; i < points.Count; i++)
            {
                canvas.DrawCircle(X(i), Y(points[i].Value), 3f, dot);
            }
        }

        DrawCategoryLabels(canvas, points.Select(p => p.Label).ToList(), X, bottom);
    }

    private static void DrawBars(SKCanvas canvas, ChartSpec chart, SKRect plot)
    {
        var points = chart.Points;
        var min = Math.Min(points.Min(p => p.Value), 0);
        var max = Math.Max(points.Max(p => p.Value), 0);
        if (max == min)
        {
            max = min + 1;
        }

        // Margen del 12 % hacia el lado de las barras para que quepan las etiquetas de valor.
        var margin = Math.Max((max - min) * 12 / 100, 1);
        var (axisMin, axisMax, step) = NiceAxis(min < 0 ? min - margin : 0, max > 0 ? max + margin : 0);
        var (left, top, right, bottom) = Frame(plot, chart, axisMin, axisMax);
        DrawGrid(canvas, chart, left, top, right, bottom, axisMin, axisMax, step);

        float Y(long v) => bottom - (bottom - top) * (v - axisMin) / (axisMax - axisMin);
        var slot = (right - left) / points.Count;
        var barWidth = Math.Min(slot * 0.6f, 60f);
        var zeroY = Y(0);

        using var valueFont = ReportFonts.Font(9f);
        using var paint = new SKPaint { IsAntialias = true };
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var centerX = left + slot * (i + 0.5f);
            paint.Color = point.Color is { } hex && SKColor.TryParse(hex, out var custom)
                ? custom
                : point.Value < 0 ? Negative : Positive;
            var y = Y(point.Value);
            canvas.DrawRect(new SKRect(centerX - barWidth / 2, Math.Min(y, zeroY), centerX + barWidth / 2, Math.Max(y, zeroY)), paint);

            paint.Color = Ink;
            var label = FormatValue(point.Value, chart.ValueFormat, withSign: true);
            var labelY = point.Value < 0 ? Math.Max(y, zeroY) + valueFont.Size + 2 : Math.Min(y, zeroY) - 3;
            canvas.DrawText(label, centerX, labelY, SKTextAlign.Center, valueFont, paint);
        }

        using var axis = new SKPaint { IsAntialias = true, Color = Muted, StrokeWidth = 1f };
        canvas.DrawLine(left, zeroY, right, zeroY, axis);
        DrawCategoryLabels(canvas, points.Select(p => p.Label).ToList(), i => left + slot * (i + 0.5f), bottom, slot);
    }

    private static void DrawPie(SKCanvas canvas, ChartSpec chart, SKRect plot)
    {
        var total = chart.Points.Sum(p => Math.Max(p.Value, 0));
        using var legendFont = ReportFonts.Font(11f);
        using var paint = new SKPaint { IsAntialias = true };

        var size = Math.Min(plot.Height - 8, plot.Width * 0.55f);
        if (size <= 0 || total <= 0)
        {
            paint.Color = Muted;
            canvas.DrawText("Sin datos", plot.Left + 4, plot.Top + 20, SKTextAlign.Left, legendFont, paint);
            return;
        }

        var circle = new SKRect(plot.Left + 4, plot.Top + 4, plot.Left + 4 + size, plot.Top + 4 + size);
        float start = -90;
        for (var i = 0; i < chart.Points.Count; i++)
        {
            var point = chart.Points[i];
            if (point.Value <= 0)
            {
                continue;
            }

            var sweep = 360f * point.Value / total;
            paint.Color = ColorOf(point, i);
            paint.Style = SKPaintStyle.Fill;
            if (point.Value == total)
            {
                canvas.DrawOval(circle, paint);
            }
            else
            {
                canvas.DrawArc(circle, start, sweep, true, paint);
            }

            start += sweep;
        }

        var x = circle.Right + 16;
        var y = plot.Top + 22;
        for (var i = 0; i < chart.Points.Count; i++)
        {
            var point = chart.Points[i];
            paint.Color = ColorOf(point, i);
            canvas.DrawRect(new SKRect(x, y - 10, x + 12, y + 2), paint);
            paint.Color = Ink;
            var percent = total == 0 ? 0 : Math.Max(point.Value, 0) * 100m / total;
            var text = string.Create(Culture, $"{point.Label}: {FormatValue(point.Value, chart.ValueFormat)} ({percent:0.#} %)");
            canvas.DrawText(text, x + 18, y, SKTextAlign.Left, legendFont, paint);
            y += legendFont.Size + 10;
        }
    }

    private static SKColor ColorOf(ChartPoint point, int index) =>
        point.Color is { } hex && SKColor.TryParse(hex, out var color) ? color : Palette[index % Palette.Length];

    private static (float Left, float Top, float Right, float Bottom) Frame(
        SKRect plot,
        ChartSpec chart,
        long min,
        long max,
        float labelSpace = 18)
    {
        using var font = ReportFonts.Font(9f);
        var widest = new[] { min, max, 0 }
            .Select(v => font.MeasureText(FormatValue(v, chart.ValueFormat)))
            .Max();
        return (plot.Left + widest + 10, plot.Top + 14, plot.Right - 10, plot.Bottom - labelSpace - 6);
    }

    /// <summary>Eje con valores "bonitos" (1, 2, 5 por potencias de 10) que contiene [min, max], con unos 4 intervalos.</summary>
    internal static (long Min, long Max, long Step) NiceAxis(long min, long max)
    {
        if (max <= min)
        {
            max = min + 1;
        }

        var raw = (max - min) / 4.0;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var normalized = raw / magnitude;
        var nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        var step = Math.Max((long)(nice * magnitude), 1);
        var axisMin = (long)Math.Floor(min / (double)step) * step;
        var axisMax = (long)Math.Ceiling(max / (double)step) * step;
        return (axisMin, axisMax == axisMin ? axisMin + step : axisMax, step);
    }

    private static void DrawGrid(SKCanvas canvas, ChartSpec chart, float left, float top, float right, float bottom, long axisMin, long axisMax, long step)
    {
        using var font = ReportFonts.Font(9f);
        using var paint = new SKPaint { IsAntialias = true, Color = Grid, StrokeWidth = 1f };
        using var text = new SKPaint { IsAntialias = true, Color = Muted };
        for (var value = axisMin; value <= axisMax; value += step)
        {
            var y = bottom - (bottom - top) * (value - axisMin) / (axisMax - axisMin);
            canvas.DrawLine(left, y, right, y, paint);
            canvas.DrawText(FormatValue(value, chart.ValueFormat), left - 6, y + 3, SKTextAlign.Right, font, text);
        }
    }

    private static void DrawCategoryLabels(SKCanvas canvas, List<string> labels, Func<int, float> x, float bottom, float? slot = null)
    {
        using var font = ReportFonts.Font(9f);
        using var paint = new SKPaint { IsAntialias = true, Color = Muted };
        var maxWidth = labels.Max(l => font.MeasureText(l)) + 8;
        var spacing = slot ?? (labels.Count > 1 ? Math.Abs(x(1) - x(0)) : maxWidth);
        var step = Math.Max(1, (int)Math.Ceiling(maxWidth / Math.Max(spacing, 1f)));
        for (var i = 0; i < labels.Count; i += step)
        {
            canvas.DrawText(labels[i], x(i), bottom + 16, SKTextAlign.Center, font, paint);
        }
    }
}
