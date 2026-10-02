using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Inventory;
using Pos.Application.Inventory.SearchMovements;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Navigation;
using Pos.Desktop.Purchases;
using Pos.Desktop.Resources;
using Pos.Domain.Inventory;
using Pos.Domain.Users;

namespace Pos.Desktop.Inventory;

/// <summary>
/// Fila del historial con los textos ya formateados. Los movimientos son inmutables; la única acción es abrir la
/// compra que generó o revirtió el movimiento (020), si <paramref name="CanOpenPurchase"/>.
/// </summary>
public sealed record MovementRow(MovementDto Item, bool CanOpenPurchase = false)
{
    public string DateText => Item.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string ProductText => $"{Item.ProductName} ({Item.ProductSku})";

    public string TypeText => MovementTypeLabels.Of(Item.Type);

    public string QuantityText =>
        (Item.Type.IsIncrease() ? "+" : "−") + QuantityConverter.Format(Item.QuantityThousandths, Item.DecimalPlaces);

    public string ResultingText => QuantityConverter.Format(Item.ResultingStockThousandths, Item.DecimalPlaces);

    public string UnitName => Item.UnitName;

    public string Reason => Item.Reason ?? string.Empty;

    /// <summary>"Factura {número} · {proveedor}" en los movimientos de compra (contracts/ui.md "Kárdex").</summary>
    public string Reference => Item.SupplierName is { } supplier
        ? string.Format(CultureInfo.CurrentCulture, Strings.Purchase_InvoiceReference, Item.Reference, supplier)
        : Item.Reference ?? string.Empty;

    public bool ShowPurchaseLink => CanOpenPurchase && Item.PurchaseId is not null;

    public bool ShowReferenceText => !ShowPurchaseLink;

    public string UserName => Item.CreatedByName;
}

/// <summary>Pantalla Movimientos (kárdex): historial filtrable por producto, tipo y fechas.</summary>
public sealed partial class MovementsViewModel : PageViewModel, INavigationArgumentReceiver, IDisposable
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<MovementEditorViewModel> _editorFactory;
    private readonly Func<PurchaseDetailViewModel>? _purchaseDetailFactory;

    private int _searchVersion;
    private bool _suppressAutoSearch;

    public MovementsViewModel(
        UseCases useCases,
        OperationRunner runner,
        Func<MovementEditorViewModel> editorFactory,
        ICurrentPermissions? permissions = null,
        Func<PurchaseDetailViewModel>? purchaseDetailFactory = null)
    {
        CanRegisterMovements = permissions?.Has(Permission.RegisterMovements) ?? true;
        CanOpenPurchases = purchaseDetailFactory is not null
            && (permissions is null || permissions.Has(Permission.ViewPurchaseReport) || permissions.Has(Permission.RegisterPurchases));
        _purchaseDetailFactory = purchaseDetailFactory;
        _useCases = useCases;
        _runner = runner;
        _editorFactory = editorFactory;
        Picker = new ProductPickerViewModel(useCases, runner, includeInactive: true);
        Picker.SelectionChanged += (_, _) => RestartSearch();

        TypeOptions =
        [
            new(null, Strings.Movements_AllTypes),
            .. Enum.GetValues<MovementType>().Select(t => new MovementTypeOption(t, MovementTypeLabels.Of(t))),
        ];
        _suppressAutoSearch = true;
        SelectedType = TypeOptions[0];
        _suppressAutoSearch = false;
    }

    public override string Title => Strings.Nav_Movements;

    /// <summary>Puede registrar movimientos de inventario; el Cajero solo consulta (FR-010).</summary>
    public bool CanRegisterMovements { get; }

    /// <summary>Puede abrir el detalle de la compra de un movimiento (020, contracts/ui.md "Kárdex").</summary>
    public bool CanOpenPurchases { get; }

    public override FormHost Forms { get; } = new();

    public ProductPickerViewModel Picker { get; }

    public IReadOnlyList<MovementTypeOption> TypeOptions { get; }

    public ObservableCollection<MovementRow> Rows { get; } = [];

    [ObservableProperty]
    public partial MovementTypeOption SelectedType { get; set; }

    [ObservableProperty]
    public partial DateTime? FromDate { get; set; }

    [ObservableProperty]
    public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Error del rango de fechas, si lo hay.</summary>
    [ObservableProperty]
    public partial string? DateRangeError { get; private set; }

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

    public override async Task OnActivatedAsync()
    {
        await Picker.LoadAsync();
        await SearchAsync();
    }

    /// <summary>
    /// Un <see cref="MovementsProductFilter"/> fija el producto, limpia tipo y fechas y vuelve a la
    /// página 1. La búsqueda la hace <see cref="OnActivatedAsync"/>, que sigue a este método.
    /// </summary>
    public void Receive(object argument)
    {
        if (argument is not MovementsProductFilter filter)
        {
            return;
        }

        _suppressAutoSearch = true;
        try
        {
            SelectedType = TypeOptions[0];
            FromDate = null;
            ToDate = null;
            CurrentPage = 1;
            Picker.Select(new ProductOption(filter.ProductId, filter.Name, string.Empty));
        }
        finally
        {
            _suppressAutoSearch = false;
        }
    }

    public void Dispose() => Picker.Dispose();

    partial void OnSelectedTypeChanged(MovementTypeOption value) => RestartSearch();

    partial void OnFromDateChanged(DateTime? value) => RestartSearch();

    partial void OnToDateChanged(DateTime? value) => RestartSearch();

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task LastPageAsync() => GoToPageAsync(TotalPages);

    [RelayCommand]
    private async Task RegisterMovementAsync()
    {
        if (!CanRegisterMovements)
        {
            return;
        }

        var editor = _editorFactory();
        await editor.InitializeWithPickerAsync();
        editor.Saved += (_, movement) => _ = OnMovementSavedAsync(movement);
        editor.Closed += (_, _) => _ = SearchAsync();
        await Forms.OpenAsync(editor, FormPresentation.SidePanel);
    }

    /// <summary>Abre el detalle de la compra que generó o revirtió el movimiento.</summary>
    [RelayCommand]
    private async Task OpenPurchaseAsync(MovementRow? row)
    {
        if (row?.Item.PurchaseId is not { } purchaseId || _purchaseDetailFactory is null || !CanOpenPurchases)
        {
            return;
        }

        var detail = _purchaseDetailFactory();
        detail.Voided += (_, _) => _ = SearchAsync();
        if (await detail.LoadAsync(purchaseId))
        {
            await Forms.OpenAsync(detail, FormPresentation.FullScreen);
        }
    }

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private void RestartSearch()
    {
        if (_suppressAutoSearch)
        {
            return;
        }

        CurrentPage = 1;
        _ = SearchAsync();
    }

    private Task GoToPageAsync(int page)
    {
        CurrentPage = page;
        return SearchAsync();
    }

    private async Task OnMovementSavedAsync(MovementDto movement)
    {
        StatusMessage = MovementEditorViewModel.RegisteredMessage(movement);
        CurrentPage = 1;
        await SearchAsync();
    }

    /// <summary>Medianoche local del día elegido, expresada en UTC.</summary>
    private static DateTime LocalMidnightToUtc(DateTime date) =>
        new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();

    private async Task SearchAsync()
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var query = new SearchMovementsQuery(
            Picker.Selected?.ProductId,
            SelectedType.Type,
            FromDate is { } from ? LocalMidnightToUtc(from) : null,
            ToDate is { } to ? LocalMidnightToUtc(to.AddDays(1)) : null,
            CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarMovimientos",
            () => _useCases.RunAsync<SearchMovementsHandler, Result<MovementPage>>(
                h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?>
            {
                ["ProductId"] = query.ProductId,
                ["Type"] = query.Type?.ToString(),
                ["Page"] = query.Page,
            });

        if (!completed || result is null || version != _searchVersion)
        {
            return;
        }

        if (result.Error is ValidationFailed validation)
        {
            DateRangeError = validation.Errors.FirstOrDefault(e => e.Field == InventoryFields.DateRange)?.Message;
            Rows.Clear();
            IsEmpty = true;
            return;
        }

        if (!result.IsSuccess)
        {
            return;
        }

        DateRangeError = null;
        Rows.Clear();
        foreach (var item in result.Value.Items)
        {
            Rows.Add(new MovementRow(item, CanOpenPurchases));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Rows.Count == 0;
    }
}
