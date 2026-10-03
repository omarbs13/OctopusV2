using Pos.Application.Abstractions;

namespace Pos.Application.Licensing;

/// <summary>
/// Rechazo explícito en bloqueo para los casos de uso que usan un permiso exento (por ejemplo <c>OperateShift</c>)
/// pero no son parte de terminar la venta en curso ni de cerrar el turno abierto (025, research §10.3, blocked-mode §1).
/// </summary>
public static class LicenseGate
{
    /// <summary>El error <see cref="SystemNotActivated"/> si el sistema está bloqueado; nulo si no.</summary>
    public static SystemNotActivated? WhenBlocked(ILicenseState? license) =>
        license?.Current is { IsBlocked: true, BlockReason: { } reason } ? new SystemNotActivated(reason) : null;
}
