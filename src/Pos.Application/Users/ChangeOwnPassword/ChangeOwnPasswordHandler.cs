using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Session;

namespace Pos.Application.Users.ChangeOwnPassword;

/// <summary>Cambio de la contraseña propia (FR-025) y cambio obligatorio tras un restablecimiento.</summary>
public sealed partial class ChangeOwnPasswordHandler
{
    private readonly UserSession _session;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<ChangeOwnPasswordCommand> _validator;
    private readonly ILogger<ChangeOwnPasswordHandler> _logger;

    public ChangeOwnPasswordHandler(
        UserSession session,
        IUserRepository users,
        IPasswordHasher hasher,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<ChangeOwnPasswordCommand> validator,
        ILogger<ChangeOwnPasswordHandler> logger)
    {
        _session = session;
        _users = users;
        _hasher = hasher;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ChangeOwnPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ProductRules.ToError(validation));
        }

        var mandatory = command.SealedUserId is not null;
        var userId = mandatory
            ? (_session.IsSealed(command.SealedUserId!.Value) ? command.SealedUserId : null)
            : _session.User?.Id;
        if (userId is null)
        {
            return Result.Failure(new InvalidState("No hay una sesión para cambiar la contraseña."));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var user = await _users.GetAsync(userId.Value, cancellationToken);
        if (user is null || !user.IsActive || user.IsSystem || user.PasswordHash is null)
        {
            return Result.Failure(new InvalidState("El usuario no puede cambiar su contraseña."));
        }

        if (!mandatory && _hasher.Verify(command.CurrentPassword!, user.PasswordHash) == PasswordCheck.Failed)
        {
            return Result.Failure(Invalid(UserFields.CurrentPassword, UserMessages.CurrentPasswordIncorrect));
        }

        if (_hasher.Verify(command.NewPassword, user.PasswordHash) != PasswordCheck.Failed)
        {
            return Result.Failure(Invalid(UserFields.Password, UserMessages.NewPasswordSameAsCurrent));
        }

        user.SetPassword(_hasher.Hash(command.NewPassword), mustChange: false);
        _audit.Add(AuditActions.PasswordChanged, AuditActions.UserEntity, user.Id, null);

        var outcome = await _users.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogChanged(user.Id);
        return Result.Success();
    }

    private static ValidationFailed Invalid(string field, string message) => new([new FieldError(field, message)]);

    [LoggerMessage(Level = LogLevel.Information, Message = "Contraseña cambiada por el propio usuario. UserId={UserId}")]
    private partial void LogChanged(Guid userId);
}
