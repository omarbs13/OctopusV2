using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace Pos.Desktop.Sales;

/// <summary>Las ventas canceladas se muestran atenuadas.</summary>
public sealed class CancelledOpacityConverter : IValueConverter
{
    public static CancelledOpacityConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 0.55 : 1.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public partial class SalesHistoryView : UserControl
{
    public SalesHistoryView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    /// <summary>Enter sobre el listado abre el detalle de la venta seleccionada.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SalesList.IsKeyboardFocusWithin)
        {
            OpenSelected();
            e.Handled = true;
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (DataContext is SalesHistoryViewModel page && page.OpenDetailCommand.CanExecute(null))
        {
            page.OpenDetailCommand.Execute(null);
        }
    }
}
