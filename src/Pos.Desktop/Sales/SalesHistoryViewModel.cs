using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Sales.SearchSales;
using Pos.Application.Users;
using Pos.Application.Users.ListCashiers;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Sales;
using Pos.Domain.Users;

namespace Pos.Desktop.Sales;

/// <summary>Opción del filtro de estado de "Ventas realizadas".</summary>
public sealed record SaleStatusOption(SaleStatus? Status, string Label);

/// <summary>Fila del listado de ventas con los textos ya formateados.</summary>
public sealed record SaleRow(SaleListItemDto Item)
{
    public string CashierName => Item.CashierName;

    public Guid Id => Item.Id;

    public string Folio => Item.Folio;

    public string DateText => Item.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string TotalText => MoneyConverter.Format(Item.TotalCents);

    public string MethodsText => string.Join(", ", Item.Methods.Select(PaymentMethodLabels.Of));

    public bool IsCancelled => Item.Status == SaleStatus.Cancelled;

    /// <summary>Las ventas a crédito agregan "Pendiente de pago" o "Pagada" (014).</summary>
    public string StatusText => (IsCancelled ? Strings.Sales_StatusCancelled : Strings.Sales_StatusCompleted)
        + (Item.CreditStatus is { } credit && !IsCancelled ? $" · {CreditStatusLabels.Of(credit)}" : string.Empty);
}

/// <summary>Opción del filtro de cajero; el valor nulo significa "Todos" (solo quien ve todas las ventas).</summary>
public sealed record CashierOption(Guid? UserId, string Label);

/// <summary>Filtro de fechas que entrega Inicio al abrir "Ventas realizadas" (por ejemplo, hoy).</summary>
public sealed record SalesDateFilter(DateTime FromLocalDate, DateTime ToLocalDate);

/// <summary>Pantalla "Ventas realizadas": listado filtrable y paginado, con el detalle de cada venta.</summary>
public sealed partial class SalesHistoryViewModel : PageViewModel, INavigationArgumentReceiver
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<SaleDetailViewModel> _detailFactory;

    private int _searchVersion;
    private bool _suppressAutoSearch;

    public SalesHistoryViewModel(
        UseCases useCases,
        OperationRunner runner,
        Func<SaleDetailViewModel> detailFactory,
        ICurrentPermissions? permissions = null)
    {
        _useCases = useCases;
        _runner = runner;
        _detailFactory = detailFactory;
        CanFilterByCashier = permissions?.Has(Permission.ViewAllSales) ?? false;
        CashierOptions = [new CashierOption(null, Strings.Sales_CashierAll)];
        SelectedCashier = CashierOptions[0];
        StatusOptions =
        [
            new(null, Strings.Sales_StatusAll),
            new(SaleStatus.Completed, Strings.Sales_StatusCompletedPlural),
            new(SaleStatus.Cancelled, Strings.Sales_StatusCancelledPlural),
        ];
        _suppressAutoSearch = true;
        SelectedStatus = StatusOptions[0];
        _suppressAutoSearch = false;
    }

    /// <summary>El filtro por cajero solo lo ve quien puede ver todas las ventas (FR-026).</summary>
    public bool CanFilterByCashier { get; }

    public ObservableCollection<CashierOption> CashierOptions { get; }

    [ObservableProperty]
    public partial CashierOption SelectedCashier { get; set; }

    public override string Title => Strings.Nav_SalesHistory;

    public override FormHost Forms { get; } = new();

    public IReadOnlyList<SaleStatusOption> StatusOptions { get; }

    public ObservableCollection<SaleRow> Rows { get; } = [];

    [ObservableProperty]
    public partial SaleRow? SelectedRow { get; set; }

    [ObservableProperty]
    public partial SaleStatusOption SelectedStatus { get; set; }

    [ObservableProperty]
    public partial DateTime? FromDate { get; set; }

    [ObservableProperty]
    public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    public partial string FolioText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Error del folio o del rango de fechas, si lo hay.</summary>
    [ObservableProperty]
    public partial string? FilterError { get; private set; }

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
        await LoadCashiersAsync();
        await SearchAsync();
    }

    /// <summary>Un <see cref="SalesDateFilter"/> fija las fechas, limpia folio y estado y vuelve a la página 1.</summary>
    public void Receive(object argument)
    {
        if (argument is not SalesDateFilter filter)
        {
            return;
        }

        _suppressAutoSearch = true;
        try
        {
            FromDate = filter.FromLocalDate;
            ToDate = filter.ToLocalDate;
            FolioText = string.Empty;
            SelectedStatus = StatusOptions[0];
            CurrentPage = 1;
        }
        finally
        {
            _suppressAutoSearch = false;
        }
    }

    partial void OnSelectedStatusChanged(SaleStatusOption value) => RestartSearch();

    partial void OnSelectedCashierChanged(CashierOption value) => RestartSearch();

    partial void OnFromDateChanged(DateTime? value) => RestartSearch();

    partial void OnToDateChanged(DateTime? value) => RestartSearch();

    [RelayCommand]
    private void SearchNow() => RestartSearch();

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task LastPageAsync() => GoToPageAsync(TotalPages);

    /// <summary>Enter o doble clic: abre el detalle de la venta seleccionada.</summary>
    [RelayCommand]
    private async Task OpenDetailAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var detail = _detailFactory();
        detail.Closed += (_, _) => _ = SearchAsync();
        if (await detail.LoadAsync(row.Id))
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

    /// <summary>Medianoche local del día elegido, expresada en UTC.</summary>
    private static DateTime LocalMidnightToUtc(DateTime date) =>
        new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();

    /// <summary>Cajeros para el filtro (Administrador); se conserva la elección al volver a la pantalla.</summary>
    private async Task LoadCashiersAsync()
    {
        if (!CanFilterByCashier)
        {
            return;
        }

        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "ListarCajeros",
            () => _useCases.RunAsync<ListCashiersHandler, Result<IReadOnlyList<UserOption>>>(h => h.HandleAsync(CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var selected = SelectedCashier?.UserId;
        _suppressAutoSearch = true;
        try
        {
            CashierOptions.Clear();
            CashierOptions.Add(new CashierOption(null, Strings.Sales_CashierAll));
            foreach (var cashier in result.Value)
            {
                CashierOptions.Add(new CashierOption(cashier.Id, cashier.UserName));
            }

            SelectedCashier = CashierOptions.FirstOrDefault(o => o.UserId == selected) ?? CashierOptions[0];
        }
        finally
        {
            _suppressAutoSearch = false;
        }
    }

    private async Task SearchAsync()
    {
        var version = Interlocked.Increment(ref _searchVersion);

        // Desde 00:00 del primer día hasta 00:00 del día siguiente al último: [desde, hasta+1) en UTC.
        var query = new SearchSalesQuery(
            FromDate is { } from ? LocalMidnightToUtc(from) : null,
            ToDate is { } to ? LocalMidnightToUtc(to.AddDays(1)) : null,
            FolioText,
            SelectedStatus?.Status,
            CurrentPage,
            CanFilterByCashier ? SelectedCashier?.UserId : null);

        var (completed, result) = await _runner.RunAsync(
            "BuscarVentas",
            () => _useCases.RunAsync<SearchSalesHandler, Result<SalePage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Status"] = query.Status?.ToString(), ["Page"] = query.Page });

        if (!completed || result is null || version != _searchVersion)
        {
            return;
        }

        if (result.Error is ValidationFailed validation)
        {
            FilterError = validation.Errors.Count > 0 ? validation.Errors[0].Message : null;
            Rows.Clear();
            IsEmpty = true;
            return;
        }

        if (!result.IsSuccess)
        {
            return;
        }

        FilterError = null;
        Rows.Clear();
        foreach (var item in result.Value.Items)
        {
            Rows.Add(new SaleRow(item));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Rows.Count == 0;
    }
}
