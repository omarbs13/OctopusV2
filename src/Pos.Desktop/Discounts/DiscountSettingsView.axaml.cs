using Avalonia.Controls;

namespace Pos.Desktop.Discounts;

public partial class DiscountSettingsView : UserControl
{
    public DiscountSettingsView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => LimitBox.Focus();
    }
}
