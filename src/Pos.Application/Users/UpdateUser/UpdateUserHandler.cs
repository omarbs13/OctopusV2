using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Application.Users.Access;
using Pos.Application.Users.Session;
using Pos.Domain.Audit;
using Pos.Domain.Users;

namespace Pos.Application.Users.UpdateUser;

/// <summary>
/// Edición de un usuario (FR-015). Las protecciones de FR-018 viven aquí, dentro de la transacción de
/// escritura: nunca se deja al sistema sin administrador activo y nadie se desactiva ni se quita
/// el rol a sí mismo. Al desactivar se descarta la venta conservada del usuario (FR-022).
/// </summary>
public sealed partial class UpdateUserHandler
{
    private readonly IAccessControl _access;
    private readonly IUserSession _session;
    private readonly IUserRepository _users;
    private readonly ISaleDraftStore _drafts;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<UpdateUserCommand> _validator;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(
        IAccessControl access,
        IUserSession session,
        IUserRepository users,
        ISaleDraftStore drafts,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<UpdateUserCommand> validator,
        ILogger<UpdateUserHandler> logger)
    {
        _access = access;
        _session = session;
        _users = users;
        _drafts = drafts;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageUsers, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var user = await _users.GetAsync(command.Id, cancellationToken);
        if (user is null || user.IsSystem)
        {
            return Result.Failure(new NotFound());
        }

        if (user.Version != command.ExpectedVersion)
        {
            return Result.Failure(new Conflict());
        }

        var losesAdminAccess = user is { Role: UserRole.Admin, IsActive: true }
            && (command.Role != UserRole.Admin || !command.IsActive);
        if (losesAdminAccess
            && (_session.User?.Id == user.Id || await _users.CountActiveAdminsAsync(cancellationToken) <= 1))
        {
            return Result.Failure(new LastAdministrator());
        }

        var newName = UserNameRules.Normalize(command.UserName);
        if (newName != user.NormalizedUserName
            && await _users.FindByUserNameAsync(newName, cancellationToken) is { } other
            && other.Id != user.Id)
        {
            return Result.Failure(new Duplicate(UserFields.UserName));
        }

        var before = UserAuditFields.Snapshot(user);
        var wasActive = user.IsActive;

        user.Rename(command.FullName);
        user.ChangeUserName(command.UserName);
        user.ChangeRole(command.Role);
        if (command.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        // Los datos van en USER_UPDATED y el Estado en el evento de activación o desactivación (018, FR-002).
        var changes = AuditChanges.Compare(before, UserAuditFields.Snapshot(user));
        var fieldChanges = changes.Where(c => c.Field != UserAuditFields.State).ToList();
        var stateChanges = changes.Where(c => c.Field == UserAuditFields.State).ToList();
        if (AuditChanges.HasChanges(fieldChanges))
        {
            _audit.Add(Record(AuditActions.UserUpdated, user, $"Usuario: {user.UserName}. Rol: {user.Role.ToCode()}", fieldChanges));
        }

        if (wasActive && !user.IsActive)
        {
            _audit.Add(Record(AuditActions.UserDeactivated, user, $"Usuario: {user.UserName}", stateChanges));
            if (await _drafts.RemoveForAsync(user.Id, cancellationToken))
            {
                _audit.Add(AuditActions.HeldSaleDiscarded, AuditActions.UserEntity, user.Id, $"Usuario: {user.UserName}");
            }
        }
        else if (!wasActive && user.IsActive)
        {
            _audit.Add(Record(AuditActions.UserActivated, user, $"Usuario: {user.UserName}", stateChanges));
        }

        var outcome = await _users.SaveChangesAsync(cancellationToken);
        switch (outcome.Status)
        {
            case SaveStatus.Duplicate:
                return Result.Failure(new Duplicate(UserFields.UserName));
            case SaveStatus.Conflict:
                return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogUpdated(user.Id, user.IsActive);
        return Result.Success();
    }

    private static AuditRecord Record(string action, User user, string details, IReadOnlyList<AuditFieldChange> changes) =>
        new(action, AuditActions.UserEntity, user.Id, EntityName: user.UserName, Details: details, Changes: changes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuario modificado. UserId={UserId} Activo={IsActive}")]
    private partial void LogUpdated(Guid userId, bool isActive);
}
