using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Pos.Desktop.Sales;

public partial class ProductChooserView : UserControl
{
    public ProductChooserView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => ResultsList.Focus());
        AddHandler(KeyDownEvent, OnPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>Enter elige el resultado seleccionado y Esc cierra el selector.</summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ProductChooserViewModel chooser)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            chooser.ChooseCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            chooser.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ProductChooserViewModel chooser)
        {
            chooser.ChooseCommand.Execute(null);
        }
    }
}
