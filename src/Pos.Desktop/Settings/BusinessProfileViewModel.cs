using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Business.GetBusinessProfile;
using Pos.Application.Business.SaveBusinessProfile;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Settings;

/// <summary>Pantalla "Datos del negocio": lo que aparece en el encabezado y el pie del ticket (006, US1).</summary>
public sealed partial class BusinessProfileViewModel : PageViewModel
{
    private static readonly IReadOnlyList<FileTypeFilter> ImageFilters =
        [new(Strings.Editor_ImageFilter, ["*.jpg", "*.jpeg", "*.png", "*.webp"])];

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;

    private byte[]? _pendingLogo;
    private byte[]? _storedLogo;
    private bool _removeLogo;

    public BusinessProfileViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
    }

    public override string Title => Strings.Nav_BusinessProfile;

    [ObservableProperty]
    public partial string TradeName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaxId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FooterMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogo))]
    [NotifyCanExecuteChangedFor(nameof(RemoveLogoCommand))]
    public partial byte[]? PreviewLogo { get; private set; }

    public bool HasLogo => PreviewLogo is not null;

    [ObservableProperty]
    public partial string? TradeNameError { get; private set; }

    [ObservableProperty]
    public partial string? AddressError { get; private set; }

    [ObservableProperty]
    public partial string? PhoneError { get; private set; }

    [ObservableProperty]
    public partial string? TaxIdError { get; private set; }

    [ObservableProperty]
    public partial string? FooterMessageError { get; private set; }

    [ObservableProperty]
    public partial string? LogoError { get; private set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; private set; }

    public override async Task OnActivatedAsync()
    {
        StatusMessage = null;
        ClearErrors();
        var (completed, profile) = await _runner.RunAsync(
            "CargarDatosNegocio",
            () => _useCases.RunAsync<GetBusinessProfileHandler, BusinessProfileDto?>(h => h.HandleAsync(CancellationToken.None)));
        if (!completed)
        {
            return;
        }

        Load(profile);
    }

    [RelayCommand]
    private async Task SelectLogoAsync()
    {
        var file = await _dialogs.PickOpenFileAsync(Strings.BusinessProfile_SelectLogo, ImageFilters);
        if (file is null)
        {
            return;
        }

        LogoError = null;
        if (file.Length > Pos.Domain.Business.BusinessProfile.LogoMaxBytes)
        {
            LogoError = BusinessMessages.LogoTooLarge;
            return;
        }

        var (completed, bytes) = await _runner.RunAsync(
            "LeerLogotipo",
            async () =>
            {
                await using var content = await file.OpenAsync();
                using var memory = new MemoryStream();
                await content.CopyToAsync(memory);
                return memory.ToArray();
            },
            new Dictionary<string, object?> { ["FileName"] = file.Name, ["Length"] = file.Length });
        if (!completed || bytes is null)
        {
            return;
        }

        _pendingLogo = bytes;
        _removeLogo = false;
        PreviewLogo = bytes;
    }

    [RelayCommand(CanExecute = nameof(HasLogo))]
    private void RemoveLogo()
    {
        _pendingLogo = null;
        _removeLogo = _storedLogo is not null;
        PreviewLogo = null;
        LogoError = null;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ClearErrors();
        StatusMessage = null;
        IsSaving = true;
        try
        {
            var (completed, result) = await _runner.RunAsync(
                "GuardarDatosNegocio",
                async () =>
                {
                    await using var stream = _pendingLogo is null ? null : new MemoryStream(_pendingLogo);
                    LogoChange logo = stream is not null
                        ? new LogoChange.Replace(stream, stream.Length)
                        : _removeLogo ? new LogoChange.Remove() : LogoChange.KeepCurrent;
                    var command = new SaveBusinessProfileCommand(TradeName, Address, Phone, TaxId, FooterMessage, logo);
                    return await _useCases.RunAsync<SaveBusinessProfileHandler, Result>(h => h.HandleAsync(command, CancellationToken.None));
                });

            if (!completed || result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    await ReloadAfterSaveAsync();
                    StatusMessage = Strings.BusinessProfile_Saved;
                    break;

                case ValidationFailed validation:
                    ShowErrors(validation);
                    break;

                case Conflict:
                    await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.BusinessProfile_Changed);
                    await OnActivatedAsync();
                    break;

                default:
                    StatusMessage = Strings.Common_UnexpectedError;
                    break;
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanSave() => !IsSaving;

    private async Task ReloadAfterSaveAsync()
    {
        var (completed, profile) = await _runner.RunAsync(
            "CargarDatosNegocio",
            () => _useCases.RunAsync<GetBusinessProfileHandler, BusinessProfileDto?>(h => h.HandleAsync(CancellationToken.None)));
        if (completed)
        {
            Load(profile);
        }
    }

    private void Load(BusinessProfileDto? profile)
    {
        TradeName = profile?.TradeName ?? string.Empty;
        Address = profile?.Address ?? string.Empty;
        Phone = profile?.Phone ?? string.Empty;
        TaxId = profile?.TaxId ?? string.Empty;
        FooterMessage = profile?.FooterMessage ?? string.Empty;
        _storedLogo = profile?.Logo;
        _pendingLogo = null;
        _removeLogo = false;
        PreviewLogo = _storedLogo;
    }

    private void ShowErrors(ValidationFailed validation)
    {
        foreach (var error in validation.Errors)
        {
            switch (error.Field)
            {
                case BusinessFields.TradeName:
                    TradeNameError = error.Message;
                    break;
                case BusinessFields.Address:
                    AddressError = error.Message;
                    break;
                case BusinessFields.Phone:
                    PhoneError = error.Message;
                    break;
                case BusinessFields.TaxId:
                    TaxIdError = error.Message;
                    break;
                case BusinessFields.FooterMessage:
                    FooterMessageError = error.Message;
                    break;
                case BusinessFields.Logo:
                    // Un logotipo inválido se rechaza y se conserva el anterior (006, US1 #4).
                    LogoError = error.Message;
                    _pendingLogo = null;
                    _removeLogo = false;
                    PreviewLogo = _storedLogo;
                    break;
            }
        }
    }

    private void ClearErrors()
    {
        TradeNameError = null;
        AddressError = null;
        PhoneError = null;
        TaxIdError = null;
        FooterMessageError = null;
        LogoError = null;
    }
}
