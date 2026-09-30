using Pos.Desktop.Common;
using Pos.Desktop.Shell;
using Pos.Domain.Users;

namespace Pos.Desktop.Auth;

/// <summary>
/// Pide la autorización de un administrador a través del diálogo de la sesión y devuelve la
/// concesión, o nulo si se cancela (Historia 7). La sesión del solicitante no cambia.
/// </summary>
public sealed class AdminAuthorizationService
{
    private readonly ModalHost _modal;
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    public AdminAuthorizationService(ModalHost modal, UseCases useCases, OperationRunner runner)
    {
        _modal = modal;
        _useCases = useCases;
        _runner = runner;
    }

    public Task<Guid?> RequestAsync(Permission permission)
    {
        var completion = new TaskCompletionSource<Guid?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _modal.Show(new AdminAuthorizationViewModel(
            _useCases,
            _runner,
            permission,
            grant =>
            {
                _modal.Close();
                completion.TrySetResult(grant);
            }));
        return completion.Task;
    }
}
