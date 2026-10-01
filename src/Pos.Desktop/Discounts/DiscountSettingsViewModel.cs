using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Discounts.Settings.GetDiscountSettings;
using Pos.Application.Discounts.Settings.SaveDiscountSettings;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Discounts;

/// <summary>Descuentos > Configuración (015, FR-005): "Descuento máximo sin autorización (%)", con 2 decimales.</summary>
public sealed partial class DiscountSettingsViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    public DiscountSettingsViewModel(UseCases useCases, OperationRunner runner)
    {
        _useCases = useCases;
        _runner = runner;
    }

    public override string Title => Strings.DiscountSettings_Title;

    [ObservableProperty]
    public partial string LimitText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    public partial bool MessageIsError { get; private set; }

    public override async Task OnActivatedAsync()
    {
        Message = null;
        var (completed, result) = await _runner.RunAsync(
            "LeerLimiteDeDescuento",
            () => _useCases.RunAsync<GetDiscountSettingsHandler, Result<DiscountSettings>>(h => h.HandleAsync(CancellationToken.None)));
        if (completed && result is { IsSuccess: true })
        {
            LimitText = FormatPercent(result.Value.LimitBasisPoints);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryParsePercent(LimitText, out var basisPoints))
        {
            ShowMessage(Strings.DiscountSettings_Invalid, error: true);
            return;
        }

        var command = new SaveDiscountSettingsCommand(basisPoints);
        var (completed, result) = await _runner.RunAsync(
            "GuardarLimiteDeDescuento",
            () => _useCases.RunAsync<SaveDiscountSettingsHandler, Result<DiscountSettings>>(h => h.HandleAsync(command, CancellationToken.None)));
        if (!completed || result is null)
        {
            return;
        }

        switch (result.Error)
        {
            case null:
                LimitText = FormatPercent(result.Value.LimitBasisPoints);
                ShowMessage(Strings.DiscountSettings_Saved, error: false);
                break;
            case ValidationFailed validation:
                ShowMessage(validation.Errors is [{ } first, ..] ? first.Message : Strings.DiscountSettings_Invalid, error: true);
                break;
            case ModuleNotLicensed:
                ShowMessage(Strings.License_ModuleNotLicensed, error: true);
                break;
            case Forbidden:
                ShowMessage(Strings.Common_Forbidden, error: true);
                break;
            default:
                ShowMessage(Strings.Common_UnexpectedError, error: true);
                break;
        }
    }

    /// <summary>Puntos base a texto: 1000 → "10", 1250 → "12.5".</summary>
    internal static string FormatPercent(long basisPoints) =>
        (basisPoints / 100m).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Acepta de 0 a 100 con hasta 2 decimales ("10", "12.5", "7,25"); nunca redondea.</summary>
    internal static bool TryParsePercent(string? text, out int basisPoints)
    {
        basisPoints = 0;
        var normalized = (text ?? string.Empty).Trim().Replace(',', '.').TrimEnd('%').Trim();
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var percent))
        {
            return false;
        }

        var scaled = percent * 100;
        if (scaled != decimal.Truncate(scaled) || scaled is < DiscountSettings.MinLimitBasisPoints or > DiscountSettings.MaxLimitBasisPoints)
        {
            return false;
        }

        basisPoints = (int)scaled;
        return true;
    }

    private void ShowMessage(string message, bool error)
    {
        Message = message;
        MessageIsError = error;
    }
}
