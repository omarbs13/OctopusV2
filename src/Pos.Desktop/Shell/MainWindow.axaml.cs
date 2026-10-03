using Avalonia;
using Avalonia.Controls;

namespace Pos.Desktop.Shell;

public partial class MainWindow : Window
{
    // Último estado no minimizado y geometría del estado normal; se guardan al cerrar (FR-002, FR-003).
    private string _lastState = WindowPlacement.Maximized;
    private PixelPoint _normalPosition;
    private Size _normalSize = WindowPlacementRules.DefaultNormalSize;

    public MainWindow()
    {
        InitializeComponent();

        PropertyChanged += (_, e) =>
        {
            // El menú se contrae solo en ventanas angostas (FR-018).
            if (e.Property == BoundsProperty && DataContext is RootViewModel root)
            {
                root.SetWindowWidth(Bounds.Width);
            }
            else if (e.Property == WindowStateProperty)
            {
                OnWindowStateChanged();
            }
            else if (e.Property == ClientSizeProperty)
            {
                OnClientSizeChanged();
            }
        };

        PositionChanged += (_, e) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _normalPosition = e.Point;
            }
        };
    }

    /// <summary>
    /// Aplica, antes de <see cref="Window.Show()"/>, la colocación guardada (o la predeterminada) contra las
    /// pantallas conectadas. Aunque abra maximizada, recibe el último tamaño normal para la restauración.
    /// </summary>
    public void ApplyPlacement(WindowPlacement? saved)
    {
        var primary = Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
        var scaling = primary?.Scaling ?? 1;
        var workingAreas = Screens.All
            .OrderByDescending(screen => screen == primary)
            .Select(screen => screen.WorkingArea)
            .ToList();

        if (primary is not null)
        {
            SetEffectiveMinimum(WindowPlacementRules.EffectiveMinimum(primary.WorkingArea, scaling));
        }

        var placement = WindowPlacementRules.Resolve(saved, workingAreas, scaling, new Size(MinWidth, MinHeight));
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = placement.Position;
        Width = placement.Size.Width;
        Height = placement.Size.Height;
        WindowState = placement.State;

        _normalPosition = placement.Position;
        _normalSize = placement.Size;
        _lastState = placement.State == WindowState.Maximized ? WindowPlacement.Maximized : WindowPlacement.Normal;
    }

    /// <summary>Colocación actual para guardarla al cerrar; nunca es minimizada (FR-003).</summary>
    public WindowPlacement CapturePlacement() =>
        new(_lastState, _normalPosition.X, _normalPosition.Y, _normalSize.Width, _normalSize.Height);

    /// <summary>Restaura y trae al frente la ventana (por ejemplo, al intentar abrir otra instancia).</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // En pantallas menores que 1024×768 el mínimo se limita al área de trabajo (FR-006).
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            SetEffectiveMinimum(WindowPlacementRules.EffectiveMinimum(screen.WorkingArea, screen.Scaling));
        }

        UpdateContentSize();
    }

    private void SetEffectiveMinimum(Size minimum)
    {
        MinWidth = minimum.Width;
        MinHeight = minimum.Height;
    }

    private void OnWindowStateChanged()
    {
        if (WindowState is WindowState.Maximized or WindowState.FullScreen)
        {
            _lastState = WindowPlacement.Maximized;
        }
        else if (WindowState == WindowState.Normal)
        {
            _lastState = WindowPlacement.Normal;
        }
    }

    private void OnClientSizeChanged()
    {
        if (WindowState == WindowState.Normal && IsVisible)
        {
            _normalSize = ClientSize;
        }

        UpdateContentSize();
    }

    // El contenido mide al menos el mínimo: la barra global solo aparece por debajo de él y, con un
    // tamaño finito, los desplazamientos de cada pantalla siguen funcionando.
    private void UpdateContentSize()
    {
        RootContent.Width = Math.Max(ClientSize.Width, MinWidth);
        RootContent.Height = Math.Max(ClientSize.Height, MinHeight);
    }
}
