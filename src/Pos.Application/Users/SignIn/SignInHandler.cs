using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;

namespace Pos.Application.Users.SignIn;

/// <summary>
/// Inicio de sesión (FR-002 a FR-006). No abre la sesión: sella al usuario autenticado para que
/// <c>StartSession</c> lo acepte tras el cambio obligatorio de contraseña, si lo hay.
/// </summary>
public sealed partial class SignInHandler
{
    private readonly CredentialVerifier _verifier;
    private readonly UserSession _session;
    private readonly IValidator<SignInCommand> _validator;
    private readonly ILogger<SignInHandler> _logger;

    public SignInHandler(
        CredentialVerifier verifier,
        UserSession session,
        IValidator<SignInCommand> validator,
        ILogger<SignInHandler> logger)
    {
        _verifier = verifier;
        _session = session;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<SignInOutcome>> HandleAsync(SignInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SignInOutcome>(ProductRules.ToError(validation));
        }

        var events = new CredentialEvents(
            RequireAdmin: false,
            OnFailure: (audit, user, enteredName) => audit.Add(
                AuditActions.LoginFailed,
                AuditActions.UserEntity,
                user?.Id ?? Guid.Empty,
                user is null ? $"Usuario: {Truncate(enteredName)}" : null),
            OnSuccess: (audit, user) => audit.Add(AuditActions.LoginSucceeded, AuditActions.UserEntity, user.Id, null));

        var verified = await _verifier.VerifyAsync(command.UserName, command.Password, events, cancellationToken);
        if (!verified.IsSuccess)
        {
            return Result.Failure<SignInOutcome>(verified.Error);
        }

        var user = verified.Value;
        var sessionUser = SessionUser.From(user);
        _session.Seal(sessionUser);
        LogSignedIn(user.Id, user.UserName);
        return Result.Success(new SignInOutcome(sessionUser, user.MustChangePassword));
    }

    private static string Truncate(string text) => text.Length <= 40 ? text : text[..40];

    [LoggerMessage(Level = LogLevel.Information, Message = "Inicio de sesión correcto. UserId={UserId} Usuario={UserName}")]
    private partial void LogSignedIn(Guid userId, string userName);
}
