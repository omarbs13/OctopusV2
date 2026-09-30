using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Users.CreateUser;

/// <summary>
/// Alta de usuario por un Administrador (FR-015). La contraseña inicial es temporal: el usuario
/// debe cambiarla en su primer inicio de sesión.
/// </summary>
public sealed partial class CreateUserHandler
{
    private readonly IAccessControl _access;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<CreateUserCommand> _validator;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IAccessControl access,
        IUserRepository users,
        IPasswordHasher hasher,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<CreateUserCommand> validator,
        ILogger<CreateUserHandler> logger)
    {
        _access = access;
        _users = users;
        _hasher = hasher;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageUsers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure<Guid>(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        if (await _users.FindByUserNameAsync(UserNameRules.Normalize(command.UserName), cancellationToken) is not null)
        {
            return Result.Failure<Guid>(new Duplicate(UserFields.UserName));
        }

        var user = User.Create(command.FullName, command.UserName, command.Role, _hasher.Hash(command.Password));
        if (!command.IsActive)
        {
            user.Deactivate();
        }

        _users.Add(user);
        _audit.Add(AuditActions.UserCreated, AuditActions.UserEntity, user.Id, $"Usuario: {user.UserName}. Rol: {user.Role.ToCode()}");

        var outcome = await _users.SaveChangesAsync(cancellationToken);
        if (outcome.Status == SaveStatus.Duplicate)
        {
            return Result.Failure<Guid>(new Duplicate(UserFields.UserName));
        }

        if (outcome.Status != SaveStatus.Saved)
        {
            return Result.Failure<Guid>(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogCreated(user.Id, user.UserName);
        return Result.Success(user.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuario creado. UserId={UserId} Usuario={UserName}")]
    private partial void LogCreated(Guid userId, string userName);
}
