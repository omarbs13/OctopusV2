using Avalonia.Controls;

namespace Pos.Desktop.Categories;

/// <summary>Vista del selector de categoría; su contexto es un <see cref="CategoryPickerViewModel"/>.</summary>
public partial class CategoryPicker : UserControl
{
    public CategoryPicker() => InitializeComponent();

    /// <summary>Lleva el foco a la lista de opciones (por ejemplo, ante un error en el campo).</summary>
    public void FocusSelector() => Selector.Focus();
}
