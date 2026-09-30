using Avalonia.Controls;

namespace Pos.Desktop.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // El menú se contrae solo en ventanas angostas (FR-018).
        PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty && DataContext is MainViewModel main)
            {
                main.Menu.SetWindowWidth(Bounds.Width);
            }
        };
    }

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
}
