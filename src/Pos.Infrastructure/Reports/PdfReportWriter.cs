using Pos.Application.Reports.Export;
using SkiaSharp;

namespace Pos.Infrastructure.Reports;

/// <summary>
/// PDF A4 horizontal con SkiaSharp (research §6): encabezado del negocio y pie con fecha y usuario en cada
/// página, métricas, gráficas en vectores y tablas paginadas que repiten su encabezado de columnas.
/// Cada página se registra primero como imagen para conocer el total ("Página n de N") y luego se escribe.
/// </summary>
public sealed class PdfReportWriter : IPdfReportWriter
{
    private const float PageWidth = 842;
    private const float PageHeight = 595;
    private const float Margin = 28;
    private const float HeaderHeight = 44;
    private const float FooterHeight = 22;
    private const float RowHeight = 16;

    /// <summary>Alto de cada línea adicional de una celda de texto con varias líneas (018, bitácora).</summary>
    private const float LineHeight = 11;
    private const float ChartHeight = 210;
    private const string MissingBusinessText = "Datos del negocio no capturados";

    private static readonly SKColor Ink = new(0x21, 0x25, 0x29);
    private static readonly SKColor Muted = new(0x6C, 0x75, 0x7D);
    private static readonly SKColor Line = new(0xCE, 0xD4, 0xDA);
    private static readonly SKColor Shade = new(0xF1, 0xF3, 0xF5);
    private static readonly SKColor HeaderShade = new(0xDE, 0xE2, 0xE6);

    public byte[] Write(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var layout = new Layout(document);
        var pages = layout.Build();
        try
        {
            using var stream = new MemoryStream();
            using (var output = new SKManagedWStream(stream))
            using (var pdf = SKDocument.CreatePdf(output))
            {
                for (var i = 0; i < pages.Count; i++)
                {
                    var canvas = pdf.BeginPage(PageWidth, PageHeight);
                    canvas.DrawPicture(pages[i]);
                    DrawHeader(canvas, document);
                    DrawFooter(canvas, document, i + 1, pages.Count);
                    pdf.EndPage();
                }

                pdf.Close();
            }

            return stream.ToArray();
        }
        finally
        {
            foreach (var page in pages)
            {
                page.Dispose();
            }
        }
    }

    private static void DrawHeader(SKCanvas canvas, ReportDocument document)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = Ink };
        using var bold = ReportFonts.Font(12, bold: true);
        using var small = ReportFonts.Font(9);
        if (document.Business is { } business)
        {
            canvas.DrawText(business.Name, Margin, Margin + 12, SKTextAlign.Left, bold, paint);
            paint.Color = Muted;
            canvas.DrawText($"{business.Address} · Tel. {business.Phone}", Margin, Margin + 26, SKTextAlign.Left, small, paint);
        }
        else
        {
            paint.Color = Muted;
            canvas.DrawText(MissingBusinessText, Margin, Margin + 12, SKTextAlign.Left, small, paint);
        }

        paint.Color = Line;
        paint.StrokeWidth = 0.8f;
        canvas.DrawLine(Margin, Margin + HeaderHeight - 8, PageWidth - Margin, Margin + HeaderHeight - 8, paint);
    }

    private static void DrawFooter(SKCanvas canvas, ReportDocument document, int page, int total)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = Line, StrokeWidth = 0.8f };
        using var font = ReportFonts.Font(8.5f);
        var y = PageHeight - Margin;
        canvas.DrawLine(Margin, y - FooterHeight + 6, PageWidth - Margin, y - FooterHeight + 6, paint);

        paint.Color = Muted;
        var generated = ReportCellFormatter.LocalText(document.GeneratedAtUtc);
        canvas.DrawText($"Generado el {generated} por {document.GeneratedBy}", Margin, y, SKTextAlign.Left, font, paint);
        canvas.DrawText($"Página {page} de {total}", PageWidth - Margin, y, SKTextAlign.Right, font, paint);
    }

    /// <summary>Distribuye el contenido en páginas; cada página es una imagen que se dibuja después.</summary>
    private sealed class Layout : IDisposable
    {
        private const float ContentLeft = Margin;
        private const float ContentWidth = PageWidth - (2 * Margin);
        private const float Top = Margin + HeaderHeight;
        private const float Bottom = PageHeight - Margin - FooterHeight;
        private const int MaxLinesPerRow = (int)((Bottom - Top - (2 * RowHeight)) / LineHeight);

        private readonly ReportDocument _document;
        private readonly List<SKPicture> _pages = [];
        private SKPictureRecorder? _recorder;
        private SKCanvas _canvas = null!;
        private float _y;

        public Layout(ReportDocument document) => _document = document;

        public void Dispose() => _recorder?.Dispose();

        public List<SKPicture> Build()
        {
            StartPage();
            DrawTitleBlock();
            DrawMetrics();
            DrawCharts();
            foreach (var table in _document.Tables)
            {
                DrawTable(table);
            }

            EndPage();
            return _pages;
        }

        private void StartPage()
        {
            _recorder = new SKPictureRecorder();
            _canvas = _recorder.BeginRecording(new SKRect(0, 0, PageWidth, PageHeight));
            _y = Top;
        }

        private void EndPage()
        {
            if (_recorder is null)
            {
                return;
            }

            _pages.Add(_recorder.EndRecording());
            _recorder.Dispose();
            _recorder = null;
        }

        private void NewPage()
        {
            EndPage();
            StartPage();
        }

        private void EnsureSpace(float height)
        {
            if (_y + height > Bottom)
            {
                NewPage();
            }
        }

        private void DrawTitleBlock()
        {
            using var paint = new SKPaint { IsAntialias = true, Color = Ink };
            using var title = ReportFonts.Font(18, bold: true);
            using var normal = ReportFonts.Font(11);
            using var small = ReportFonts.Font(9.5f);

            _y += 18;
            _canvas.DrawText(_document.Title, ContentLeft, _y, SKTextAlign.Left, title, paint);
            _y += 18;
            _canvas.DrawText(_document.PeriodText, ContentLeft, _y, SKTextAlign.Left, normal, paint);
            paint.Color = Muted;
            foreach (var filter in _document.FilterTexts)
            {
                _y += 13;
                _canvas.DrawText(Fit(filter, small, ContentWidth), ContentLeft, _y, SKTextAlign.Left, small, paint);
            }

            _y += 14;
        }

        private void DrawMetrics()
        {
            if (_document.Metrics.Count == 0)
            {
                return;
            }

            const int PerRow = 4;
            const float Gap = 10;
            const float BoxHeight = 42;
            var boxWidth = (ContentWidth - ((PerRow - 1) * Gap)) / PerRow;

            using var label = ReportFonts.Font(9);
            using var value = ReportFonts.Font(14, bold: true);
            using var paint = new SKPaint { IsAntialias = true };
            using var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, Color = Line, StrokeWidth = 0.8f };

            for (var i = 0; i < _document.Metrics.Count; i++)
            {
                if (i % PerRow == 0)
                {
                    EnsureSpace(BoxHeight + Gap);
                }

                var column = i % PerRow;
                var left = ContentLeft + (column * (boxWidth + Gap));
                var rect = new SKRect(left, _y, left + boxWidth, _y + BoxHeight);
                _canvas.DrawRoundRect(rect, 4, 4, border);

                var metric = _document.Metrics[i];
                paint.Color = Muted;
                _canvas.DrawText(Fit(metric.Label, label, boxWidth - 16), left + 8, _y + 15, SKTextAlign.Left, label, paint);
                paint.Color = Ink;
                _canvas.DrawText(Fit(ReportCellFormatter.Format(metric.Value), value, boxWidth - 16), left + 8, _y + 34, SKTextAlign.Left, value, paint);

                if (column == PerRow - 1 || i == _document.Metrics.Count - 1)
                {
                    _y += BoxHeight + Gap;
                }
            }
        }

        private void DrawCharts()
        {
            foreach (var chart in _document.Charts)
            {
                EnsureSpace(ChartHeight + 12);
                ChartRenderer.Draw(_canvas, chart, new SKRect(ContentLeft, _y, ContentLeft + ContentWidth, _y + ChartHeight));
                _y += ChartHeight + 12;
            }
        }

        private void DrawTable(ReportTable table)
        {
            if (table.Columns.Count == 0)
            {
                return;
            }

            var widths = ColumnWidths(table);
            using var titleFont = ReportFonts.Font(12, bold: true);
            using var headerFont = ReportFonts.Font(8.5f, bold: true);
            using var font = ReportFonts.Font(8.5f);
            using var paint = new SKPaint { IsAntialias = true, Color = Ink };

            EnsureSpace(18 + (RowHeight * 3));
            _canvas.DrawText(table.Title, ContentLeft, _y + 12, SKTextAlign.Left, titleFont, paint);
            _y += 20;
            DrawHeaderRow(table, widths, headerFont, paint);

            for (var r = 0; r < table.Rows.Count; r++)
            {
                // Una celda de texto puede traer varias líneas: la fila crece hasta lo que quepa en una página.
                var row = table.Rows[r];
                var texts = row.Select(ReportCellFormatter.Format).ToList();
                var lines = Math.Min(texts.Max(t => t.Split('\n').Length), MaxLinesPerRow);
                var height = RowHeight + ((lines - 1) * LineHeight);
                if (_y + height > Bottom)
                {
                    NewPage();
                    DrawHeaderRow(table, widths, headerFont, paint);
                }

                if (r % 2 == 1)
                {
                    paint.Color = Shade;
                    _canvas.DrawRect(new SKRect(ContentLeft, _y, ContentLeft + ContentWidth, _y + height), paint);
                }

                paint.Color = Ink;
                var x = ContentLeft;
                for (var c = 0; c < table.Columns.Count; c++)
                {
                    var cellLines = texts[c].Split('\n');
                    for (var l = 0; l < Math.Min(cellLines.Length, lines); l++)
                    {
                        var text = l == lines - 1 && cellLines.Length > lines ? cellLines[l] + " …" : cellLines[l];
                        DrawCell(text, x, widths[c], table.Columns[c].Type, font, paint, l * LineHeight);
                    }

                    x += widths[c];
                }

                _y += height;
            }

            _y += 10;
        }

        private void DrawHeaderRow(ReportTable table, float[] widths, SKFont font, SKPaint paint)
        {
            paint.Color = HeaderShade;
            _canvas.DrawRect(new SKRect(ContentLeft, _y, ContentLeft + ContentWidth, _y + RowHeight), paint);
            paint.Color = Ink;
            var x = ContentLeft;
            for (var c = 0; c < table.Columns.Count; c++)
            {
                DrawCell(table.Columns[c].Header, x, widths[c], table.Columns[c].Type, font, paint);
                x += widths[c];
            }

            _y += RowHeight;
        }

        private void DrawCell(string text, float x, float width, ReportColumnType type, SKFont font, SKPaint paint, float offset = 0)
        {
            var padded = width - 8;
            var fitted = Fit(text, font, padded);
            if (ReportCellFormatter.IsNumeric(type))
            {
                _canvas.DrawText(fitted, x + width - 4, _y + 11.5f + offset, SKTextAlign.Right, font, paint);
            }
            else
            {
                _canvas.DrawText(fitted, x + 4, _y + 11.5f + offset, SKTextAlign.Left, font, paint);
            }
        }

        /// <summary>Anchos proporcionales al tipo de columna, que suman el ancho útil de la página.</summary>
        private static float[] ColumnWidths(ReportTable table)
        {
            var weights = table.Columns.Select(c => c.Type switch
            {
                ReportColumnType.Text => 2.4f,
                ReportColumnType.Date => 1.7f,
                ReportColumnType.Money => 1.4f,
                ReportColumnType.Count => 1.0f,
                ReportColumnType.Quantity => 1.2f,
                _ => 1.0f,
            }).ToArray();
            var total = weights.Sum();
            return [.. weights.Select(w => ContentWidth * w / total)];
        }

        /// <summary>Recorta el texto con "…" si no cabe en el ancho dado.</summary>
        private static string Fit(string text, SKFont font, float maxWidth)
        {
            if (font.MeasureText(text) <= maxWidth)
            {
                return text;
            }

            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (font.MeasureText(text[..middle] + "…") <= maxWidth)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return text[..low] + "…";
        }
    }
}
