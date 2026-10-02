using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Categories.SetCategoryActive;

/// <summary>
/// Activa o desactiva una categoría. La confirmación se exige aquí y no solo en la interfaz (SC-006); el
/// conteo se lee dentro de la transacción de escritura para que el número del aviso no quede desfasado.
/// Desactivar no quita la categoría a sus productos (FR-006).
/// </summary>
public sealed partial class SetCategoryActiveHandler
{
    private readonly IAccessControl _access;
    private readonly ICategoryRepository _categories;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly ILogger<SetCategoryActiveHandler> _logger;

    public SetCategoryActiveHandler(
        IAccessControl access,
        ICategoryRepository categories,
        IAuditLog audit,
        IWriteTransactions transactions,
        ILogger<SetCategoryActiveHandler> logger)
    {
        _access = access;
        _categories = categories;
        _audit = audit;
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(SetCategoryActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageProducts, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        await using var transaction = await _transactions.BeginAsync(cancellationToken);

        var category = await _categories.GetAsync(command.Id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(new NotFound());
        }

        if (category.Version != command.ExpectedVersion)
        {
            LogConflict(category.Id);
            return Result.Failure(new Conflict());
        }

        if (command.IsActive)
        {
            category.Activate();
            _audit.Add(AuditActions.CategoryActivated, AuditActions.CategoryEntity, category.Id, CategoryRules.Describe(category));
        }
        else
        {
            var count = await _categories.CountProductsAsync(category.Id, cancellationToken);
            if (count > 0 && !command.Confirmed)
            {
                return Result.Failure(new ConfirmationRequired(count));
            }

            category.Deactivate();
            _audit.Add(
                AuditActions.CategoryDeactivated,
                AuditActions.CategoryEntity,
                category.Id,
                $"{CategoryRules.Describe(category)}. Productos: {count}");
        }

        var outcome = await _categories.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            LogConflict(category.Id);
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogChanged(category.Id, command.IsActive);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Categoría activada o desactivada. CategoryId={CategoryId} Activa={Active}")]
    private partial void LogChanged(Guid categoryId, bool active);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cambio de estado de categoría rechazado por conflicto de versión. CategoryId={CategoryId}")]
    private partial void LogConflict(Guid categoryId);
}
