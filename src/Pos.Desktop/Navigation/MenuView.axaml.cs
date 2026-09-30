using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Pos.Desktop.Navigation;

public partial class MenuView : UserControl
{
    /// <summary>Refleja <see cref="MenuViewModel.IsCollapsed"/> para los estilos y los elementos internos.</summary>
    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<MenuView, bool>(nameof(IsCollapsed));

    public MenuView()
    {
        InitializeComponent();
        Bind(IsCollapsedProperty, new Binding(nameof(MenuViewModel.IsCollapsed)));
    }

    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }
}
