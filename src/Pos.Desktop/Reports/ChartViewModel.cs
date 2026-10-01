using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Reports;
using Pos.Desktop.Common;

namespace Pos.Desktop.Reports;

/// <summary>
/// Muestra una gráfica como imagen: el <see cref="IChartRenderer"/> dibuja el mismo <see cref="ChartSpec"/>
/// que usan el PDF y el XLSX, así que lo exportado se ve igual que la pantalla (research §8). La vista
/// avisa los cambios de tamaño y la imagen se vuelve a dibujar con un retardo corto.
/// </summary>
public sealed partial class ChartViewModel : ViewModelBase
{
    private const int DefaultWidth = 720;
    private const int DefaultHeight = 300;
    private static readonly TimeSpan ResizeDelay = TimeSpan.FromMilliseconds(150);

    private readonly IChartRenderer _renderer;
    private ChartSpec? _spec;
    private int _width = DefaultWidth;
    private int _height = DefaultHeight;
    private int _version;

    public ChartViewModel(IChartRenderer renderer) => _renderer = renderer;

    [ObservableProperty]
    public partial Bitmap? Image { get; private set; }

    [ObservableProperty]
    public partial bool HasChart { get; private set; }

    /// <summary>Dibuja la gráfica, o la quita si <paramref name="spec"/> es nulo.</summary>
    public void Set(ChartSpec? spec)
    {
        _spec = spec;
        Render();
    }

    /// <summary>La vista cambió de tamaño: vuelve a dibujar tras un instante sin más cambios.</summary>
    public async Task ResizeAsync(int width, int height)
    {
        if (width < 100 || height < 80 || (width == _width && height == _height))
        {
            return;
        }

        var version = Interlocked.Increment(ref _version);
        await Task.Delay(ResizeDelay);
        if (version != _version)
        {
            return;
        }

        _width = width;
        _height = height;
        Render();
    }

    private void Render()
    {
        var previous = Image;
        if (_spec is null)
        {
            Image = null;
            HasChart = false;
        }
        else
        {
            using var stream = new MemoryStream(_renderer.RenderPng(_spec, _width, _height));
            Image = new Bitmap(stream);
            HasChart = true;
        }

        previous?.Dispose();
    }
}
