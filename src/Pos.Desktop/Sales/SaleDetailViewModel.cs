using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Sales;
using Pos.Application.Sales.GetSale;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.Sales;

namespace Pos.Desktop.Sales;

/// <summary>Línea de una venta registrada, con los valores guardados al venderla.</summary>
public sealed record SaleLineRow(SaleLineDto Line)
{
    public string Name => Line.ProductName;

    public string Sku => Line.ProductSku;

    public string QuantityText => QuantityConverter.Format(Line.QuantityThousandths, Line.DecimalPlaces);

    public string PriceText => MoneyConverter.Format(Line.UnitPriceCents);

    public string AmountText => MoneyConverter.Format(Line.AmountCents);
}

/// <summary>Pago de una venta registrada.</summary>
public sealed record SalePaymentRow(SalePaymentDto Payment)
{
    public string MethodText => PaymentMethodLabels.Of(Payment.Method);

    public string AmountText => MoneyConverter.Format(Payment.AmountCents);

    public string ReceivedText => Payment.ReceivedCents is { } received ? MoneyConverter.Format(received) : QuantityConverter.NoValue;

    public string ChangeText => Payment.ChangeCents is { } change ? MoneyConverter.Format(change) : QuantityConverter.NoValue;

    public string ReferenceText => Payment.Reference ?? string.Empty;
}

/// <summary>
/// Detalle de una venta: encabezado, líneas y pagos con los valores guardados, y la cancelación
/// cuando la venta está completada. Es de solo lectura: nunca tiene cambios sin guardar.
/// </summary>
public sealed partial class SaleDetailViewModel : FormViewModel
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    private Guid _saleId;

    public SaleDetailViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
    }

    public override string Title => Detail is null
        ? Strings.SaleDetail_Title
        : string.Format(Display, Strings.SaleDetail_TitleWithFolio, Detail.Folio);

    public ObservableCollection<SaleLineRow> Lines { get; } = [];

    public ObservableCollection<SalePaymentRow> Payments { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HeaderText), nameof(TotalText), nameof(IsCancelled), nameof(CanCancel), nameof(CancellationText))]
    public partial SaleDetailDto? Detail { get; private set; }

    /// <summary>Formulario de cancelación, mientras esté abierto.</summary>
    [ObservableProperty]
    public partial CancelSaleViewModel? CancelForm { get; private set; }

    public string HeaderText => Detail is null
        ? string.Empty
        : string.Format(
            Display,
            Strings.SaleDetail_Header,
            Detail.Folio,
            Detail.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            Detail.CreatedByName,
            Detail.Status == SaleStatus.Cancelled ? Strings.Sales_StatusCancelled : Strings.Sales_StatusCompleted);

    public string TotalText => Detail is null ? string.Empty : MoneyConverter.Format(Detail.TotalCents);

    public bool IsCancelled => Detail?.Status == SaleStatus.Cancelled;

    /// <summary>El botón "Cancelar venta" solo aparece si la venta está completada.</summary>
    public bool CanCancel => Detail?.Status == SaleStatus.Completed && CancelForm is null;

    public string CancellationText => Detail is { Status: SaleStatus.Cancelled } d
        ? string.Format(
            Display,
            Strings.SaleDetail_CancelledInfo,
            d.CancellationReason,
            d.CancelledAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            d.CancelledByName)
        : string.Empty;

    /// <summary>Carga la venta; falso si no existe o falló (el operador ya vio el mensaje).</summary>
    public async Task<bool> LoadAsync(Guid saleId)
    {
        _saleId = saleId;
        var (completed, result) = await _runner.RunAsync(
            "ConsultarVenta",
            () => _useCases.RunAsync<GetSaleHandler, Result<SaleDetailDto>>(
                h => h.HandleAsync(new GetSaleQuery(saleId), CancellationToken.None)),
            new Dictionary<string, object?> { ["SaleId"] = saleId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Editor_NotFound);
            return false;
        }

        Apply(result.Value);
        return true;
    }

    public void StartCancel()
    {
        if (Detail is not { Status: SaleStatus.Completed } detail || CancelForm is not null)
        {
            return;
        }

        CancelForm = new CancelSaleViewModel(_useCases, _runner, Dialogs, detail, OnCancelFinishedAsync, CloseCancelForm);
        OnPropertyChanged(nameof(CanCancel));
    }

    protected override object CaptureState() => 0;

    protected override Task<bool> SaveCoreAsync() => Task.FromResult(true);

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelSale() => StartCancel();

    private void CloseCancelForm()
    {
        CancelForm = null;
        OnPropertyChanged(nameof(CanCancel));
    }

    /// <summary>Tras cancelar, o si la venta cambió, vuelve a leerla para mostrar su estado actual.</summary>
    private async Task OnCancelFinishedAsync()
    {
        CloseCancelForm();
        await LoadAsync(_saleId);
    }

    private void Apply(SaleDetailDto detail)
    {
        Lines.Clear();
        foreach (var line in detail.Lines)
        {
            Lines.Add(new SaleLineRow(line));
        }

        Payments.Clear();
        foreach (var payment in detail.Payments)
        {
            Payments.Add(new SalePaymentRow(payment));
        }

        Detail = detail;
        CancelSaleCommand.NotifyCanExecuteChanged();
    }
}
