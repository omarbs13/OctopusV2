using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Users.Session;

namespace Pos.Application.Users.EndSession;

/// <summary>
/// Cierra la sesión y audita <c>LOGOUT</c>. La venta en curso ya está en el borrador del usuario
/// (007, Historia 8), así que no se pierde.
/// </summary>
public sealed class EndSessionHandler
{
    private readonly UserSession _session;
    private readonly IAuditLog _audit;

    public EndSessionHandler(UserSession session, IAuditLog audit)
    {
        _session = session;
        _audit = audit;
    }

    public async Task<Result> HandleAsync(CancellationToken cancellationToken)
    {
        if (_session.User is { } user)
        {
            _audit.Add(AuditActions.Logout, AuditActions.UserEntity, user.Id, null);
            await _audit.SaveAsync(cancellationToken);
        }

        _session.End();
        return Result.Success();
    }
}
