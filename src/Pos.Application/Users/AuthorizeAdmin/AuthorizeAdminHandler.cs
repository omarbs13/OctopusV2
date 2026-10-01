using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;
using Pos.Domain.Users;

namespace Pos.Application.Users.AuthorizeAdmin;

/// <summary>
/// Autorización de administrador desde la sesión de un usuario sin el permiso (Historia 7). Aplica
/// las mismas reglas de acceso que el inicio de sesión (mismo contador de bloqueo, FR-005) y emite una
/// concesión de un solo uso; la sesión del solicitante no cambia (research §8).
/// </summary>
public sealed partial class AuthorizeAdminHandler
{
    private readonly CredentialVerifier _verifier;
    private readonly IUserSession _session;
    private readonly IAuthorizationGrants _grants;
    private readonly IValidator<AuthorizeAdminCommand> _validator;
    private readonly ILogger<AuthorizeAdminHandler> _logger;

    public AuthorizeAdminHandler(
        CredentialVerifier verifier,
        IUserSession session,
        IAuthorizationGrants grants,
        IValidator<AuthorizeAdminCommand> validator,
        ILogger<AuthorizeAdminHandler> logger)
    {
        _verifier = verifier;
        _session = session;
        _grants = grants;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(AuthorizeAdminCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!RolePermissions.IsAuthorizable(command.Permission))
        {
            return Result.Failure<Guid>(new InvalidState(UserMessages.NotAuthorizable));
        }

        if (_session.User is not { } requester)
        {
            return Result.Failure<Guid>(new InvalidState("No hay una sesión que solicite la autorización."));
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ProductRules.ToError(validation));
        }

        var permission = command.Permission;
        var context = string.IsNullOrWhiteSpace(command.Context) ? string.Empty : $". {Truncate(command.Context.Trim(), ContextMaxLength)}";
        var events = new CredentialEvents(
            RequireAdmin: true,
            OnFailure: (audit, admin, enteredName) => audit.Add(
                AuditActions.AdminAuthorizationDenied,
                AuditActions.UserEntity,
                admin?.Id ?? Guid.Empty,
                $"Permiso: {permission}. Usuario: {Truncate(enteredName)}{context}"),
            OnSuccess: (audit, admin) => audit.Add(
                AuditActions.AdminAuthorizationGranted,
                AuditActions.UserEntity,
                admin.Id,
                $"Permiso: {permission}{context}",
                authorizedBy: admin.Id));

        var verified = await _verifier.VerifyAsync(command.UserName, command.Password, events, cancellationToken);
        if (!verified.IsSuccess)
        {
            return Result.Failure<Guid>(verified.Error);
        }

        var grantId = _grants.Issue(permission, requester.Id, verified.Value.Id);
        LogGranted(requester.Id, verified.Value.Id, permission);
        return Result.Success(grantId);
    }

    private const int ContextMaxLength = 200;

    private static string Truncate(string text) => Truncate(text, 40);

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    [LoggerMessage(Level = LogLevel.Information, Message = "Autorización de administrador concedida. Solicitante={RequestedBy} Administrador={AuthorizedBy} Permiso={Permission}")]
    private partial void LogGranted(Guid requestedBy, Guid authorizedBy, Permission permission);
}
