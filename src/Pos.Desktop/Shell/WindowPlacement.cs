using Avalonia;
using Avalonia.Controls;

namespace Pos.Desktop.Shell;

/// <summary>Estado de la ventana principal guardado por equipo en &lt;datos&gt;/preferences/window.json.</summary>
/// <param name="State">"Maximized" | "Normal". Último estado no minimizado. Un valor desconocido se trata como sin preferencia.</param>
/// <param name="X">Posición horizontal del estado normal, en píxeles de pantalla.</param>
/// <param name="Y">Posición vertical del estado normal, en píxeles de pantalla.</param>
/// <param name="Width">Ancho del área cliente del estado normal, en DIP; ≥ mínimo efectivo.</param>
/// <param name="Height">Alto del área cliente del estado normal, en DIP; ≥ mínimo efectivo.</param>
public sealed record WindowPlacement(string State, int X, int Y, double Width, double Height)
{
    /// <summary>Clave de la preferencia (archivo <c>window.json</c>).</summary>
    public const string PreferenceKey = "window";

    public const string Maximized = "Maximized";

    public const string Normal = "Normal";
}

/// <summary>Colocación que se aplica a la ventana antes de mostrarla.</summary>
/// <param name="State">Estado inicial (maximizada o normal).</param>
/// <param name="Position">Posición del estado normal, en píxeles de pantalla.</param>
/// <param name="Size">Tamaño del estado normal, en DIP; también es el de la restauración si abre maximizada.</param>
public sealed record ResolvedWindowPlacement(WindowState State, PixelPoint Position, Size Size);

/// <summary>Reglas para restaurar la ventana principal (FR-001 a FR-006).</summary>
public static class WindowPlacementRules
{
    /// <summary>Mínimo del área cliente en pantallas que lo permiten (FR-005).</summary>
    public static readonly Size Minimum = new(1024, 768);

    /// <summary>Tamaño normal cuando no hay preferencia guardada.</summary>
    public static readonly Size DefaultNormalSize = new(1200, 800);

    /// <summary>Mínimo efectivo: 1024×768 o el área de trabajo en DIP si es menor (FR-006).</summary>
    public static Size EffectiveMinimum(PixelRect workingArea, double scaling)
    {
        var scale = scaling > 0 ? scaling : 1;
        return new Size(
            Math.Min(Minimum.Width, workingArea.Width / scale),
            Math.Min(Minimum.Height, workingArea.Height / scale));
    }

    /// <summary>
    /// Decide estado, posición y tamaño a partir de lo guardado y de las pantallas conectadas.
    /// </summary>
    /// <param name="saved">Preferencia guardada; nula si no existe o no se pudo leer.</param>
    /// <param name="workingAreas">Áreas de trabajo de las pantallas; la primera es la principal.</param>
    /// <param name="scaling">Escala de la pantalla principal (píxeles por DIP).</param>
    /// <param name="minimumSize">Mínimo efectivo del área cliente, en DIP.</param>
    public static ResolvedWindowPlacement Resolve(
        WindowPlacement? saved,
        IReadOnlyList<PixelRect> workingAreas,
        double scaling,
        Size minimumSize)
    {
        ArgumentNullException.ThrowIfNull(workingAreas);
        var scale = scaling > 0 ? scaling : 1;
        var primary = workingAreas.Count > 0 ? workingAreas[0] : new PixelRect(0, 0, 1920, 1080);

        var state = saved?.State switch
        {
            WindowPlacement.Maximized => WindowState.Maximized,
            WindowPlacement.Normal => WindowState.Normal,
            _ => (WindowState?)null,
        };

        if (saved is null || state is null)
        {
            var size = Raise(Fit(DefaultNormalSize, primary, scale), minimumSize);
            return new ResolvedWindowPlacement(WindowState.Maximized, Center(size, primary, scale), size);
        }

        var savedSize = Raise(new Size(saved.Width, saved.Height), minimumSize);
        var position = new PixelPoint(saved.X, saved.Y);
        var rect = new PixelRect(position, PixelSize.FromSize(savedSize, scale));
        if (!workingAreas.Any(area => area.Intersects(rect)))
        {
            position = Center(savedSize, primary, scale);
        }

        return new ResolvedWindowPlacement(state.Value, position, savedSize);
    }

    private static Size Fit(Size size, PixelRect area, double scale) =>
        new(Math.Min(size.Width, area.Width / scale), Math.Min(size.Height, area.Height / scale));

    private static Size Raise(Size size, Size minimum) =>
        new(
            double.IsFinite(size.Width) ? Math.Max(size.Width, minimum.Width) : minimum.Width,
            double.IsFinite(size.Height) ? Math.Max(size.Height, minimum.Height) : minimum.Height);

    private static PixelPoint Center(Size size, PixelRect area, double scale) =>
        new(
            area.X + (int)Math.Max(0, (area.Width - (size.Width * scale)) / 2),
            area.Y + (int)Math.Max(0, (area.Height - (size.Height * scale)) / 2));
}
