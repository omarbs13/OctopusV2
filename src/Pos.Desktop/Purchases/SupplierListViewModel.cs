using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Suppliers;
using Pos.Application.Suppliers.SearchSuppliers;
using Pos.Application.Suppliers.SetSupplierActive;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Purchases;

/// <summary>Fila del listado de proveedores con los textos ya formateados.</summary>
public sealed record SupplierRow(SupplierListItemDto Item)
{
    public Guid Id => Item.Id;

    public string Name => Item.Name;

    public string TaxId => Item.TaxId ?? string.Empty;

    public string Phone => Item.Phone ?? string.Empty;

    public string TermsText => SupplierTexts.Terms(Item.PaymentTerms, Item.CreditDays);

    public bool IsActive => Item.IsActive;

    public string StatusText => Item.IsActive ? Strings.Supplier_Active : Strings.Supplier_Inactive;
}

/// <summary>
/// Proveedores (020, Historia 1): búsqueda mientras se escribe por nombre o RFC, filtro de inactivos, alta,
/// edición y activación. Solo invoca casos de uso.
/// </summary>
public sealed partial class SupplierListViewModel : PageViewModel, IDisposable
{
    /// <summary>Espera tras la última tecla antes de buscar.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly Func<SupplierFormViewModel> _formFactory;

    private CancellationTokenSource? _pendingSearch;
    private int _searchVersion;

    public SupplierListViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        Func<SupplierFormViewModel> formFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _formFactory = formFactory;
    }

    public override string Title => Strings.Supplier_ListTitle;

    public override FormHost Forms { get; } = new();

    public ObservableCollection<SupplierRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeInactive { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleActiveText))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    public partial SupplierRow? SelectedRow { get; set; }

    /// <summary>"Desactivar" con un proveedor activo seleccionado; "Activar" con uno inactivo.</summary>
    public string ToggleActiveText => SelectedRow is { IsActive: false } ? Strings.Supplier_Activate : Strings.Supplier_Deactivate;

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
        string.Format(CultureInfo.CurrentCulture, Strings.Supplier_PageSummary, TotalCount, CurrentPage, TotalPages);

    public override Task OnActivatedAsync() => SearchAsync();

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
    private async Task NewSupplierAsync()
    {
        var form = _formFactory();
        form.Saved += (_, supplierId) => _ = OnSavedAsync(supplierId);
        await Forms.OpenAsync(form, FormPresentation.SidePanel);
    }

    /// <summary>Enter o doble clic: edita el proveedor seleccionado.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var form = _formFactory();
        form.Saved += (_, supplierId) => _ = OnSavedAsync(supplierId);
        if (await form.LoadAsync(row.Id))
        {
            await Forms.OpenAsync(form, FormPresentation.SidePanel);
        }
        else
        {
            await SearchAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var command = new SetSupplierActiveCommand(row.Id, row.Item.Version, !row.IsActive);
        var (completed, result) = await _runner.RunAsync(
            "ActivarProveedor",
            () => _useCases.RunAsync<SetSupplierActiveHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["SupplierId"] = row.Id, ["Active"] = command.Active });

        if (!completed || result is null)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, result.Error switch
            {
                Forbidden => Strings.Common_Forbidden,
                ModuleNotLicensed => Strings.License_ModuleNotLicensed,
                NotFound => Strings.Supplier_NotFound,
                _ => Strings.Supplier_Conflict,
            });
        }

        await OnSavedAsync(row.Id);
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

    private async Task OnSavedAsync(Guid supplierId)
    {
        await SearchAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.Id == supplierId);
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
        var query = new SearchSuppliersQuery(SearchText, IncludeInactive, CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarProveedores",
            () => _useCases.RunAsync<SearchSuppliersHandler, Result<SupplierPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["IncludeInactive"] = query.IncludeInactive, ["Page"] = query.Page });

        if (!completed || result is not { IsSuccess: true } || version != _searchVersion)
        {
            return;
        }

        var selectedId = SelectedRow?.Id;
        Rows.Clear();
        foreach (var item in result.Value.Items)
        {
            Rows.Add(new SupplierRow(item));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Rows.Count == 0;
        SelectedRow = Rows.FirstOrDefault(r => r.Id == selectedId);
    }
}
