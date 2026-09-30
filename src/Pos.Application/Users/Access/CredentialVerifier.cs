using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Domain.Users;

namespace Pos.Application.Users.Access;

/// <summary>Qué se audita al verificar credenciales y si se exige rol de administrador.</summary>
public sealed record CredentialEvents(
    bool RequireAdmin,
    Action<IAuditLog, User?, string> OnFailure,
    Action<IAuditLog, User>? OnSuccess);

/// <summary>
/// Reglas de acceso compartidas por el inicio de sesión, la autorización de administrador y el
/// desbloqueo (research §9): mensaje genérico, contador de fallos persistente, bloqueo de 5
/// intentos / 5 minutos y contador en memoria para nombres inexistentes. El contador y la bitácora de
/// cada intento se escriben en una sola transacción (Principio I). Nunca registra contraseñas.
/// </summary>
public sealed partial class CredentialVerifier
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly LoginThrottle _throttle;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ILogger<CredentialVerifier> _logger;

    public CredentialVerifier(
        IUserRepository users,
        IPasswordHasher hasher,
        LoginThrottle throttle,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ILogger<CredentialVerifier> logger)
    {
        _users = users;
        _hasher = hasher;
        _throttle = throttle;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Verifica usuario y contraseña; devuelve el usuario con seguimiento o <see cref="InvalidCredentials"/> / <see cref="LockedOut"/>.</summary>
    public async Task<Result<User>> VerifyAsync(
        string userName,
        string password,
        CredentialEvents events,
        CancellationToken cancellationToken)
    {
        var normalized = UserNameRules.Normalize(userName);
        var user = await _users.FindByUserNameAsync(normalized, cancellationToken);
        return await VerifyAsync(user, normalized, userName.Trim(), password, events, cancellationToken);
    }

    /// <summary>Igual, sobre un usuario ya cargado (desbloqueo de la sesión).</summary>
    public Task<Result<User>> VerifyAsync(
        User user,
        string password,
        CredentialEvents events,
        CancellationToken cancellationToken) =>
        VerifyAsync(user, user.NormalizedUserName, user.UserName, password, events, cancellationToken);

    private async Task<Result<User>> VerifyAsync(
        User? user,
        string normalized,
        string enteredName,
        string password,
        CredentialEvents events,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        if (user is null || user.IsSystem)
        {
            return await RejectUnknownAsync(normalized, enteredName, password, events, cancellationToken);
        }

        if (user.IsLockedOut(now))
        {
            return Result.Failure<User>(new LockedOut(user.LockoutEndsAt!.Value));
        }

        var check = _hasher.Verify(password, user.PasswordHash ?? _throttle.DummyHashFor(_hasher));
        if (check == PasswordCheck.Failed)
        {
            return await RejectWrongPasswordAsync(user, enteredName, now, events, cancellationToken);
        }

        if (!user.IsActive || (events.RequireAdmin && user.Role != UserRole.Admin))
        {
            // Contraseña correcta pero no puede entrar: mensaje genérico, sin contar el intento.
            events.OnFailure(_audit, user, enteredName);
            await _audit.SaveAsync(cancellationToken);
            LogRejected(user.Id, user.IsActive);
            return Result.Failure<User>(new InvalidCredentials());
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);
        user.RegisterSuccessfulLogin(now);
        await _users.RecordLoginAttemptAsync(user.Id, 0, null, now, cancellationToken);
        if (check == PasswordCheck.SucceededRehashNeeded)
        {
            // Endurece el hash con el costo vigente tras un acceso correcto (research §1).
            user.SetPassword(_hasher.Hash(password), user.MustChangePassword);
        }

        events.OnSuccess?.Invoke(_audit, user);
        await _audit.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(user);
    }

    private async Task<Result<User>> RejectUnknownAsync(
        string normalized,
        string enteredName,
        string password,
        CredentialEvents events,
        CancellationToken cancellationToken)
    {
        if (_throttle.LockedUntil(normalized) is { } until)
        {
            return Result.Failure<User>(new LockedOut(until));
        }

        // Mismo costo que una verificación real para no revelar que el usuario no existe.
        _hasher.Verify(password, _throttle.DummyHashFor(_hasher));
        var locked = _throttle.RegisterFailure(normalized);

        events.OnFailure(_audit, null, enteredName);
        await _audit.SaveAsync(cancellationToken);
        LogUnknown();

        return Result.Failure<User>(locked && _throttle.LockedUntil(normalized) is { } lockedUntil
            ? new LockedOut(lockedUntil)
            : new InvalidCredentials());
    }

    private async Task<Result<User>> RejectWrongPasswordAsync(
        User user,
        string enteredName,
        DateTime now,
        CredentialEvents events,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _transactions.BeginAsync(cancellationToken);
        var lockedNow = user.RegisterFailedLogin(now);
        await _users.RecordLoginAttemptAsync(user.Id, user.FailedLoginCount, user.LockoutEndsAt, null, cancellationToken);
        events.OnFailure(_audit, user, enteredName);
        if (lockedNow)
        {
            _audit.Add(AuditActions.UserLockedOut, AuditActions.UserEntity, user.Id, null);
        }

        await _audit.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogFailed(user.Id, lockedNow);

        return Result.Failure<User>(lockedNow ? new LockedOut(user.LockoutEndsAt!.Value) : new InvalidCredentials());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Acceso rechazado para un usuario inexistente.")]
    private partial void LogUnknown();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Contraseña incorrecta. UserId={UserId} Bloqueado={LockedOut}")]
    private partial void LogFailed(Guid userId, bool lockedOut);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Acceso rechazado con contraseña correcta. UserId={UserId} Activo={IsActive}")]
    private partial void LogRejected(Guid userId, bool isActive);
}
