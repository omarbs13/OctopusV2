using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Administration;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => SearchBox.Focus();
    }

    /// <summary>Doble clic en una fila abre el editor.</summary>
    private void OnUserDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is UsersViewModel page && page.EditCommand.CanExecute(null))
        {
            page.EditCommand.Execute(null);
        }
    }
}
