using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.CashShifts;

public partial class ShiftCutsView : UserControl
{
    public ShiftCutsView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    /// <summary>Enter sobre el listado abre el corte seleccionado.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && CutsList.IsKeyboardFocusWithin)
        {
            OpenSelected();
            e.Handled = true;
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (DataContext is ShiftCutsViewModel page && page.OpenDetailCommand.CanExecute(null))
        {
            page.OpenDetailCommand.Execute(null);
        }
    }
}
