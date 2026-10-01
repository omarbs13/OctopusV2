using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Customers;
using Pos.Application.Customers.FindCustomersForSale;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Sales;

/// <summary>Fila del buscador de clientes del punto de venta.</summary>
public sealed record CustomerPickerRow(CustomerForSaleDto Item)
{
    public string Name => Item.Name;

    public string Detail => string.IsNullOrEmpty(Item.TaxId) ? Item.Phone : $"{Item.Phone} · {Item.TaxId}";
}

/// <summary>
/// Buscador de clientes para la venta a crédito (014, contracts/ui.md "Punto de venta y cobro"): busca
/// mientras se escribe con <c>FindCustomersForSale</c>, que solo devuelve clientes activos con crédito.
/// </summary>
public sealed partial class CustomerPickerViewModel : ViewModelBase, IDisposable
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Action<CustomerForSaleDto> _onChosen;
    private readonly Action _onClosed;

    private CancellationTokenSource? _pendingSearch;
    private int _searchVersion;

    public CustomerPickerViewModel(UseCases useCases, OperationRunner runner, Action<CustomerForSaleDto> onChosen, Action onClosed)
    {
        _useCases = useCases;
        _runner = runner;
        _onChosen = onChosen;
        _onClosed = onClosed;
    }

    public ObservableCollection<CustomerPickerRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CustomerPickerRow? SelectedRow { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>Primera búsqueda al abrir: los primeros clientes con crédito.</summary>
    public Task LoadAsync() => SearchAsync();

    public void Dispose()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
    }

    partial void OnSearchTextChanged(string value) => _ = SearchAfterDelayAsync();

    [RelayCommand]
    private void Choose()
    {
        if (SelectedRow is { } row)
        {
            _onChosen(row.Item);
        }
    }

    [RelayCommand]
    private void Close() => _onClosed();

    private async Task SearchAfterDelayAsync()
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
        var pending = _pendingSearch = new CancellationTokenSource();
        try
        {
            await Task.Delay(Customers.CustomerListViewModel.SearchDelay, pending.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var query = new FindCustomersForSaleQuery(SearchText);
        var (completed, result) = await _runner.RunAsync(
            "BuscarClienteParaVender",
            () => _useCases.RunAsync<FindCustomersForSaleHandler, Result<IReadOnlyList<CustomerForSaleDto>>>(h => h.HandleAsync(query, CancellationToken.None)));
        if (!completed || result is null || version != _searchVersion)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error is ModuleNotLicensed ? Strings.License_ModuleNotLicensed : Strings.Common_Forbidden;
            return;
        }

        ErrorMessage = null;
        Rows.Clear();
        foreach (var item in result.Value)
        {
            Rows.Add(new CustomerPickerRow(item));
        }

        IsEmpty = Rows.Count == 0;
        SelectedRow = Rows.Count > 0 ? Rows[0] : null;
    }
}
