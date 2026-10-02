using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.GenerateShiftReadout;
using Pos.Application.CashShifts.GetCurrentShift;
using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.CashShifts;

/// <summary>
/// "Caja > Corte X" (017, Historia 1): muestra de qué turno se hará la lectura, sin ninguna cifra, y
/// genera un Corte X por clic. El Cajero necesita la autorización de un administrador (FR-007).
/// </summary>
public sealed partial class ShiftReadoutViewModel : PageViewModel
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly AdminAuthorizationService _authorization;
    private readonly Func<ShiftCutDetailViewModel> _detailFactory;

    public ShiftReadoutViewModel(
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        AdminAuthorizationService authorization,
        Func<ShiftCutDetailViewModel> detailFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _authorization = authorization;
        _detailFactory = detailFactory;
    }

    public override string Title => Strings.Nav_CashReadout;

    public override FormHost Forms { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShift), nameof(HasNoShift), nameof(ShiftText))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial CurrentShiftSummary? Shift { get; private set; }

    public bool HasShift => Shift is not null;

    public bool HasNoShift => Shift is null;

    /// <summary>"Turno T-000123 de {nombre} · desde {hora}"; nunca muestra cifras antes de generar (contracts/ui.md).</summary>
    public string ShiftText => Shift is { } shift
        ? string.Format(Display, Strings.Cut_ShiftHeader, shift.Folio, shift.OpenedByName, shift.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
        : string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial bool IsBusy { get; private set; }

    public override Task OnActivatedAsync() => RefreshAsync();

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await GenerateCoreAsync(grant: null);
            if (result?.Error is Forbidden { CanBeAuthorized: true } && Shift is { } shift)
            {
                var grant = await _authorization.RequestAsync(
                    Permission.GenerateShiftReadout,
                    string.Format(Display, Strings.Cut_ReadoutContext, shift.Folio));
                if (grant is not { } grantId)
                {
                    ErrorMessage = Strings.Cut_AuthorizationRequired;
                    return;
                }

                result = await GenerateCoreAsync(grantId);
            }

            if (result is null)
            {
                return;
            }

            switch (result.Error)
            {
                case null:
                    await OpenCutAsync(result.Value.CutId);
                    break;

                case ShiftRequired:
                    Shift = null;
                    break;

                case Forbidden:
                    ErrorMessage = Strings.Common_Forbidden;
                    break;

                case Conflict:
                    ErrorMessage = CashShiftMessages.CutNumberTaken;
                    break;

                default:
                    ErrorMessage = Strings.Common_UnexpectedError;
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanGenerate() => !IsBusy && Shift is not null;

    private async Task<Result<GeneratedShiftCut>?> GenerateCoreAsync(Guid? grant)
    {
        var command = new GenerateShiftReadoutCommand(grant);
        var (completed, result) = await _runner.RunAsync(
            "GenerarCorteX",
            () => _useCases.RunAsync<GenerateShiftReadoutHandler, Result<GeneratedShiftCut>>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["Authorized"] = grant is not null });
        return completed ? result : null;
    }

    private async Task OpenCutAsync(Guid cutId)
    {
        var detail = _detailFactory();
        if (await detail.LoadAsync(cutId, isReprint: false))
        {
            await Forms.OpenAsync(detail, FormPresentation.FullScreen);
        }
        else
        {
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Cut_NotFound);
        }
    }

    private async Task RefreshAsync()
    {
        var (completed, result) = await _runner.RunAsync(
            "ConsultarTurnoParaCorteX",
            () => _useCases.RunAsync<GetCurrentShiftHandler, Result<CurrentShiftSummary?>>(h => h.HandleAsync(CancellationToken.None)));
        if (completed && result is { IsSuccess: true })
        {
            Shift = result.Value;
        }
    }
}
