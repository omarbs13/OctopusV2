using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Products.DeleteProduct;
using Pos.Application.Products.SearchProducts;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Inventory;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Products;

/// <summary>Pantalla de Productos: listado, búsqueda y apertura del editor.</summary>
public sealed partial class ProductsViewModel : PageViewModel, IDisposable
{
    /// <summary>Espera tras la última tecla antes de buscar.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly Func<ProductEditorViewModel> _editorFactory;
    private readonly Navigator? _navigator;

    private CancellationTokenSource? _pendingSearch;
    private int _searchVersion;
    private bool _suppressAutoSearch;

    public ProductsViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        Func<ProductEditorViewModel> editorFactory,
        Navigator? navigator = null,
        ICurrentPermissions? permissions = null)
    {
        _navigator = navigator;
        CanManage = permissions?.Has(Permission.ManageProducts) ?? true;
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _editorFactory = editorFactory;
        Forms.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FormHost.ActiveForm))
            {
                OnPropertyChanged(nameof(Editor));
            }
        };
    }

    public override string Title => Strings.Shell_NavProducts;

    /// <summary>Puede crear, editar y borrar productos; el Cajero solo consulta (FR-010).</summary>
    public bool CanManage { get; }

    public ObservableCollection<ProductListItemDto> Items { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeInactive { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Página mostrada, base 1 (FR-007).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    /// <summary>Productos que cumplen la búsqueda y el filtro, en todas las páginas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    /// <summary>Por ejemplo "250 registros · Página 2 de 3".</summary>
    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Products_PageSummary, TotalCount, CurrentPage, TotalPages);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial ProductListItemDto? SelectedItem { get; set; }

    /// <summary>Formulario de alta y edición: panel lateral sobre el listado (FR-027).</summary>
    public override FormHost Forms { get; } = new();

    /// <summary>Editor abierto, si hay.</summary>
    public ProductEditorViewModel? Editor => Forms.ActiveForm as ProductEditorViewModel;

    public override Task OnActivatedAsync() => SearchAsync();

    public void Dispose()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
    }

    partial void OnSearchTextChanged(string value)
    {
        // Una búsqueda nueva siempre empieza en la primera página (FR-009).
        CurrentPage = 1;
        if (!_suppressAutoSearch)
        {
            _ = SearchAfterDelayAsync();
        }
    }

    partial void OnIncludeInactiveChanged(bool value)
    {
        CurrentPage = 1;
        _ = SearchNowAsync();
    }

    [RelayCommand]
    private Task SearchNowAsync()
    {
        CancelPendingSearch();
        return SearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task LastPageAsync() => GoToPageAsync(TotalPages);

    [RelayCommand]
    private Task NewProductAsync() => CanManage ? OpenEditorAsync(_editorFactory()) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (!CanManage || SelectedItem is not { } selected)
        {
            return;
        }

        var editor = _editorFactory();
        if (await editor.LoadAsync(selected.Id))
        {
            await OpenEditorAsync(editor);
        }
        else
        {
            await SearchAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (!CanManage || SelectedItem is not { } selected)
        {
            return;
        }

        var question = string.Format(CultureInfo.CurrentCulture, Strings.Products_DeleteConfirm, selected.Name, selected.Sku);
        if (!await _dialogs.ConfirmAsync(Strings.Products_DeleteTitle, question, Strings.Products_Delete))
        {
            return;
        }

        var (completed, result) = await _runner.RunAsync(
            "BorrarProducto",
            () => _useCases.RunAsync<DeleteProductHandler, Result>(
                h => h.HandleAsync(new DeleteProductCommand(selected.Id, selected.Version), CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = selected.Id, ["Sku"] = selected.Sku });

        if (completed && result is { IsSuccess: false })
        {
            var message = result.Error is NotFound ? Strings.Editor_NotFound : Strings.Products_DeleteConflict;
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, message);
        }

        await SearchAsync();
    }

    /// <summary>Abre Movimientos ya filtrado por el producto de la fila (FR-019).</summary>
    [RelayCommand]
    private async Task ViewMovementsAsync(ProductListItemDto? item)
    {
        if (item is { TracksInventory: true } && _navigator is not null)
        {
            await _navigator.NavigateAsync(InventoryModule.MovementsPageId, new MovementsProductFilter(item.Id, item.Name));
        }
    }

    private bool HasSelection() => SelectedItem is not null;

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private Task GoToPageAsync(int page)
    {
        CancelPendingSearch();
        CurrentPage = page;
        return SearchAsync();
    }

    private async Task OpenEditorAsync(ProductEditorViewModel editor)
    {
        editor.Saved += (_, product) => _ = OnEditorSavedAsync(product);
        editor.Closed += (_, _) => _ = SearchAsync();
        await Forms.OpenAsync(editor, FormPresentation.SidePanel);
    }

    private async Task OnEditorSavedAsync(ProductDto product)
    {
        // Se muestra la página donde quedó el producto guardado (003, FR-012).
        await SearchAsync(product.Id);

        var visibleByStatus = product.IsActive || IncludeInactive;
        if (visibleByStatus && !Items.Any(i => i.Id == product.Id))
        {
            // El producto no coincide con el texto buscado: se limpia la búsqueda para mostrarlo.
            // Un producto que se marcó como inactivo sale del listado por defecto (FR-016 de 001).
            _suppressAutoSearch = true;
            SearchText = string.Empty;
            _suppressAutoSearch = false;
            CancelPendingSearch();
            await SearchAsync(product.Id);
        }

        SelectedItem = Items.FirstOrDefault(i => i.Id == product.Id);
    }

    private async Task SearchAfterDelayAsync()
    {
        CancelPendingSearch();
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

    private void CancelPendingSearch()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
        _pendingSearch = null;
    }

    private async Task SearchAsync(Guid? locateProductId = null)
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var query = new SearchProductsQuery(SearchText, IncludeInactive, CurrentPage, locateProductId);

        var (completed, result) = await _runner.RunAsync(
            "BuscarProductos",
            () => _useCases.RunAsync<SearchProductsHandler, Result<ProductPage>>(
                h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?>
            {
                ["SearchText"] = query.Text,
                ["IncludeInactive"] = query.IncludeInactive,
                ["Page"] = query.Page,
            });

        // Se descartan resultados de búsquedas que ya fueron reemplazadas por otra más reciente.
        if (!completed || result is not { IsSuccess: true } || version != _searchVersion)
        {
            return;
        }

        var selectedId = SelectedItem?.Id;
        Items.Clear();
        foreach (var item in result.Value.Items)
        {
            Items.Add(item);
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Items.Count == 0;
        SelectedItem = Items.FirstOrDefault(i => i.Id == selectedId);
    }
}
