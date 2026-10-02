using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Purchases;
using Pos.Application.Purchases.GetPurchase;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.Purchases;

/// <summary>Línea del detalle con los textos ya formateados.</summary>
public sealed record PurchaseDetailLineRow(PurchaseLineDetailDto Item)
{
    public int Number => Item.LineNumber;

    public string ProductName => Item.ProductName;

    public string Sku => Item.ProductSku;

    public string QuantityText => QuantityConverter.Format(Item.QuantityThousandths, Item.DecimalPlaces);

    public string UnitName => Item.UnitName;

    public string UnitCostText => MoneyConverter.Format(Item.UnitCostCents);

    public string AmountText => MoneyConverter.Format(Item.AmountCents);

    public bool IsBonus => Item.IsBonus;
}

/// <summary>
/// Detalle de una compra (vista compartida por el reporte, el kárdex y la captura): datos congelados, importes,
/// estado y líneas, sin opciones de edición (escenario 11). "Anular compra" solo con <c>VoidPurchases</c> y si
/// está vigente.
/// </summary>
public sealed partial class PurchaseDetailViewModel : FormViewModel
{
    private static readonly CultureInfo Display = MoneyConverter.Culture;

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly ICurrentPermissions? _permissions;

    private Guid _purchaseId;

    public PurchaseDetailViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, ICurrentPermissions? permissions = null)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        _permissions = permissions;
    }

    /// <summary>La compra se anuló desde este detalle; quien lo abrió puede refrescar su lista.</summary>
    public event EventHandler? Voided;

    public ObservableCollection<PurchaseDetailLineRow> Lines { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(IsVoided))]
    [NotifyPropertyChangedFor(nameof(CanVoid))]
    [NotifyPropertyChangedFor(nameof(SupplierText))]
    [NotifyPropertyChangedFor(nameof(InvoiceDateText))]
    [NotifyPropertyChangedFor(nameof(RegisteredText))]
    [NotifyPropertyChangedFor(nameof(RegisteredByText))]
    [NotifyPropertyChangedFor(nameof(SubtotalText))]
    [NotifyPropertyChangedFor(nameof(TaxText))]
    [NotifyPropertyChangedFor(nameof(TotalText))]
    [NotifyPropertyChangedFor(nameof(VoidedText))]
    [NotifyCanExecuteChangedFor(nameof(VoidCommand))]
    public partial PurchaseDetailDto? Detail { get; private set; }

    /// <summary>Diálogo de anulación abierto dentro del detalle.</summary>
    [ObservableProperty]
    public partial object? ActiveDialog { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    public override string Title => Detail is null ? Strings.Purchase_Title : string.Format(Display, Strings.Purchase_DetailTitle, Detail.InvoiceNumber);

    public bool IsVoided => Detail is { IsVoided: true };

    public bool CanVoid => Detail is { IsVoided: false } && (_permissions?.Has(Permission.VoidPurchases) ?? true);

    public string SupplierText => Detail?.SupplierName ?? string.Empty;

    public string InvoiceDateText => Detail?.InvoiceDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    public string RegisteredText => Detail is null ? string.Empty : LocalText(Detail.RegisteredAtUtc);

    public string RegisteredByText => Detail?.RegisteredByName ?? string.Empty;

    public string SubtotalText => Detail is null ? string.Empty : MoneyConverter.Format(Detail.SubtotalCents);

    public string TaxText => Detail is null ? string.Empty : MoneyConverter.Format(Detail.TaxCents);

    public string TotalText => Detail is null ? string.Empty : MoneyConverter.Format(Detail.TotalCents);

    /// <summary>"Anulada el {fecha} por {usuario}. Motivo: {motivo}" (Historia 3, escenario 8).</summary>
    public string VoidedText => Detail is { IsVoided: true, VoidedAtUtc: { } at }
        ? string.Format(Display, Strings.Purchase_VoidedInfo, LocalText(at), Detail.VoidedByName, Detail.VoidReason)
        : string.Empty;

    /// <summary>Carga el detalle; falso si no existe o falló (el operador ya vio el mensaje).</summary>
    public async Task<bool> LoadAsync(Guid purchaseId)
    {
        _purchaseId = purchaseId;
        var (completed, result) = await _runner.RunAsync(
            "ConsultarCompra",
            () => _useCases.RunAsync<GetPurchaseHandler, Result<PurchaseDetailDto>>(h => h.HandleAsync(new GetPurchaseQuery(purchaseId), CancellationToken.None)),
            new Dictionary<string, object?> { ["PurchaseId"] = purchaseId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, result.Error is Forbidden ? Strings.Common_Forbidden : Strings.Purchase_NotFound);
            return false;
        }

        Detail = result.Value;
        Lines.Clear();
        foreach (var line in result.Value.Lines)
        {
            Lines.Add(new PurchaseDetailLineRow(line));
        }

        return true;
    }

    protected override object CaptureState() => 0;

    protected override Task<bool> SaveCoreAsync() => Task.FromResult(true);

    [RelayCommand(CanExecute = nameof(CanVoid))]
    private void Void()
    {
        if (Detail is not { } detail || ActiveDialog is not null)
        {
            return;
        }

        ErrorMessage = null;
        ActiveDialog = new VoidPurchaseViewModel(_useCases, _runner, detail, OnVoidedAsync, OnConflictAsync, () => ActiveDialog = null);
    }

    private async Task OnVoidedAsync()
    {
        ActiveDialog = null;
        await LoadAsync(_purchaseId);
        Voided?.Invoke(this, EventArgs.Empty);
    }

    private async Task OnConflictAsync()
    {
        ActiveDialog = null;
        await LoadAsync(_purchaseId);
        ErrorMessage = Strings.Purchase_Conflict;
    }

    private static string LocalText(DateTime utc) => utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
