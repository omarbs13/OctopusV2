using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Inventory.SearchStock;
using Pos.Application.Reports.SetProductCritical;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Inventory;
using Pos.Domain.Users;

namespace Pos.Desktop.Inventory;

/// <summary>Opción del filtro de estado de Existencias.</summary>
public sealed record StockFilterOption(StockFilter Filter, string Label);

/// <summary>Fila de Existencias con los textos ya formateados.</summary>
public sealed record StockRow(StockItemDto Item)
{
    public Guid ProductId => Item.ProductId;

    public string Name => Item.Name;

    public string Sku => Item.Sku;

    public string UnitName => Item.UnitName;

    public bool IsActive => Item.IsActive;

    public bool IsCritical => Item.IsCritical;

    public StockStatus Status => Item.Status;

    public string OnHandText => QuantityConverter.Format(Item.OnHandThousandths, Item.DecimalPlaces);

    public string MinimumText => QuantityConverter.FormatOrDash(Item.MinimumThousandths, Item.DecimalPlaces);

    public string StatusText => Item.Status switch
    {
        StockStatus.Out => Strings.Stock_StatusOut,
        StockStatus.Low => Strings.Stock_StatusLow,
        _ => Strings.Stock_StatusNormal,
    };
}

/// <summary>Pantalla Existencias: listado filtrable y paginado, con el formulario de movimiento.</summary>
public sealed partial class StockViewModel : PageViewModel, INavigationArgumentReceiver, IDisposable
{
    /// <summary>Espera tras la última tecla antes de buscar.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<MovementEditorViewModel> _editorFactory;

    private CancellationTokenSource? _pendingSearch;
    private int _searchVersion;
    private bool _suppressAutoSearch;

    public StockViewModel(
        UseCases useCases,
        OperationRunner runner,
        Func<MovementEditorViewModel> editorFactory,
        ICurrentPermissions? permissions = null)
    {
        CanRegisterMovements = permissions?.Has(Permission.RegisterMovements) ?? true;
        CanManageProducts = permissions?.Has(Permission.ManageProducts) ?? true;
        _useCases = useCases;
        _runner = runner;
        _editorFactory = editorFactory;
        FilterOptions =
        [
            new(StockFilter.All, Strings.Stock_FilterAll),
            new(StockFilter.Normal, Strings.Stock_FilterNormal),
            new(StockFilter.Low, Strings.Stock_FilterLow),
            new(StockFilter.Out, Strings.Stock_FilterOut),
        ];
        _suppressAutoSearch = true;
        SelectedFilter = FilterOptions[0];
        _suppressAutoSearch = false;
    }

    public override string Title => Strings.Nav_Stock;

    /// <summary>Puede registrar movimientos de inventario; el Cajero solo consulta (FR-010).</summary>
    public bool CanRegisterMovements { get; }

    /// <summary>Puede marcar productos como críticos (009); requiere <c>ManageProducts</c>.</summary>
    public bool CanManageProducts { get; }

    public IReadOnlyList<StockFilterOption> FilterOptions { get; }

    public ObservableCollection<StockRow> Rows { get; } = [];

    public override FormHost Forms { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial StockFilterOption SelectedFilter { get; set; }

    [ObservableProperty]
    public partial bool IncludeInactive { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string EmptyMessage { get; private set; } = Strings.Stock_EmptyNoMatch;

    /// <summary>Confirmación del último movimiento registrado.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Products_PageSummary, TotalCount, CurrentPage, TotalPages);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterMovementCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleCriticalCommand))]
    [NotifyPropertyChangedFor(nameof(CriticalActionText))]
    public partial StockRow? SelectedRow { get; set; }

    /// <summary>Texto de la acción sobre el producto seleccionado: marcar o quitar la marca de crítico.</summary>
    public string CriticalActionText => SelectedRow is { IsCritical: true } ? Strings.Stock_UnmarkCritical : Strings.Stock_MarkCritical;

    public override Task OnActivatedAsync() => SearchAsync();

    /// <summary>
    /// Un <see cref="StockFilter"/> fija el filtro, limpia la búsqueda, desmarca inactivos y vuelve a
    /// la página 1. La búsqueda la hace <see cref="OnActivatedAsync"/>, que sigue a este método.
    /// </summary>
    public void Receive(object argument)
    {
        if (argument is not StockFilter filter)
        {
            return;
        }

        CancelPendingSearch();
        _suppressAutoSearch = true;
        try
        {
            SearchText = string.Empty;
            IncludeInactive = false;
            SelectedFilter = FilterOptions.First(o => o.Filter == filter);
            CurrentPage = 1;
        }
        finally
        {
            _suppressAutoSearch = false;
        }
    }

    public void Dispose()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
    }

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        if (!_suppressAutoSearch)
        {
            _ = SearchAfterDelayAsync();
        }
    }

    partial void OnSelectedFilterChanged(StockFilterOption value) => RestartSearch();

    partial void OnIncludeInactiveChanged(bool value) => RestartSearch();

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

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RegisterMovementAsync()
    {
        if (!CanRegisterMovements || SelectedRow is not { } row)
        {
            return;
        }

        var editor = _editorFactory();
        editor.InitializeFixed(row.Item);
        editor.Saved += (_, movement) => _ = OnMovementSavedAsync(movement);
        editor.Closed += (_, _) => _ = SearchAsync();
        await Forms.OpenAsync(editor, FormPresentation.SidePanel);
    }

    /// <summary>Marca o quita la marca de crítico del producto seleccionado (009, Historia 7).</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleCriticalAsync()
    {
        if (!CanManageProducts || SelectedRow is not { } row)
        {
            return;
        }

        var mark = !row.IsCritical;
        var (completed, result) = await _runner.RunAsync(
            "MarcarProductoCritico",
            () => _useCases.RunAsync<SetProductCriticalHandler, Result>(
                h => h.HandleAsync(new SetProductCriticalCommand(row.ProductId, mark), CancellationToken.None)),
            new Dictionary<string, object?> { ["ProductId"] = row.ProductId, ["IsCritical"] = mark });
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        StatusMessage = mark ? Strings.Stock_CriticalMarked : Strings.Stock_CriticalUnmarked;
        await SearchAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.ProductId == row.ProductId);
    }

    private bool HasSelection() => SelectedRow is not null;

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private void RestartSearch()
    {
        if (_suppressAutoSearch)
        {
            return;
        }

        CurrentPage = 1;
        _ = SearchNowAsync();
    }

    private Task GoToPageAsync(int page)
    {
        CancelPendingSearch();
        CurrentPage = page;
        return SearchAsync();
    }

    private async Task OnMovementSavedAsync(MovementDto movement)
    {
        StatusMessage = MovementEditorViewModel.RegisteredMessage(movement);
        await SearchAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.ProductId == movement.ProductId);
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
        var query = new SearchStockQuery(SearchText, SelectedFilter.Filter, IncludeInactive, CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarExistencias",
            () => _useCases.RunAsync<SearchStockHandler, Result<StockPage>>(
                h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?>
            {
                ["SearchText"] = query.Text,
                ["Filter"] = query.Filter.ToString(),
                ["IncludeInactive"] = query.IncludeInactive,
                ["Page"] = query.Page,
            });

        if (!completed || result is not { IsSuccess: true } || version != _searchVersion)
        {
            return;
        }

        var selectedId = SelectedRow?.ProductId;
        Rows.Clear();
        foreach (var item in result.Value.Items)
        {
            Rows.Add(new StockRow(item));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Rows.Count == 0;

        // Sin ningún criterio activo, un listado vacío significa que ningún producto controla inventario.
        var noCriteria = string.IsNullOrWhiteSpace(SearchText) && SelectedFilter.Filter == StockFilter.All && !IncludeInactive;
        EmptyMessage = noCriteria ? Strings.Stock_EmptyNone : Strings.Stock_EmptyNoMatch;
        SelectedRow = Rows.FirstOrDefault(r => r.ProductId == selectedId);
    }
}
