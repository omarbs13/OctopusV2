using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.CashShifts;

public partial class ShiftsView : UserControl
{
    public ShiftsView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    /// <summary>Enter sobre el listado abre el detalle del turno seleccionado.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ShiftsList.IsKeyboardFocusWithin)
        {
            OpenSelected();
            e.Handled = true;
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (DataContext is ShiftsViewModel page && page.OpenDetailCommand.CanExecute(null))
        {
            page.OpenDetailCommand.Execute(null);
        }
    }
}
