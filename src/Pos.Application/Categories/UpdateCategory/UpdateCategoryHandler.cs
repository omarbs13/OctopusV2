using FluentValidation;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Products;
using Pos.Application.Users.Access;
using Pos.Domain.Categories;
using Pos.Domain.Common;
using Pos.Domain.Users;

namespace Pos.Application.Categories.UpdateCategory;

/// <summary>Edita una categoría; el nombre sigue siendo único excluyendo a la propia (FR-003, FR-004).</summary>
public sealed partial class UpdateCategoryHandler
{
    private readonly IAccessControl _access;
    private readonly ICategoryRepository _categories;
    private readonly IAuditLog _audit;
    private readonly IWriteTransactions _transactions;
    private readonly IValidator<UpdateCategoryCommand> _validator;
    private readonly ILogger<UpdateCategoryHandler> _logger;

    public UpdateCategoryHandler(
        IAccessControl access,
        ICategoryRepository categories,
        IAuditLog audit,
        IWriteTransactions transactions,
        IValidator<UpdateCategoryCommand> validator,
        ILogger<UpdateCategoryHandler> logger)
    {
        _access = access;
        _categories = categories;
        _audit = audit;
        _transactions = transactions;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ManageCategories, cancellationToken);
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

        var nameKey = TextNormalizer.ForSearch(Category.NormalizeName(command.Name));
        if (await _categories.NameExistsAsync(nameKey, category.Id, cancellationToken))
        {
            LogDuplicate(category.Id);
            return Result.Failure(new Duplicate(CategoryFields.Name));
        }

        var before = CategoryAuditFields.Snapshot(category);
        category.Update(command.Name, command.Description);
        var changes = AuditChanges.Compare(before, CategoryAuditFields.Snapshot(category));
        if (AuditChanges.HasChanges(changes))
        {
            _audit.Add(new AuditRecord(
                AuditActions.CategoryUpdated,
                AuditActions.CategoryEntity,
                category.Id,
                EntityName: category.Name,
                Details: CategoryRules.Describe(category),
                Changes: changes));
        }

        var outcome = await _categories.SaveChangesAsync(cancellationToken);
        if (outcome.Status != SaveStatus.Saved)
        {
            if (outcome.Status == SaveStatus.Duplicate)
            {
                LogDuplicate(category.Id);
                return Result.Failure(new Duplicate(CategoryFields.Name));
            }

            LogConflict(category.Id);
            return Result.Failure(new Conflict());
        }

        await transaction.CommitAsync(cancellationToken);
        LogUpdated(category.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Categoría modificada. CategoryId={CategoryId}")]
    private partial void LogUpdated(Guid categoryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Edición de categoría rechazada por nombre repetido. CategoryId={CategoryId}")]
    private partial void LogDuplicate(Guid categoryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Edición de categoría rechazada por conflicto de versión. CategoryId={CategoryId}")]
    private partial void LogConflict(Guid categoryId);
}
