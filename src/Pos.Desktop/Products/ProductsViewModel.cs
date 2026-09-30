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
using Pos.Desktop.Resources;

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

    private CancellationTokenSource? _pendingSearch;
    private int _searchVersion;
    private bool _suppressAutoSearch;

    public ProductsViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        Func<ProductEditorViewModel> editorFactory)
    {
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

    public ObservableCollection<ProductListItemDto> Items { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeInactive { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial bool HasMore { get; private set; }

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
        if (!_suppressAutoSearch)
        {
            _ = SearchAfterDelayAsync();
        }
    }

    partial void OnIncludeInactiveChanged(bool value) => _ = SearchNowAsync();

    [RelayCommand]
    private Task SearchNowAsync()
    {
        CancelPendingSearch();
        return SearchAsync();
    }

    [RelayCommand]
    private Task NewProductAsync() => OpenEditorAsync(_editorFactory());

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedItem is not { } selected)
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
        if (SelectedItem is not { } selected)
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

    private bool HasSelection() => SelectedItem is not null;

    private async Task OpenEditorAsync(ProductEditorViewModel editor)
    {
        editor.Saved += (_, product) => _ = OnEditorSavedAsync(product);
        editor.Closed += (_, _) => _ = SearchAsync();
        await Forms.OpenAsync(editor, FormPresentation.SidePanel);
    }

    private async Task OnEditorSavedAsync(ProductDto product)
    {
        await SearchAsync();

        var visibleByStatus = product.IsActive || IncludeInactive;
        if (visibleByStatus && !Items.Any(i => i.Id == product.Id))
        {
            // El producto no coincide con el texto buscado: se limpia la búsqueda para mostrarlo.
            // Un producto que se marcó como inactivo sale del listado por defecto (FR-016).
            _suppressAutoSearch = true;
            SearchText = string.Empty;
            _suppressAutoSearch = false;
            await SearchNowAsync();
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

    private async Task SearchAsync()
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var query = new SearchProductsQuery(SearchText, IncludeInactive);

        var (completed, result) = await _runner.RunAsync(
            "BuscarProductos",
            () => _useCases.RunAsync<SearchProductsHandler, Result<SearchProductsResult>>(
                h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["SearchText"] = query.Text, ["IncludeInactive"] = query.IncludeInactive });

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

        HasMore = result.Value.HasMore;
        IsEmpty = Items.Count == 0;
        SelectedItem = Items.FirstOrDefault(i => i.Id == selectedId);
    }
}
