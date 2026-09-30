using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Inventory.SearchStock;
using Pos.Desktop.Common;

namespace Pos.Desktop.Inventory;

/// <summary>Producto elegible en un selector; <see cref="Stock"/> trae su existencia si viene de una búsqueda.</summary>
public sealed record ProductOption(Guid ProductId, string Name, string Sku, StockItemDto? Stock = null)
{
    public string Label => string.IsNullOrEmpty(Sku) ? Name : $"{Name} ({Sku})";
}

/// <summary>
/// Selector de producto con búsqueda, compartido por el formulario de movimiento y el filtro de
/// Movimientos. Busca con <see cref="SearchStockHandler"/>: solo productos que controlan inventario.
/// </summary>
public sealed partial class ProductPickerViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly bool _includeInactive;

    private CancellationTokenSource? _pendingSearch;
    private bool _refreshing;

    public ProductPickerViewModel(UseCases useCases, OperationRunner runner, bool includeInactive)
    {
        _useCases = useCases;
        _runner = runner;
        _includeInactive = includeInactive;
    }

    /// <summary>La selección cambió (por el operador o por código).</summary>
    public event EventHandler? SelectionChanged;

    public ObservableCollection<ProductOption> Options { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    public partial ProductOption? Selected { get; set; }

    /// <summary>Carga las primeras opciones sin texto de búsqueda.</summary>
    public Task LoadAsync() => SearchAsync();

    /// <summary>Fija la selección por código, agregándola a las opciones si hace falta.</summary>
    public void Select(ProductOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        _refreshing = true;
        try
        {
            if (Options.All(o => o.ProductId != option.ProductId))
            {
                Options.Insert(0, option);
            }

            Selected = Options.First(o => o.ProductId == option.ProductId);
        }
        finally
        {
            _refreshing = false;
        }
    }

    public void Dispose()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
    }

    partial void OnSearchTextChanged(string value) => _ = SearchAfterDelayAsync();

    partial void OnSelectedChanged(ProductOption? value)
    {
        if (!_refreshing)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Clear() => Selected = null;

    private bool HasSelection() => Selected is not null;

    private async Task SearchAfterDelayAsync()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
        var pending = _pendingSearch = new CancellationTokenSource();
        try
        {
            await Task.Delay(SearchDelay, pending.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var text = SearchText;
        var (completed, result) = await _runner.RunAsync(
            "BuscarProductoInventario",
            () => _useCases.RunAsync<SearchStockHandler, Result<StockPage>>(
                h => h.HandleAsync(new SearchStockQuery(text, StockFilter.All, _includeInactive), CancellationToken.None)),
            new Dictionary<string, object?> { ["SearchText"] = text });
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        // La selección se conserva aunque ya no coincida con el texto buscado.
        var keep = Selected;
        _refreshing = true;
        try
        {
            Options.Clear();
            if (keep is not null)
            {
                Options.Add(keep);
            }

            foreach (var item in result.Value.Items.Where(i => keep is null || i.ProductId != keep.ProductId))
            {
                Options.Add(new ProductOption(item.ProductId, item.Name, item.Sku, item));
            }

            Selected = keep;
        }
        finally
        {
            _refreshing = false;
        }
    }
}
