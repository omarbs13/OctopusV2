using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Pos.Desktop.Sales;

public partial class CustomerPickerView : UserControl
{
    public CustomerPickerView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchBox.Focus());
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Enter elige, Esc cierra y ↓ desde la búsqueda pasa a la lista.</summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CustomerPickerViewModel picker)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                picker.ChooseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                picker.CloseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down when SearchBox.IsFocused:
                ResultsList.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is CustomerPickerViewModel picker)
        {
            picker.ChooseCommand.Execute(null);
        }
    }
}
