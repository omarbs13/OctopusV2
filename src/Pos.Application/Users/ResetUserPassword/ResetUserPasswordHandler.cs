using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;
using Pos.Domain.Users;

namespace Pos.Application.Users.ResetUserPassword;

/// <summary>
/// Restablece la contraseña de otro usuario (FR-016): queda como temporal y se reinicia su bloqueo.
/// Para la propia contraseña se usa <c>ChangeOwnPassword</c>.
/// </summary>
public sealed partial class ResetUserPasswordHandler
{
    private readonly IAccessControl _access;
    private readonly IUserSession _session;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<ResetUserPasswordCommand> _validator;
    private readonly ILogger<ResetUserPasswordHandler> _logger;

    public ResetUserPasswordHandler(
        IAccessControl access,
        IUserSession session,
        IUserRepository users,
        IPasswordHasher hasher,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<ResetUserPasswordCommand> validator,
        ILogger<ResetUserPasswordHandler> logger)
    {
        _access = access;
        _session = session;
        _users = users;
        _hasher = hasher;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageUsers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        if (_session.User?.Id == command.UserId)
        {
            return Result.Failure(new InvalidState(UserMessages.CannotResetOwnPassword));
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var user = await _users.GetAsync(command.UserId, cancellationToken);
        if (user is null || user.IsSystem)
        {
            return Result.Failure(new NotFound());
        }

        user.SetPassword(_hasher.Hash(command.Password), mustChange: true);
        user.ClearLockout();
        _audit.Add(AuditActions.PasswordReset, AuditActions.UserEntity, user.Id, $"Usuario: {user.UserName}");

        var outcome = await _users.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogReset(user.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Contraseña restablecida por un administrador. UserId={UserId}")]
    private partial void LogReset(Guid userId);
}
