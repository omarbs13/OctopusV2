using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Sales;
using Pos.Domain.Users;

namespace Pos.Application.Users.CreateFirstAdmin;

/// <summary>
/// Asistente de primer arranque (FR-001). Comprueba "no hay usuarios" dentro de la transacción de
/// escritura, así que si se ejecuta dos veces solo uno prevalece (research §10). Reasigna a este
/// administrador la venta en curso que haya quedado a nombre de "Sistema" al migrar.
/// </summary>
public sealed partial class CreateFirstAdminHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ISaleDraftStore _drafts;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<CreateFirstAdminCommand> _validator;
    private readonly ILogger<CreateFirstAdminHandler> _logger;

    public CreateFirstAdminHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        ISaleDraftStore drafts,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<CreateFirstAdminCommand> validator,
        ILogger<CreateFirstAdminHandler> logger)
    {
        _users = users;
        _hasher = hasher;
        _drafts = drafts;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(CreateFirstAdminCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ProductRules.ToError(validation));
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        if (await _users.AnyRealUserAsync(cancellationToken))
        {
            return Result.Failure(new InvalidState(UserMessages.SetupAlreadyDone));
        }

        var admin = User.CreateFirstAdmin(command.FullName, command.UserName, _hasher.Hash(command.Password));
        _users.Add(admin);
        await _drafts.ReassignAsync(SystemUser.Id, admin.Id, cancellationToken);
        _audit.Add(AuditActions.UserCreated, AuditActions.UserEntity, admin.Id, $"Primer administrador: {admin.UserName}");

        var outcome = await _users.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            // Otro asistente ganó la carrera por el mismo nombre: ya hay un usuario real.
            return Result.Failure(new InvalidState(UserMessages.SetupAlreadyDone));
        }

        await transaction.CommitAsync(cancellationToken);
        LogCreated(admin.Id, admin.UserName);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Primer administrador creado. UserId={UserId} Usuario={UserName}")]
    private partial void LogCreated(Guid userId, string userName);
}
