using Avalonia.Controls;
using Avalonia.Input;

namespace Pos.Desktop.Discounts;

public partial class CouponListView : UserControl
{
    public CouponListView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => SearchBox.Focus();
    }

    /// <summary>Doble clic en una fila abre el cupón para editarlo.</summary>
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is CouponListViewModel page && page.EditCommand.CanExecute(null))
        {
            page.EditCommand.Execute(null);
        }
    }
}
