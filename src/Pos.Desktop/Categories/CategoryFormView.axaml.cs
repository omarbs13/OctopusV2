using Avalonia.Controls;

namespace Pos.Desktop.Categories;

public partial class CategoryFormView : UserControl
{
    public CategoryFormView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => NameBox.Focus();
    }
}
