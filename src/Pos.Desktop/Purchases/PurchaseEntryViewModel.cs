using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Purchases;
using Pos.Application.Purchases.CalculatePurchaseTotals;
using Pos.Application.Purchases.RegisterPurchase;
using Pos.Application.Suppliers;
using Pos.Application.Suppliers.ListSuppliersForPurchase;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Inventory;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Purchases;

/// <summary>Opción del selector de proveedor de la compra.</summary>
public sealed record SupplierChoice(SupplierOption Supplier)
{
    public Guid Id => Supplier.Id;

    public string Label => string.IsNullOrEmpty(Supplier.TaxId) ? Supplier.Name : $"{Supplier.Name} ({Supplier.TaxId})";
}

/// <summary>
/// "Inventario > Entrada de mercancía" (020, Historia 2): encabezado, líneas y totales. Los importes vienen
/// solo de <c>CalculatePurchaseTotals</c> en cada cambio y la compra se guarda con <c>RegisterPurchase</c>; el
/// ViewModel no suma (Principio III). Ante una falla inesperada la captura se conserva (escenario 12).
/// </summary>
public sealed partial class PurchaseEntryViewModel : PageViewModel, IDisposable
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly Func<PurchaseDetailViewModel> _detailFactory;

    private CancellationTokenSource? _pendingSupplierSearch;
    private int _totalsVersion;
    private bool _addingProduct;

    public PurchaseEntryViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        Func<PurchaseDetailViewModel> detailFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _detailFactory = detailFactory;
        Picker = new ProductPickerViewModel(useCases, runner, includeInactive: false);
        Picker.SelectionChanged += (_, _) => OnProductPicked();
        Lines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoLines));
        InvoiceDate = DateTime.Today;
    }

    /// <summary>Pide a la vista enfocar la cantidad de una línea (al agregarla o si ya estaba, escenario 10).</summary>
    public event EventHandler<PurchaseLineViewModel>? FocusQuantityRequested;

    public override string Title => Strings.Purchase_EntryTitle;

    public override FormHost Forms { get; } = new();

    public ProductPickerViewModel Picker { get; }

    public ObservableCollection<SupplierChoice> SupplierOptions { get; } = [];

    public ObservableCollection<PurchaseLineViewModel> Lines { get; } = [];

    public bool HasNoLines => Lines.Count == 0;

    /// <summary>Último día elegible: no se permiten fechas futuras.</summary>
    public DateTime MaxInvoiceDate { get; } = DateTime.Today;

    [ObservableProperty]
    public partial string SupplierSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SupplierChoice? SelectedSupplier { get; set; }

    [ObservableProperty]
    public partial string InvoiceNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime? InvoiceDate { get; set; }

    [ObservableProperty]
    public partial string TaxText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SubtotalText { get; private set; } = MoneyConverter.Format(0);

    [ObservableProperty]
    public partial string TotalText { get; private set; } = MoneyConverter.Format(0);

    [ObservableProperty]
    public partial string? SupplierError { get; private set; }

    [ObservableProperty]
    public partial string? InvoiceError { get; private set; }

    [ObservableProperty]
    public partial string? DateError { get; private set; }

    [ObservableProperty]
    public partial string? TaxError { get; private set; }

    [ObservableProperty]
    public partial string? LinesError { get; private set; }

    [ObservableProperty]
    public partial string? TotalsError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    /// <summary>Compra existente con la misma factura, para "Ver compra".</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ViewDuplicateCommand))]
    public partial Guid? DuplicatePurchaseId { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    public partial bool IsSaving { get; private set; }

    public override async Task OnActivatedAsync()
    {
        await SearchSuppliersAsync();
        await Picker.LoadAsync();
    }

    public override async Task<bool> CanLeaveAsync()
    {
        if (!await base.CanLeaveAsync())
        {
            return false;
        }

        return Lines.Count == 0 || await _dialogs.ConfirmAsync(Strings.Purchase_DiscardTitle, Strings.Purchase_DiscardConfirm, Strings.Purchase_Discard);
    }

    public void Dispose()
    {
        _pendingSupplierSearch?.Cancel();
        _pendingSupplierSearch?.Dispose();
        Picker.Dispose();
    }

    partial void OnSupplierSearchTextChanged(string value) => _ = SearchSuppliersAfterDelayAsync();

    partial void OnTaxTextChanged(string value) => _ = RecalculateAsync();

    /// <summary>Enter en el buscador: agrega el primer producto encontrado.</summary>
    [RelayCommand]
    private void AddFirstMatch()
    {
        if (Picker.Options.FirstOrDefault() is { } option)
        {
            AddProduct(option);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        ClearErrors();
        IsSaving = true;
        try
        {
            var supplier = SelectedSupplier;
            var command = new RegisterPurchaseCommand(
                supplier?.Id ?? Guid.Empty,
                InvoiceNumber,
                InvoiceDate is { } date ? DateOnly.FromDateTime(date) : null,
                [.. Lines.Select(l => new PurchaseLineInput(l.ProductId, l.QuantityText, l.UnitCostText))],
                TaxText);
            var (completed, result) = await _runner.RunAsync(
                "RegistrarCompra",
                () => _useCases.RunAsync<RegisterPurchaseHandler, Result<PurchaseRegisteredDto>>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["SupplierId"] = command.SupplierId, ["Lines"] = command.Lines.Count });

            if (!completed || result is null)
            {
                // La captura se conserva (escenario 12); el runner ya registró la falla.
                ErrorMessage = Strings.Purchase_UnexpectedError;
                return;
            }

            if (result.IsSuccess)
            {
                StatusMessage = string.Format(
                    MoneyConverter.Culture,
                    Strings.Purchase_Registered,
                    supplier?.Supplier.Name,
                    InvoiceNumber.Trim(),
                    MoneyConverter.Format(result.Value.TotalCents));
                Reset();
                return;
            }

            ShowError(result.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task DiscardAsync()
    {
        if (Lines.Count > 0
            && !await _dialogs.ConfirmAsync(Strings.Purchase_DiscardTitle, Strings.Purchase_DiscardConfirm, Strings.Purchase_Discard))
        {
            return;
        }

        StatusMessage = null;
        Reset();
    }

    [RelayCommand(CanExecute = nameof(HasDuplicate))]
    private async Task ViewDuplicateAsync()
    {
        if (DuplicatePurchaseId is not { } id)
        {
            return;
        }

        var detail = _detailFactory();
        if (await detail.LoadAsync(id))
        {
            await Forms.OpenAsync(detail, FormPresentation.FullScreen);
        }
    }

    private bool CanRegister() => !IsSaving;

    private bool HasDuplicate() => DuplicatePurchaseId is not null;

    private void OnProductPicked()
    {
        if (_addingProduct || Picker.Selected is not { } option)
        {
            return;
        }

        AddProduct(option);
    }

    private void AddProduct(ProductOption option)
    {
        _addingProduct = true;
        try
        {
            var existing = Lines.FirstOrDefault(l => l.ProductId == option.ProductId);
            if (existing is null)
            {
                existing = new PurchaseLineViewModel(
                    option.ProductId,
                    option.Name,
                    option.Sku,
                    option.Stock?.UnitName ?? string.Empty,
                    option.Stock?.DecimalPlaces ?? 3,
                    OnLineChanged,
                    RemoveLine);
                Lines.Add(existing);
                Renumber();
                _ = RecalculateAsync();
            }

            Picker.Selected = null;
            Picker.SearchText = string.Empty;
            FocusQuantityRequested?.Invoke(this, existing);
        }
        finally
        {
            _addingProduct = false;
        }
    }

    private void OnLineChanged(PurchaseLineViewModel line) => _ = RecalculateAsync();

    private void RemoveLine(PurchaseLineViewModel line)
    {
        Lines.Remove(line);
        Renumber();
        _ = RecalculateAsync();
    }

    private void Renumber()
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            Lines[i].Number = i + 1;
        }
    }

    /// <summary>Totales en vivo: solo los del caso de uso, descartando respuestas viejas.</summary>
    private async Task RecalculateAsync()
    {
        var version = Interlocked.Increment(ref _totalsVersion);
        var snapshot = Lines.ToList();
        var query = new CalculatePurchaseTotalsQuery(
            [.. snapshot.Select(l => new PurchaseTotalsLine(l.ProductId, l.DecimalPlaces, l.UnitName, l.QuantityText, l.UnitCostText))],
            TaxText);
        var (completed, result) = await _runner.RunAsync(
            "CalcularCompra",
            () => _useCases.RunAsync<CalculatePurchaseTotalsHandler, Result<PurchaseTotalsDto>>(h => h.HandleAsync(query, CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true } || version != _totalsVersion)
        {
            return;
        }

        var totals = result.Value;
        for (var i = 0; i < snapshot.Count; i++)
        {
            snapshot[i].ApplyTotals(totals.Lines[i].AmountCents, totals.Lines[i].IsBonus);
        }

        SubtotalText = MoneyConverter.Format(totals.SubtotalCents);
        TotalText = MoneyConverter.Format(totals.TotalCents);
    }

    private void ShowError(Error error)
    {
        switch (error)
        {
            case ValidationFailed validation:
                foreach (var field in validation.Errors)
                {
                    SetError(field.Field, field.Message);
                }

                break;

            case DuplicateInvoice duplicate:
                DuplicatePurchaseId = duplicate.PurchaseId;
                InvoiceError = string.Format(
                    MoneyConverter.Culture,
                    Strings.Purchase_DuplicateInvoice,
                    InvoiceNumber.Trim(),
                    SelectedSupplier?.Supplier.Name,
                    duplicate.InvoiceDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                break;

            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;

            case ModuleNotLicensed:
                ErrorMessage = Strings.License_ModuleNotLicensed;
                break;

            default:
                ErrorMessage = Strings.Purchase_UnexpectedError;
                break;
        }
    }

    private void SetError(string field, string message)
    {
        if (LineField().Match(field) is { Success: true } match)
        {
            var index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
            if (index < Lines.Count)
            {
                var line = Lines[index];
                switch (match.Groups["field"].Value)
                {
                    case "Product":
                        line.ProductError = message;
                        break;
                    case "Quantity":
                        line.QuantityError = message;
                        break;
                    default:
                        line.CostError = message;
                        break;
                }
            }

            return;
        }

        switch (field)
        {
            case PurchaseFields.SupplierId:
                SupplierError = message;
                break;
            case PurchaseFields.InvoiceNumber:
                InvoiceError = message;
                break;
            case PurchaseFields.InvoiceDate:
                DateError = message;
                break;
            case PurchaseFields.Tax:
                TaxError = message;
                break;
            case PurchaseFields.Lines:
                LinesError = message;
                break;
            case PurchaseFields.Subtotal:
                TotalsError = message;
                break;
            default:
                ErrorMessage = message;
                break;
        }
    }

    private void ClearErrors()
    {
        SupplierError = InvoiceError = DateError = TaxError = LinesError = TotalsError = ErrorMessage = null;
        DuplicatePurchaseId = null;
        foreach (var line in Lines)
        {
            line.ClearErrors();
        }
    }

    /// <summary>Limpia la captura para la siguiente compra; conserva la fecha de hoy.</summary>
    private void Reset()
    {
        ClearErrors();
        Lines.Clear();
        SelectedSupplier = null;
        InvoiceNumber = string.Empty;
        InvoiceDate = DateTime.Today;
        TaxText = string.Empty;
        _ = RecalculateAsync();
    }

    private async Task SearchSuppliersAfterDelayAsync()
    {
        _pendingSupplierSearch?.Cancel();
        _pendingSupplierSearch?.Dispose();
        var pending = _pendingSupplierSearch = new CancellationTokenSource();
        try
        {
            await Task.Delay(SearchDelay, pending.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchSuppliersAsync();
    }

    private async Task SearchSuppliersAsync()
    {
        var query = new ListSuppliersForPurchaseQuery(SupplierSearchText);
        var (completed, result) = await _runner.RunAsync(
            "BuscarProveedoresCompra",
            () => _useCases.RunAsync<ListSuppliersForPurchaseHandler, Result<IReadOnlyList<SupplierOption>>>(h => h.HandleAsync(query, CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        // La selección se conserva aunque ya no coincida con el texto buscado.
        var keep = SelectedSupplier;
        SupplierOptions.Clear();
        if (keep is not null)
        {
            SupplierOptions.Add(keep);
        }

        foreach (var supplier in result.Value.Where(s => keep is null || s.Id != keep.Id))
        {
            SupplierOptions.Add(new SupplierChoice(supplier));
        }

        SelectedSupplier = keep;
    }

    [GeneratedRegex(@"^Lines\[(?<index>\d+)\]\.(?<field>\w+)$", RegexOptions.CultureInvariant)]
    private static partial Regex LineField();
}
