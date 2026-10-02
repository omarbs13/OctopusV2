using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.GetCurrentShift;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Users;

namespace Pos.Desktop.CashShifts;

/// <summary>
/// "Caja > Corte Z" (017, Historia 2): muestra el turno abierto y abre el mismo cierre con arqueo
/// ciego de 008. El turno de otro usuario solo lo cierra un administrador (FR-024 de 008).
/// </summary>
public sealed partial class ShiftClosingViewModel : PageViewModel
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-MX");

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly CashShiftDialogs _shiftDialogs;
    private readonly ICurrentPermissions _permissions;

    public ShiftClosingViewModel(UseCases useCases, OperationRunner runner, CashShiftDialogs shiftDialogs, ICurrentPermissions permissions)
    {
        _useCases = useCases;
        _runner = runner;
        _shiftDialogs = shiftDialogs;
        _permissions = permissions;
    }

    public override string Title => Strings.Nav_CashClosing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShift), nameof(HasNoShift), nameof(ShiftText), nameof(CanClose), nameof(IsOtherOwnerBlocked))]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    public partial CurrentShiftSummary? Shift { get; private set; }

    public bool HasShift => Shift is not null;

    public bool HasNoShift => Shift is null;

    public string ShiftText => Shift is { } shift
        ? string.Format(Display, Strings.Cut_ShiftHeader, shift.Folio, shift.OpenedByName, shift.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
        : string.Empty;

    /// <summary>El turno propio lo cierra su dueño; el ajeno, solo un administrador.</summary>
    public bool CanClose => Shift is { } shift && (shift.IsMine || _permissions.Has(Permission.ManageShifts));

    public bool IsOtherOwnerBlocked => Shift is not null && !CanClose;

    public override Task OnActivatedAsync() => RefreshAsync();

    [RelayCommand(CanExecute = nameof(CanClose))]
    private async Task CloseAsync()
    {
        if (Shift is not { } shift)
        {
            return;
        }

        await _shiftDialogs.CloseShiftAsync(shift.ShiftId, shift.IsMine ? null : shift.OpenedByName);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var (completed, result) = await _runner.RunAsync(
            "ConsultarTurnoParaCorteZ",
            () => _useCases.RunAsync<GetCurrentShiftHandler, Result<CurrentShiftSummary?>>(h => h.HandleAsync(CancellationToken.None)));
        if (completed && result is { IsSuccess: true })
        {
            Shift = result.Value;
        }
    }
}
