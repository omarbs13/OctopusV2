using Pos.Desktop.Auth;
using Pos.Desktop.Common;
using Pos.Desktop.Settings;
using Pos.Desktop.Shell;
using Pos.Domain.CashShifts;

namespace Pos.Desktop.CashShifts;

/// <summary>
/// Abre los diálogos de turno (apertura, movimiento de efectivo y cierre) en el <see cref="ModalHost"/>
/// de la sesión, para usarlos tanto desde el Punto de venta como desde "Turnos". Cada método espera a
/// que el diálogo termine y devuelve si hubo un cambio (el llamador refresca su estado).
/// </summary>
public sealed class CashShiftDialogs
{
    private readonly ModalHost _modal;
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly TicketPrintingService _printing;
    private readonly AdminAuthorizationService _authorization;

    public CashShiftDialogs(
        ModalHost modal,
        UseCases useCases,
        OperationRunner runner,
        IDialogService dialogs,
        TicketPrintingService printing,
        AdminAuthorizationService authorization)
    {
        _modal = modal;
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _printing = printing;
        _authorization = authorization;
    }

    /// <summary>Pide el fondo inicial y abre el turno; verdadero si quedó abierto.</summary>
    public Task<bool> OpenShiftAsync()
    {
        var completion = NewCompletion();
        _modal.Show(new OpenShiftViewModel(_useCases, _runner, _dialogs, result => Finish(completion, result)));
        return completion.Task;
    }

    /// <summary>Captura un ingreso o un retiro del turno; verdadero si se registró.</summary>
    public Task<bool> RegisterMovementAsync(Guid shiftId, CashMovementType type)
    {
        var completion = NewCompletion();
        CashMovementViewModel? dialog = null;
        dialog = new CashMovementViewModel(
            _useCases,
            _runner,
            _dialogs,
            _printing,
            _authorization,
            shiftId,
            type,
            () => _modal.Show(dialog!),
            result => Finish(completion, result));
        _modal.Show(dialog);
        return completion.Task;
    }

    /// <summary>
    /// Cierra el turno con el arqueo ciego en tres pasos; verdadero si quedó cerrado. <paramref name="beforeCount"/>
    /// espera el guardado pendiente de la venta en curso antes de contar (research §9).
    /// </summary>
    public async Task<bool> CloseShiftAsync(Guid shiftId, string? ownerName = null, Func<Task>? beforeCount = null)
    {
        if (beforeCount is not null)
        {
            await beforeCount();
        }

        var completion = NewCompletion();
        _modal.Show(new CloseShiftViewModel(
            _useCases,
            _runner,
            _dialogs,
            _printing,
            shiftId,
            ownerName,
            result => Finish(completion, result)));
        return await completion.Task;
    }

    private static TaskCompletionSource<bool> NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Finish(TaskCompletionSource<bool> completion, bool result)
    {
        _modal.Close();
        completion.TrySetResult(result);
    }
}
