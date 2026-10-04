using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Customers;
using Pos.Application.Customers.SearchCustomers;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Navigation;
using Pos.Desktop.Resources;
using Pos.Domain.Customers;

namespace Pos.Desktop.Customers;

/// <summary>Fila del listado de clientes con los textos ya formateados.</summary>
public sealed record CustomerRow(CustomerListItemDto Item)
{
    public Guid Id => Item.Id;

    public string Name => Item.Name;

    public string Phone => Item.Phone;

    public string TaxId => Item.TaxId ?? string.Empty;

    public string ModeText => CustomerTexts.Mode(Item.CreditMode);

    public string LimitText => MoneyConverter.Format(Item.LimitCents);

    public string BalanceText => MoneyConverter.Format(Item.BalanceCents);

    public bool IsActive => Item.IsActive;

    public string StatusText => Item.IsActive ? Strings.Customer_Active : Strings.Customer_Inactive;
}

/// <summary>Textos comunes de las pantallas de clientes.</summary>
public static class CustomerTexts
{
    public static string Mode(CreditMode mode) => mode == CreditMode.Credit ? Strings.Customer_ModeCredit : Strings.Customer_ModeCashOnly;
}

/// <summary>
/// Clientes (014, Historia 1): búsqueda mientras se escribe por nombre, teléfono o RFC, filtro de
/// inactivos, alta y ficha. Solo invoca <c>SearchCustomers</c>; el saldo viene del caso de uso.
/// Acepta un id de cliente como argumento de navegación para abrir su ficha.
/// </summary>
public sealed partial class CustomerListViewModel : PageViewModel, INavigationArgumentReceiver, IDisposable
{
    /// <summary>Espera tras la última tecla antes de buscar.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<CustomerFormViewModel> _formFactory;
    private readonly Func<CustomerDetailViewModel> _detailFactory;

    private CancellationTokenSource? _pendingSearch;
    private int _searchVersion;
    private Guid? _pendingOpen;

    public CustomerListViewModel(
        UseCases useCases,
        OperationRunner runner,
        Func<CustomerFormViewModel> formFactory,
        Func<CustomerDetailViewModel> detailFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _formFactory = formFactory;
        _detailFactory = detailFactory;
    }

    public override string Title => Strings.Customer_ListTitle;

    public override FormHost Forms { get; } = new();

    public ObservableCollection<CustomerRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeInactive { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDetailCommand))]
    public partial CustomerRow? SelectedRow { get; set; }

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
        string.Format(CultureInfo.CurrentCulture, Strings.Customer_PageSummary, TotalCount, CurrentPage, TotalPages);

    public override async Task OnActivatedAsync()
    {
        await SearchAsync();
        if (_pendingOpen is { } customerId)
        {
            _pendingOpen = null;
            await OpenCustomerAsync(customerId);
        }
    }

    /// <summary>Un <see cref="Guid"/> abre la ficha de ese cliente al activarse la pantalla.</summary>
    public void Receive(object argument)
    {
        if (argument is Guid customerId)
        {
            _pendingOpen = customerId;
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
        _ = SearchAfterDelayAsync();
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
    private async Task NewCustomerAsync()
    {
        var form = _formFactory();
        form.Saved += (_, customerId) => _ = OnSavedAsync(customerId);
        await Forms.OpenAsync(form, FormPresentation.SidePanel);
    }

    /// <summary>Enter o doble clic: abre la ficha del cliente seleccionado.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync() => SelectedRow is { } row ? OpenCustomerAsync(row.Id) : Task.CompletedTask;

    private async Task OpenCustomerAsync(Guid customerId)
    {
        var detail = _detailFactory();
        detail.Closed += (_, _) => _ = SearchAsync();
        if (await detail.LoadAsync(customerId))
        {
            await Forms.OpenAsync(detail, FormPresentation.FullScreen);
        }
    }

    private bool HasSelection() => SelectedRow is not null;

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private Task GoToPageAsync(int page)
    {
        CancelPendingSearch();
        CurrentPage = page;
        return SearchAsync();
    }

    private async Task OnSavedAsync(Guid customerId)
    {
        await SearchAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.Id == customerId);
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
        var query = new SearchCustomersQuery(SearchText, IncludeInactive, CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarClientes",
            () => _useCases.RunAsync<SearchCustomersHandler, Result<CustomerPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["IncludeInactive"] = query.IncludeInactive, ["Page"] = query.Page });

        if (!completed || result is not { IsSuccess: true } || version != _searchVersion)
        {
            return;
        }

        var selectedId = SelectedRow?.Id;
        Rows.Clear();
        foreach (var item in result.Value.Items)
        {
            Rows.Add(new CustomerRow(item));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Rows.Count == 0;
        SelectedRow = Rows.FirstOrDefault(r => r.Id == selectedId);
    }
}
