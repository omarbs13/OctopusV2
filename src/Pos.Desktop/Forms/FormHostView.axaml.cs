using Avalonia;
using Avalonia.Controls;

namespace Pos.Desktop.Forms;

/// <summary>
/// Muestra el listado de una pantalla y su formulario activo como panel lateral o a pantalla
/// completa, según <see cref="FormHost.ActivePresentation"/>.
/// </summary>
public partial class FormHostView : UserControl
{
    public static readonly StyledProperty<FormHost?> HostProperty =
        AvaloniaProperty.Register<FormHostView, FormHost?>(nameof(Host));

    public static readonly StyledProperty<object?> ListContentProperty =
        AvaloniaProperty.Register<FormHostView, object?>(nameof(ListContent));

    public FormHostView()
    {
        InitializeComponent();
    }

    public FormHost? Host
    {
        get => GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    /// <summary>Contenido del listado (se oculta con un formulario a pantalla completa).</summary>
    public object? ListContent
    {
        get => GetValue(ListContentProperty);
        set => SetValue(ListContentProperty, value);
    }
}
