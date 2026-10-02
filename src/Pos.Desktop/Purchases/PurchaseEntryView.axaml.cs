using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Pos.Desktop.Purchases;

public partial class PurchaseEntryView : UserControl
{
    private PurchaseEntryViewModel? _viewModel;

    public PurchaseEntryView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => SupplierBox.Focus();

        // Enter en el costo regresa al buscador de productos (SC-003).
        LinesControl.AddHandler(KeyDownEvent, OnLinesKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusQuantityRequested -= OnFocusQuantityRequested;
        }

        _viewModel = DataContext as PurchaseEntryViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusQuantityRequested += OnFocusQuantityRequested;
        }

        base.OnDataContextChanged(e);
    }

    private void OnLinesKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.Source is TextBox { Name: "CostBox" })
        {
            ProductSearchBox.Focus();
            e.Handled = true;
        }
    }

    /// <summary>Enfoca la cantidad de la línea y la selecciona, una vez que su fila existe.</summary>
    private void OnFocusQuantityRequested(object? sender, PurchaseLineViewModel line) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                var box = LinesControl.ContainerFromItem(line)?
                    .GetVisualDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault(t => t.Name == "QuantityBox");
                box?.Focus();
                box?.SelectAll();
            },
            DispatcherPriority.Background);
}
