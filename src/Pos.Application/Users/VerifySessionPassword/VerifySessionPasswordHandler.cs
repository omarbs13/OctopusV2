using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;

namespace Pos.Application.Users.VerifySessionPassword;

/// <summary>
/// Desbloqueo por inactividad (FR-023): verifica la contraseña del usuario de la sesión. Los
/// fallos cuentan para el bloqueo de FR-005 y quedan en la bitácora.
/// </summary>
public sealed class VerifySessionPasswordHandler
{
    private readonly CredentialVerifier _verifier;
    private readonly IUserSession _session;
    private readonly IUserRepository _users;

    public VerifySessionPasswordHandler(CredentialVerifier verifier, IUserSession session, IUserRepository users)
    {
        _verifier = verifier;
        _session = session;
        _users = users;
    }

    public async Task<Result> HandleAsync(string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Result.Failure(new InvalidCredentials());
        }

        var sessionUser = _session.User;
        var user = sessionUser is null ? null : await _users.GetAsync(sessionUser.Id, cancellationToken);
        if (user is null)
        {
            return Result.Failure(new InvalidCredentials());
        }

        var events = new CredentialEvents(
            RequireAdmin: false,
            OnFailure: (audit, failed, _) => audit.Add(AuditActions.LoginFailed, AuditActions.UserEntity, failed?.Id ?? user.Id, "Desbloqueo de sesión"),
            OnSuccess: null);

        var verified = await _verifier.VerifyAsync(user, password, events, cancellationToken);
        return verified.IsSuccess ? Result.Success() : Result.Failure(verified.Error);
    }
}
