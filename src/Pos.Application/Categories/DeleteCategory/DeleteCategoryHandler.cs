using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Categories.DeleteCategory;

/// <summary>
/// Elimina una categoría en una sola transacción de escritura (research §4): cuenta los productos no
/// borrados (activos o inactivos) y, si no hay, marca la categoría como borrada y la quita de los productos
/// borrados que aún la tenían, cuyas ventas pasan a "Sin categoría". Como las escrituras están serializadas,
/// ningún producto vivo queda apuntando a una categoría borrada.
/// </summary>
public sealed partial class DeleteCategoryHandler
{
    private readonly IAccessControl _access;
    private readonly ICategoryRepository _categories;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IClock _clock;
    private readonly ILogger<DeleteCategoryHandler> _logger;

    public DeleteCategoryHandler(
        IAccessControl access,
        ICategoryRepository categories,
        IAuditLog audit,
        IWriteTransactions transactions,
        IClock clock,
        ILogger<DeleteCategoryHandler> logger)
    {
        _access = access;
        _categories = categories;
        _audit = audit;
        _transactions = transactions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageCategories, cancellationToken);
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

        var count = await _categories.CountProductsAsync(category.Id, cancellationToken);
        if (count > 0)
        {
            LogInUse(category.Id, count);
            return Result.Failure(new CategoryInUse(count));
        }

        category.Delete(_clock.UtcNow);
        await _categories.ClearFromDeletedProductsAsync(category.Id, cancellationToken);
        _audit.Add(new AuditRecord(
            AuditActions.CategoryDeleted,
            AuditActions.CategoryEntity,
            category.Id,
            EntityName: category.Name,
            Details: CategoryRules.Describe(category),
            Changes: AuditChanges.Removed(CategoryAuditFields.Snapshot(category))));

        var outcome = await _categories.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            LogConflict(category.Id);
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogDeleted(category.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Categoría eliminada. CategoryId={CategoryId}")]
    private partial void LogDeleted(Guid categoryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Eliminación de categoría rechazada: tiene productos. CategoryId={CategoryId} Productos={ProductCount}")]
    private partial void LogInUse(Guid categoryId, int productCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Eliminación de categoría rechazada por conflicto de versión. CategoryId={CategoryId}")]
    private partial void LogConflict(Guid categoryId);
}
